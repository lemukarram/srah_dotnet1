using System.Collections.Concurrent;
using SarhSummarizer.Models;
using SarhSummarizer.Workers;

namespace SarhSummarizer.Services;

public class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();
    private readonly ConcurrentDictionary<string, string> _idempotencyKeys = new();
    private readonly IJobQueue _jobQueue;

    public JobManager(IJobQueue jobQueue)
    {
        _jobQueue = jobQueue;
    }

    public Job CreateJob(SubmitJobRequest request, string? idempotencyKey)
    {
        if (!string.IsNullOrEmpty(idempotencyKey) && _idempotencyKeys.TryGetValue(idempotencyKey, out var existingJobId))
        {
            if (_jobs.TryGetValue(existingJobId, out var existingJob))
            {
                return existingJob;
            }
        }

        var job = new Job
        {
            Text = request.Text,
            Priority = request.Priority?.ToLower() == "high" ? "high" : "normal",
            IdempotencyKey = idempotencyKey
        };

        _jobs[job.Id] = job;

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            _idempotencyKeys[idempotencyKey] = job.Id;
        }

        _jobQueue.Enqueue(job);

        return job;
    }

    public Job? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public bool CancelJob(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            // If it's already completed or failed, we can't cancel it, but returning true is fine if requested (or return false if we strictly just modify status)
            if (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
            {
                job.Status = JobStatus.Cancelled;
                job.Cts.Cancel();
                return true;
            }
        }
        return false;
    }

    public IEnumerable<Job> GetJobs(string? status, int limit, int offset)
    {
        var query = _jobs.Values.AsEnumerable();
        
        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<JobStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(j => j.Status == parsedStatus);
            }
        }

        return query.OrderByDescending(j => j.CreatedAt)
                    .Skip(offset)
                    .Take(limit);
    }
}