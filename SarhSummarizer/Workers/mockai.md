Implement a mock client behind an interface. It must simulate the real-world behaviour your code has to handle: latency, occasional failures, rate limiting, and cancellation. A trivial always-succeeds mock will not exercise the requirements above. public interface 


#code

ILlmClient {     Task<LlmResponse> SummarizeAsync(string prompt, CancellationToken ct); }   public class MockLlmClient : ILlmClient {     private readonly Random _random = new();       public async Task<LlmResponse> SummarizeAsync(string prompt, CancellationToken ct)     {         // Simulate network + model latency (honor cancellation!)         var delay = TimeSpan.FromMilliseconds(_random.Next(1500, 3500));         await Task.Delay(delay, ct);           // ~15% transient failures: timeouts and rate limits         var roll = _random.NextDouble();         if (roll < 0.08) throw new TimeoutException("Mock LLM timeout");         if (roll < 0.15) throw new RateLimitException("Mock 429: slow down");           var inputTokens = prompt.Length / 4;         return new LlmResponse(             Summary: $"Mock summary of {prompt.Length} chars.",             InputTokens: inputTokens,             OutputTokens: 50,             Model: "mock-gpt-4");     } }   public record LlmResponse(     string Summary, int InputTokens, int OutputTokens, string Model);   public class RateLimitException : Exception {     public RateLimitException(string message) : base(message) { } } 


#endcode
Cost guidance for logging: assume $0.005 per 1K input tokens and $0.015 per 1K output tokens (or document your own assumption in the README). 