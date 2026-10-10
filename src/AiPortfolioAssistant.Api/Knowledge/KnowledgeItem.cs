namespace AiPortfolioAssistant.Api.Knowledge;

public record KnowledgeItem(
    Guid Id,
    string Category,
    string Title,
    string Content,
    string? SourceUrl
);