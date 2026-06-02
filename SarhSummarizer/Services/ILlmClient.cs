using System;
using System.Threading;
using System.Threading.Tasks;

namespace SarhSummarizer.Services;

public interface ILlmClient
{
    Task<LlmResponse> SummarizeAsync(string prompt, CancellationToken ct);
}

public record LlmResponse(string Summary, int InputTokens, int OutputTokens, string Model);
