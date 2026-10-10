using System.Net.Http.Json;

namespace AiPortfolioAssistant.Api.Embeddings;

public class GeminiEmbeddingClient(HttpClient http, IConfiguration config)
{
    private const string Model = "gemini-embedding-2";
    public const int Dimensions = 768;

    public async Task<float[]> EmbedAsync(string text)
    {
        var apiKey = config["Gemini:ApiKey"] 
        ?? throw new InvalidOperationException("Gemini:ApiKey가 설정되지 않았습니다. dotnet user-secrets를 확인하세요.");
    
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Model}:embedContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(new{
            content = new { parts = new[] { new { text } } },
            output_dimensionality = Dimensions
        });

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<EmbedResponse>();
        return body!.Embedding.Values;
    }

    private record EmbedResponse(EmbeddingValues Embedding);
    private record EmbeddingValues(float[] Values); 
}