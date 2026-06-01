namespace SarhSummarizer.Models;

public record SummarizeRequest(string Text);

public record SummarizeResponse(string JobId, string Status);

public record JobStatusResponse(
    string JobId,
    string Status,
    string? Summary = null,
    int? Tokens = null,
    decimal? CostUsd = null,
    string? ErrorReason = null);

public record ErrorResponse(string Error);
