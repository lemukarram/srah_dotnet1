using SarhSummarizer.Models;

namespace SarhSummarizer.Workers;

public interface IJobQueue
{
    void Enqueue(Job job);
    ValueTask<Job> DequeueAsync(CancellationToken cancellationToken);
}