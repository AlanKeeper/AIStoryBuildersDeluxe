using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using AIStoryBuilders.Services;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Files;
using OpenAI.FineTuning;
using OpenAI.Models;
using System.ClientModel;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        public event EventHandler<ReadTextEventArgs> ReadTextEvent;
        public SettingsService SettingsService { get; set; }
        public LogService LogService { get; set; }
        public DatabaseService DatabaseService { get; set; }
        public string Summary { get; set; }

        public List<(string, float)> similarities = new List<(string, float)>();

        public Dictionary<string, string> AIStoryBuildersMemory = new Dictionary<string, string>();

        // Constructor
        public OrchestratorMethods(SettingsService _SettingsService, LogService _LogService, DatabaseService _DatabaseService)
        {
            SettingsService = _SettingsService;
            LogService = _LogService;
            DatabaseService = _DatabaseService;
        }

		// Memory and Vectors

		#region public async Task<string> GetVectorEmbedding(string EmbeddingContent, bool Combine)
		public async Task<string> GetVectorEmbedding(string EmbeddingContent, bool Combine)
		{
			float[] embeddingVectors = SettingsService.AIType switch
			{
				"OpenAI" => await GetOpenAIEmbeddingAsync(EmbeddingContent),
				"Azure OpenAI" => await GetAzureOpenAIEmbeddingAsync(EmbeddingContent),
				"LM Studio" => await GetLmStudioEmbeddingAsync(EmbeddingContent),
                "Ollama" => await GetOllamaEmbeddingAsync(EmbeddingContent),
				_ => throw new NotSupportedException($"AIType '{SettingsService.AIType}' is not supported for embeddings.")
			};

			var VectorsToSave = "[" + string.Join(",", embeddingVectors) + "]";
			return Combine ? EmbeddingContent + "|" + VectorsToSave : VectorsToSave;
		}

		private async Task<float[]> GetOpenAIEmbeddingAsync(string content)
		{
			var openAIClient = new OpenAIClient(SettingsService.ApiKey);
			var generator = openAIClient.AsEmbeddingGenerator("text-embedding-ada-002");
			var embeddings = await generator.GenerateAsync(new[] { content });
			return embeddings[0].Vector.ToArray();
		}

		private async Task<float[]> GetAzureOpenAIEmbeddingAsync(string content)
		{
			var azureClient = new AzureOpenAIClient(new Uri(SettingsService.Endpoint), new AzureKeyCredential(SettingsService.ApiKey));
			var generator = azureClient.AsEmbeddingGenerator(SettingsService.AIEmbeddingModel);
			var embeddings = await generator.GenerateAsync(new[] { content });
			return embeddings[0].Vector.ToArray();
		}

		private async Task<float[]> GetLmStudioEmbeddingAsync(string content)
		{
			string baseEndpoint = SettingsService.Endpoint.TrimEnd('/');
			string embeddingsEndpoint = baseEndpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
				? $"{baseEndpoint}/embeddings"
				: $"{baseEndpoint}/v1/embeddings";

			using var httpClient = new HttpClient();
			var requestBody = new
			{
				input = content,
				model = SettingsService.AIEmbeddingModel
			};

			var response = await httpClient.PostAsJsonAsync(embeddingsEndpoint, requestBody);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync();
			var obj = System.Text.Json.JsonDocument.Parse(json);
			return obj.RootElement
				.GetProperty("data")[0]
				.GetProperty("embedding")
				.EnumerateArray()
				.Select(x => x.GetSingle())
				.ToArray();
		}

		private async Task<float[]> GetOllamaEmbeddingAsync(string content)
		{
			string endpoint = SettingsService.Endpoint.TrimEnd('/');
			string embeddingsEndpoint = $"{endpoint}/api/embeddings";

			using var httpClient = new HttpClient();
			var requestBody = new
			{
				model = SettingsService.AIEmbeddingModel,
				prompt = content
			};

			var response = await httpClient.PostAsJsonAsync(embeddingsEndpoint, requestBody);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync();
			var obj = System.Text.Json.JsonDocument.Parse(json);
			return obj.RootElement
				.GetProperty("embedding")
				.EnumerateArray()
				.Select(x => x.GetSingle())
				.ToArray();
		}
		#endregion

		#region public async Task<string> GetVectorEmbeddingAsFloats(string EmbeddingContent)
		public async Task<float[]> GetVectorEmbeddingAsFloats(string EmbeddingContent)
		{
			return SettingsService.AIType switch
			{
				"OpenAI" => await GetOpenAIEmbeddingAsync(EmbeddingContent),
				"Azure OpenAI" => await GetAzureOpenAIEmbeddingAsync(EmbeddingContent),
				"LM Studio" => await GetLmStudioEmbeddingAsync(EmbeddingContent),
				"Ollama" => await GetOllamaEmbeddingAsync(EmbeddingContent),
				_ => throw new NotSupportedException($"AIType '{SettingsService.AIType}' is not supported for embeddings.")
			};
		}
		#endregion

		// Utility Methods

		public IChatClient CreateOpenAIChatClient()
        {
            return CreateAIChatClient(SettingsService.AIType, SettingsService.AIModel, SettingsService.ApiKey, SettingsService.Endpoint, SettingsService.AIEmbeddingModel);
        }

        public IChatClient CreateOpenAIClient(string GPTModel)
        {
            return CreateAIChatClient(SettingsService.AIType, GPTModel, SettingsService.ApiKey, SettingsService.Endpoint, SettingsService.AIEmbeddingModel);
        }

		#region public IChatClient CreateAIChatClient(string AIType, string AIModel, string ApiKey, string Endpoint, string AIEmbeddingModel)
		public IChatClient CreateAIChatClient(string AIType, string AIModel, string ApiKey, string Endpoint, string AIEmbeddingModel)
		{
			if (AIType == "OpenAI")
			{
				ApiKeyCredential apiKeyCredential = new ApiKeyCredential(ApiKey);

				OpenAIClientOptions options = new OpenAIClientOptions();
				options.NetworkTimeout = TimeSpan.FromSeconds(520);

				return new OpenAIClient(
					apiKeyCredential, options)
					.AsChatClient(AIModel);
			}
			else if (AIType == "Azure OpenAI")
			{
				ApiKeyCredential apiKeyCredential = new ApiKeyCredential(ApiKey);

				AzureOpenAIClientOptions options = new AzureOpenAIClientOptions();
				options.NetworkTimeout = TimeSpan.FromSeconds(520);

				return new AzureOpenAIClient(
					new Uri(Endpoint),
					apiKeyCredential, options)
					.AsChatClient(AIModel);
			}
			else if (AIType == "LM Studio")
			{
				var options = new OpenAIClientOptions
				{
					Endpoint = new Uri(Endpoint),
					NetworkTimeout = TimeSpan.FromSeconds(520)
				};
				// LM Studio doesn’t validate the key, so any non-empty string will work
				var credential = new ApiKeyCredential("dummy-api-key");
				return new OpenAIClient(credential, options)
					.AsChatClient(AIModel);
			}
			else // Ollama
			{
				return new OllamaChatClient(
					new Uri(Endpoint),
					AIModel);
			}
		}
		#endregion

		#region public IChatClient CreateEmbeddingOpenAIClient()
		public IChatClient CreateEmbeddingOpenAIClient()
        {
            string Organization = SettingsService.Organization;
            string ApiKey = SettingsService.ApiKey;
            string Endpoint = SettingsService.Endpoint;
            string ApiVersion = SettingsService.ApiVersion;
            string AIEmbeddingModel = SettingsService.AIEmbeddingModel;
            string AIModel = SettingsService.AIModel;

            ApiKeyCredential apiKeyCredential = new ApiKeyCredential(ApiKey);

            if (SettingsService.AIType == "OpenAI")
            {
                OpenAIClientOptions options = new OpenAIClientOptions();
                options.OrganizationId = Organization;
                options.NetworkTimeout = TimeSpan.FromSeconds(520);

                return new OpenAIClient(
                    apiKeyCredential, options)
                    .AsChatClient(AIModel);
            }
            else // Azure OpenAI
            {
                AzureOpenAIClientOptions options = new AzureOpenAIClientOptions();
                options.NetworkTimeout = TimeSpan.FromSeconds(520);

                return new AzureOpenAIClient(
                    new Uri(Endpoint),
                    apiKeyCredential, options)
                    .AsChatClient(AIEmbeddingModel);
            }
        }
        #endregion

        #region public float CosineSimilarity(float[] vector1, float[] vector2)
        public float CosineSimilarity(float[] vector1, float[] vector2)
        {
            // Initialize variables for dot product and
            // magnitudes of the vectors
            float dotProduct = 0;
            float magnitude1 = 0;
            float magnitude2 = 0;

            // Iterate through the vectors and calculate
            // the dot product and magnitudes
            for (int i = 0; i < vector1?.Length; i++)
            {
                // Calculate dot product
                dotProduct += vector1[i] * vector2[i];

                // Calculate squared magnitude of vector1
                magnitude1 += vector1[i] * vector1[i];

                // Calculate squared magnitude of vector2
                magnitude2 += vector2[i] * vector2[i];
            }

            // Take the square root of the squared magnitudes
            // to obtain actual magnitudes
            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            // Calculate and return cosine similarity by dividing
            // dot product by the product of magnitudes
            return dotProduct / (magnitude1 * magnitude2);
        }
        #endregion

        #region private string CombineAndSortLists(string paramExistingList, string paramNewList)
        private string CombineAndSortLists(string paramExistingList, string paramNewList)
        {
            // Split the lists into an arrays
            string[] ExistingListArray = paramExistingList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] NewListArray = paramNewList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Combine the lists
            string[] CombinedListArray = ExistingListArray.Concat(NewListArray).ToArray();

            // Remove duplicates
            CombinedListArray = CombinedListArray.Distinct().ToArray();

            // Sort the array
            Array.Sort(CombinedListArray);

            // Combine the array into a string
            string CombinedList = string.Join("\n", CombinedListArray);

            return CombinedList;
        }
        #endregion

        #region public static string TrimToMaxWords(string input, int maxWords = 500)
        public static string TrimToMaxWords(string input, int maxWords = 500)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] words = input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length <= maxWords)
                return input;

            return string.Join(" ", words.Take(maxWords));
        }
        #endregion

        #region public bool IsValidFolderName(string folderName)
        public bool IsValidFolderName(string folderName)
        {
            string invalidChars = new string(Path.GetInvalidPathChars()) + new string(Path.GetInvalidFileNameChars());
            Regex containsABadCharacter = new Regex("[" + Regex.Escape(invalidChars) + "]");
            if (containsABadCharacter.IsMatch(folderName))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion

        #region public static string TrimInnerSpaces(string input)
        public static string TrimInnerSpaces(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return Regex.Replace(input, @"\s{2,}", " ");
        }
        #endregion

        #region public string SanitizeFileName(string input)
        private static readonly char[] InvalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
        private static readonly string[] ReservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

        public string SanitizeFileName(string input)
        {
            // Strip out invalid characters
            string sanitized = new string(input.Where(ch => !InvalidFileNameChars.Contains(ch)).ToArray());

            // Remove the | character
            sanitized = sanitized.Replace("|", "");

            return sanitized;
        }
        #endregion

        #region public string ExtractJson(string json)
        /// <summary>
        /// Extracts the JSON object from a Markdown code block (```json … ```).
        /// If no fenced block is found, returns the input unchanged.
        /// </summary>
        public static string ExtractJson(string json)
        {
            // Pattern captures the JSON object between ```json and ```
            const string pattern = @"```json\s*(\{[\s\S]*?\})\s*```";
            var match = Regex.Match(json, pattern, RegexOptions.Singleline);
            return match.Success
                ? match.Groups[1].Value   // the raw JSON
                : json;                  // fallback to original if no match
        }
        #endregion

        #region public static List<string> ParseStringToList(string input)
        public static List<string> ParseStringToList(string input)
        {
            // Remove the brackets and split the string by comma
            string[] items = Regex.Replace(input, @"[\[\]]", "").Split(',');

            // Convert the array to a List<string> and return
            return new List<string>(items);
        }
        #endregion

        #region public class ReadTextEventArgs : EventArgs
        public class ReadTextEventArgs : EventArgs
        {
            public string Message { get; set; }
            public int DisplayLength { get; set; }

            public ReadTextEventArgs(string message, int display_length)
            {
                Message = message;
                DisplayLength = display_length;
            }
        }
        #endregion

        #region public enum OpenAIServiceType
        public enum OpenAIServiceType
        {
            OpenAI,
            AzureOpenAI
        }
        #endregion
    }
}