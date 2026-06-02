namespace SarhSummarizer.Models;

public class JobResponse
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int? Tokens { get; set; }
    public decimal? CostUsd { get; set; }
    public JobProgress? Progress { get; set; }
    public string? Error { get; set; }
}

public class JobProgress
{
    public int ChunksDone { get; set; }
    public int ChunksTotal { get; set; }
}