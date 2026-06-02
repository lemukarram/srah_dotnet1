using SarhSummarizer.Models;

namespace SarhSummarizer.Services;

public interface ILlmService
{
    Task ProcessJobAsync(Job job, CancellationToken cancellationToken);
}