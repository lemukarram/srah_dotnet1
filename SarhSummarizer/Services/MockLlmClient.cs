using System;
using System.Threading;
using System.Threading.Tasks;

namespace SarhSummarizer.Services;

public class MockLlmClient : ILlmClient
{
    private readonly Random _random = new();

    public async Task<LlmResponse> SummarizeAsync(string prompt, CancellationToken ct)
    {
        // Simulate network + model latency (honor cancellation!)
        var delay = TimeSpan.FromMilliseconds(_random.Next(1500, 3500));
        await Task.Delay(delay, ct);

        // ~15% transient failures: timeouts and rate limits
        var roll = _random.NextDouble();
        if (roll < 0.08) throw new TimeoutException("Mock LLM timeout");
        if (roll < 0.15) throw new RateLimitException("Mock 429: slow down");

        var inputTokens = prompt.Length / 4;
        return new LlmResponse(
            Summary: $"Mock summary of {prompt.Length} chars.",
            InputTokens: inputTokens,
            OutputTokens: 50,
            Model: "mock-gpt-4"
        );
    }
}

public class RateLimitException : Exception
{
    public RateLimitException(string message) : base(message) { }
}
