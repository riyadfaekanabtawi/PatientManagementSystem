using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace PatientManagementSystem.Services
{
    public class ThreeDModelService : I3DModelService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ThreeDModelService> _logger;

        public ThreeDModelService(HttpClient httpClient, ILogger<ThreeDModelService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GenerateModelAsync(string frontImage, string leftImage, string rightImage, string backImage)
        {
            var requestBody = new
            {
                front = frontImage,
                left = leftImage,
                right = rightImage,
                back = backImage
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation("🚀 Sending request to 3D Model API...");

                var response = await _httpClient.PostAsync("http://127.0.0.1:5001/flask-api/generate", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ 3D Model API failed: {response.StatusCode} {response.ReasonPhrase}");
                    return "null";
                }

                var responseData = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"📩 API Response JSON: {responseData}");

                // ✅ Debug: Print response before parsing
                var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseData);

                if (jsonResponse == null)
                {
                    _logger.LogError("❌ API response was null.");
                    return "null";
                }

                _logger.LogInformation($"🔍 JSON Keys Available: {string.Join(", ", jsonResponse.Keys)}");

                if (jsonResponse.ContainsKey("modelFileUrl"))
                {
                    string modelFileUrl = jsonResponse["modelFileUrl"].GetString() ?? "";
                    _logger.LogInformation($"✅ Found modelFileUrl: {modelFileUrl}");
                    return modelFileUrl;
                }

                _logger.LogError("❌ API response did not contain 'modelFileUrl'.");
                return "null";
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Exception calling 3D Model API: {ex.Message}");
                return "null";
            }
        }
    }
}
