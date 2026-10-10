using System.Text.Json;
using AiPortfolioAssistant.Api.Knowledge;
using Qdrant.Client;
using AiPortfolioAssistant.Api.Embeddings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = builder.Configuration.GetValue("Qdrant:Port", 6334);
builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));

builder.Services.AddHttpClient<GeminiEmbeddingClient>(client =>
client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/qdrant/collections", async (QdrantClient qdrant) =>
{
    var collections = await qdrant.ListCollectionsAsync();
    return Results.Ok(collections);
});

app.MapGet("/knowledge", async (IWebHostEnvironment env) =>
{
    var path = Path.Combine(env.ContentRootPath, "Data", "knowledge.json");
    await using var stream = File.OpenRead(path);
    var items = await JsonSerializer.DeserializeAsync<List<KnowledgeItem>>(stream, JsonSerializerOptions.Web);
    return Results.Ok(items);
});

app.MapGet("/embed", async (string text, GeminiEmbeddingClient embedder) =>
{
    var vector = await embedder.EmbedAsync(text);
    return Results.Ok(new { length = vector.Length, first5 = vector.Take(5) });
});

app.Run();
