using AiPortfolioAssistant.Api.Rag;
using AiPortfolioAssistant.Api.Knowledge;
using Qdrant.Client;
using AiPortfolioAssistant.Api.Embeddings;
using Qdrant.Client.Grpc;
using AiPortfolioAssistant.Api.Llm;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = builder.Configuration.GetValue("Qdrant:Port", 6334);
builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));

builder.Services.AddHttpClient<GeminiEmbeddingClient>(client =>
client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));

builder.Services.AddHttpClient<GeminiChatClient>(client =>
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

app.MapGet("/search", async (string q, GeminiEmbeddingClient embedder, QdrantClient qdrant) =>
{
    var vector = await embedder.EmbedAsync($"task: search result | query: {q}");
    var results = await qdrant.QueryAsync(KnowledgeCollection, query: vector, limit: 3, payloadSelector: true);

    return Results.Ok(results.Select(r => new
    {
        score = r.Score,
        title = r.Payload["title"].StringValue,
        content = r.Payload["content"].StringValue
    }));
});

app.MapGet("/chat", async (string q, GeminiChatClient llm) =>
{
    var answer = await llm.GenerateAsync(
        "당신은 포트폴리오 주인에 대한 질문에 답하는 어시스턴트입니다. 한국어로 답하세요.",
        q);
    return Results.Ok(new { answer });
});

app.MapGet("/ask", async (string q, GeminiEmbeddingClient embedder, QdrantClient qdrant, GeminiChatClient llm, IConfiguration config) => 
{
    var threshold = config.GetValue("Rag:ScoreThreshold", 0.65f);
    var topK = config.GetValue("Rag:TopK", 3);

    // 질문 임베딩 -> 검색
    var vector = await embedder.EmbedAsync($"task: search result | query: {q}");
    var results = await qdrant.QueryAsync(KnowledgeCollection, query: vector, limit: (ulong)topK, payloadSelector: true);

    // threshold 필터
    var sources = results.Where(r => r.Score >= threshold).Select(r => new {
        title = r.Payload["title"].StringValue,
        content = r.Payload["content"].StringValue,
        score = r.Score
    })
    .ToList();

    // 근거가 없으면 LLM 호출 x
    if (sources.Count == 0)
    {
        return Results.Ok(new { answer = RagPrompt.NoAnswer, answered = false, sources });
    }

    // 프롬프트 조립 -> LLM 호출
    var userMessage = RagPrompt.BuildUserMessage(q, sources.Select(s => (s.title, s.content)));
    var answer = (await llm.GenerateAsync(RagPrompt.SystemInstruction, userMessage)).Trim();

    return Results.Ok(new { answer, answered = answer != RagPrompt.NoAnswer, sources });
});

app.Run();
