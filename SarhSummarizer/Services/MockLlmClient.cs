namespace SarhSummarizer.Services;

public class MockLlmClient : IMockLlmClient
{
    private readonly Random _random = new();

    public async Task<LlmResult> SummarizeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_random.Next(500, 2000), cancellationToken);

        if (_random.NextDouble() < 0.2)
        {
            throw new HttpRequestException("Simulated transient network error (5xx/429)");
        }

        int tokens = text.Length / 4;

        return new LlmResult
        {
            Summary = $"Summary of chunk (length {text.Length}): {text.Substring(0, Math.Min(50, text.Length))}...",
            TokensUsed = tokens
        };
    }
}