using SarhSummarizer.Models;

namespace SarhSummarizer.Services;

public interface IJobStore
{
    void AddJob(SummarizationJob job);
    SummarizationJob? GetJob(string jobId);
    void UpdateJob(SummarizationJob job);
}
