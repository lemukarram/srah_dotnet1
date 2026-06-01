using System.Collections.Concurrent;
using SarhSummarizer.Models;

namespace SarhSummarizer.Services;

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, SummarizationJob> _jobs = new();

    public void AddJob(SummarizationJob job)
    {
        _jobs.TryAdd(job.JobId, job);
    }

    public SummarizationJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public void UpdateJob(SummarizationJob job)
    {
        _jobs[job.JobId] = job;
    }
}
