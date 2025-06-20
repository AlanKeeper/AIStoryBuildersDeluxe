using Newtonsoft.Json;
using System.Threading.Tasks;

namespace AIStoryBuilders.Model
{
	public class SettingsService
	{
		// Properties
		public string Organization { get; set; }
		public string ApiKey { get; set; }
		public string AIModel { get; set; }
		public string AIType { get; set; }
		public string Endpoint { get; set; }
		public string AIEmbeddingModel { get; set; }
		public string ApiVersion { get; set; }

		private readonly string settingsPath;

		public SettingsService()
		{
			settingsPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/AIStoryBuilders/AIStoryBuildersSettings.config";
			LoadSettings();
		}

		public void LoadSettings()
		{
			string settingsJson = File.ReadAllText(settingsPath);
			dynamic settingsObj = JsonConvert.DeserializeObject(settingsJson);

			// Default to OpenAI if not set
			string aiType = settingsObj.ApplicationSettings.AIType ?? "OpenAI";
			AIType = aiType;

			// Load the correct section based on AIType
			dynamic aiSettings = GetAISettingsSection(settingsObj, aiType);

			Organization = aiSettings?.Organization;
			ApiKey = aiSettings?.ApiKey;
			AIModel = aiSettings?.AIModel;
			Endpoint = aiSettings?.Endpoint;
			ApiVersion = aiSettings?.ApiVersion;
			AIEmbeddingModel = aiSettings?.AIEmbeddingModel;
		}

		public async Task SaveSettings(
			string paramOrganization,
			string paramApiKey,
			string paramAIModel,
			string paramAIType,
			string paramEndpoint,
			string paramApiVersion,
			string paramAIEmbeddingModel)
		{
			string settingsJson = File.ReadAllText(settingsPath);
			dynamic settingsObj = JsonConvert.DeserializeObject(settingsJson);

			// Update the selected AI type in ApplicationSettings
			settingsObj.ApplicationSettings.AIType = paramAIType;

			// Get or create the section for the selected AI type
			dynamic aiSettings = GetAISettingsSection(settingsObj, paramAIType, createIfMissing: true);

			// Save only to the selected AI type section
			aiSettings.Organization = paramOrganization;
			aiSettings.ApiKey = paramAIType == "Local" ? null : paramApiKey;
			aiSettings.AIModel = paramAIModel;
			aiSettings.Endpoint = paramEndpoint;
			aiSettings.ApiVersion = paramApiVersion;
			aiSettings.AIEmbeddingModel = paramAIEmbeddingModel;

			// Write back to file
			string updatedJson = JsonConvert.SerializeObject(settingsObj, Formatting.Indented);
			await File.WriteAllTextAsync(settingsPath, updatedJson);

			// Update properties
			Organization = paramOrganization;
			ApiKey = paramApiKey;
			AIModel = paramAIModel;
			AIType = paramAIType;
			Endpoint = paramEndpoint;
			ApiVersion = paramApiVersion;
			AIEmbeddingModel = paramAIEmbeddingModel;
		}

		private dynamic GetAISettingsSection(dynamic settingsObj, string aiType, bool createIfMissing = false)
		{
			string sectionName = aiType switch
			{
				"OpenAI" => "OpenAIServiceOptions",
				"Azure OpenAI" => "AzureOpenAIServiceOptions",
				"LM Studio" => "LMStudioServiceOptions",
				"Ollama" => "OllamaServiceOptions",

				// Add more mappings as needed
				_ => $"{aiType.Replace(" ", "")}ServiceOptions"
			};

			var section = settingsObj[sectionName];
			if (section == null && createIfMissing)
			{
				section = new Newtonsoft.Json.Linq.JObject();
				settingsObj[sectionName] = section;
			}
			return section;
		}

		public string GetApiKey(string aiType)
		{
			string settingsJson = File.ReadAllText(settingsPath);
			dynamic settingsObj = JsonConvert.DeserializeObject(settingsJson);
			dynamic aiSettings = GetAISettingsSection(settingsObj, aiType, false);
			return aiSettings?.ApiKey ?? string.Empty;
		}

		public string GetAIModel(string aiType)
		{
			string settingsJson = File.ReadAllText(settingsPath);
			dynamic settingsObj = JsonConvert.DeserializeObject(settingsJson);
			dynamic aiSettings = GetAISettingsSection(settingsObj, aiType, false);
			return aiSettings?.AIModel ?? string.Empty;
		}

		public string GetAIEmbeddingModel(string aiType)
		{
			string settingsJson = File.ReadAllText(settingsPath);
			dynamic settingsObj = JsonConvert.DeserializeObject(settingsJson);
			dynamic aiSettings = GetAISettingsSection(settingsObj, aiType, false);
			return aiSettings?.AIEmbeddingModel ?? string.Empty;
		}

		public string GetEndpoint(string aiType)
		{
			string settingsJson = File.ReadAllText(settingsPath);
			dynamic settingsObj = JsonConvert.DeserializeObject(settingsJson);
			dynamic aiSettings = GetAISettingsSection(settingsObj, aiType, false);
			return aiSettings?.Endpoint ?? string.Empty;
		}

		public string GetApiVersion(string aiType)
		{
			string settingsJson = File.ReadAllText(settingsPath);
			dynamic settingsObj = JsonConvert.DeserializeObject(settingsJson);
			dynamic aiSettings = GetAISettingsSection(settingsObj, aiType, false);
			return aiSettings?.ApiVersion ?? string.Empty;
		}
	}
}