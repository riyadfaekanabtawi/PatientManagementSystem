using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
            _logger.LogInformation("🔄 Sending request to ML service...");
            var response = await _httpClient.PostAsync("http://127.0.0.1:5001/flask-api/generate", content);

            var responseData = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"📩 API Response (Raw): {responseData}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"❌ API call failed. Status: {response.StatusCode}, Response: {responseData}");
                return ""; // Return empty string to indicate failure
            }

            try
            {
                var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseData);

                if (jsonResponse != null && jsonResponse.ContainsKey("modelFileUrl"))
                {
                    return jsonResponse["modelFileUrl"];
                }
                else
                {
                    _logger.LogError("❌ API response did not contain 'modelFileUrl'.");
                    return "";
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError($"⚠️ Failed to parse API response as JSON. Exception: {ex.Message}");
                return "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Exception calling ML service: {ex.Message}");
            return "";
        }
    }

}
