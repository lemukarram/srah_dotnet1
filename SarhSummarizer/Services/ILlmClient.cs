using SarhSummarizer.Models;

namespace SarhSummarizer.Services;

public interface ILlmClient
{
    Task<LlmResponse> SummarizeAsync(string text, CancellationToken cancellationToken = default);
}
