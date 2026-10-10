using System.Text.Json;

namespace AiPortfolioAssistant.Api.Knowledge;

public static class KnowledgeFile
{
    public static async Task<List<KnowledgeItem>> LoadAsync(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "Data", "knowledge.json");
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<KnowledgeItem>>(stream, JsonSerializerOptions.Web) ?? [];
    }
}