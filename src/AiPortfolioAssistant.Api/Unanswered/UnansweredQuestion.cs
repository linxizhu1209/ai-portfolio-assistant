namespace AiPortfolioAssistant.Api.Unanswered;

public class UnansweredQuestion
{
    public int Id { get; set; }
    public required string Question { get; set; }
    public float? TopScore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public UnansweredStatus Status { get; set; } = UnansweredStatus.Pending;
}

public enum UnansweredStatus
{
    Pending,
    Resolved
}