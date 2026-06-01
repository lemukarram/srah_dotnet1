using SarhSummarizer.Models;

namespace SarhSummarizer.Services;

public class MockLlmClient : ILlmClient
{
    private readonly Random _random = new();

    public async Task<LlmResponse> SummarizeAsync(string text, CancellationToken cancellationToken = default)
    {
        // Simulate 10% failure rate (TimeoutException as per brief)
        if (_random.NextDouble() < 0.1)
        {
            throw new TimeoutException("Mock LLM client timeout.");
        }

        // Simulate network latency (0.5 to 2 seconds)
        await Task.Delay(_random.Next(500, 2000), cancellationToken);

        // Simple mock summarization: take the first few words or a generic summary
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var summary = $"[Summary] This is a mock summary of the provided text. Length: {words.Length} words.";
        
        // Mock token counting (roughly 1.3 tokens per word)
        int inputTokens = (int)(words.Length * 1.33);
        int outputTokens = (int)(summary.Split(' ').Length * 1.33);

        return new LlmResponse(summary, inputTokens, outputTokens);
    }
}
