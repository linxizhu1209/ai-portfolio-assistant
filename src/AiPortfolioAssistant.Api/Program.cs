using AiPortfolioAssistant.Api.Knowledge;
using Qdrant.Client;
using AiPortfolioAssistant.Api.Embeddings;
using Qdrant.Client.Grpc;

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

const string KnowledgeCollection = "knowledge";

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

app.MapGet("/embed", async (string text, GeminiEmbeddingClient embedder) =>
{
    var vector = await embedder.EmbedAsync(text);
    return Results.Ok(new { length = vector.Length, first5 = vector.Take(5) });
});

app.MapPost("/knowledge/index", async (IWebHostEnvironment env, GeminiEmbeddingClient embedder, QdrantClient qdrant) =>
{
    if (!await qdrant.CollectionExistsAsync(KnowledgeCollection))
    {
        await qdrant.CreateCollectionAsync(KnowledgeCollection,
            new VectorParams { Size = GeminiEmbeddingClient.Dimensions, Distance = Distance.Cosine });
    }

    var items = await KnowledgeFile.LoadAsync(env.ContentRootPath);
    var points = new List<PointStruct>();

    foreach (var item in items)
    {
        var vector = await embedder.EmbedAsync($"title: {item.Title} | text: {item.Content}");
        points.Add(new PointStruct
        {
            Id = item.Id,
            Vectors = vector,
            Payload =
            {
                ["category"] = item.Category,
                ["title"] = item.Title,
                ["content"] = item.Content,
                ["sourceUrl"] = item.SourceUrl ?? ""
            }
        });
    }

    await qdrant.UpsertAsync(KnowledgeCollection, points);
    return Results.Ok(new { indexed = points.Count });
});


app.Run();
