using SarhSummarizer.Models;

namespace SarhSummarizer.Services;

public interface IJobManager
{
    Job CreateJob(SubmitJobRequest request, string? idempotencyKey);
    Job? GetJob(string jobId);
    bool CancelJob(string jobId);
    IEnumerable<Job> GetJobs(string? status, int limit, int offset);
}