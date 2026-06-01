namespace SarhSummarizer.Models;

public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class SummarizationJob
{
    public string JobId { get; set; } = Guid.NewGuid().ToString();
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string Text { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int? Tokens { get; set; }
    public decimal? CostUsd { get; set; }
    public string? ErrorReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
