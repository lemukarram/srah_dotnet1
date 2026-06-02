using System.Collections.Concurrent;
using SarhSummarizer.Models;

namespace SarhSummarizer.Workers;

public class JobQueue : IJobQueue
{
    private readonly ConcurrentQueue<Job> _highPriorityQueue = new();
    private readonly ConcurrentQueue<Job> _normalPriorityQueue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(Job job)
    {
        if (job.Priority == "high")
        {
            _highPriorityQueue.Enqueue(job);
        }
        else
        {
            _normalPriorityQueue.Enqueue(job);
        }
        _signal.Release();
    }

    public async ValueTask<Job> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        
        if (_highPriorityQueue.TryDequeue(out var highJob))
        {
            return highJob;
        }

        if (_normalPriorityQueue.TryDequeue(out var normalJob))
        {
            return normalJob;
        }
        
        throw new InvalidOperationException("No jobs available despite signal.");
    }
}