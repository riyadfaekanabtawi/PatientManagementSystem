using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ThreeDModelService : I3DModelService
{
    private readonly HttpClient _httpClient;

    public ThreeDModelService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
            var response = await _httpClient.PostAsync("http://18.117.78.61/flask-api/generate", content);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseData);
                return jsonResponse["modelFileUrl"];
            }
            else
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error calling ML service: " + ex.Message);
            return null;
        }
    }
}
