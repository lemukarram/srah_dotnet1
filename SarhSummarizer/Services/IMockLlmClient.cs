namespace SarhSummarizer.Services;

public class LlmResult
{
    public string Summary { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
}

public interface IMockLlmClient
{
    Task<LlmResult> SummarizeTextAsync(string text, CancellationToken cancellationToken = default);
}