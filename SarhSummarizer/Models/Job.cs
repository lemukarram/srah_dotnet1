using System.Text.Json.Serialization;

namespace SarhSummarizer.Models;

public class Job
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Text { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal"; // "normal" or "high"
    public string? IdempotencyKey { get; init; }
    
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? Summary { get; set; }
    public int Tokens { get; set; }
    public decimal CostUsd { get; set; }
    
    public int ChunksDone { get; set; }
    public int ChunksTotal { get; set; }
    
    public string? Error { get; set; }
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    [JsonIgnore]
    public CancellationTokenSource Cts { get; } = new CancellationTokenSource();
}