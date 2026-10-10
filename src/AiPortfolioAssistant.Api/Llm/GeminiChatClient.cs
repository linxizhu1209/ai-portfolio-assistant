using System.Net.Http.Json;

namespace AiPortfolioAssistant.Api.Llm;

public class GeminiChatClient(HttpClient http, IConfiguration config)
{
    public async Task<string> GenerateAsync(string systemInstruction, string userMessage)
    {
        var apiKey = config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey가 설정되지 않았습니다. dotnet user-secrets를 확인하세요.");
        var model = config["Gemini:ChatModel"] ?? "gemini-3.5-flash";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            system_instruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userMessage } } } },
            generationConfig = new
            {
                temperature = 0.2,
                thinkingConfig = new { thinkingLevel = "low" }
            }
        });

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GenerateResponse>();
        var parts = body?.Candidates?.FirstOrDefault()?.Content?.Parts ?? [];
        return string.Concat(parts.Select(p => p.Text));    
    }
    
    private record GenerateResponse(Candidate[]? Candidates);
    private record Candidate(Content? Content);
    private record Content(Part[]? Parts);
    private record Part(string? Text);
}