using Polly;
using Polly.Retry;
using SarhSummarizer.Models;
using System.Text;

namespace SarhSummarizer.Services;

public class LlmService : ILlmService
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<LlmService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const int ChunkSize = 2000;

    public LlmService(ILlmClient llmClient, ILogger<LlmService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<TimeoutException>()
            .Or<RateLimitException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                _logger.LogWarning("Retry {RetryCount} after {Delay}s due to {Message}", retryCount, timeSpan.TotalSeconds, exception.Message);
            });
    }

    public async Task ProcessJobAsync(Job job, CancellationToken cancellationToken)
    {
        try
        {
            job.Status = JobStatus.Processing;
            _logger.LogInformation("Job {JobId} started processing.", job.Id);

            var chunks = ChunkText(job.Text, ChunkSize);
            job.ChunksTotal = chunks.Count;
            job.ChunksDone = 0;

            var summaries = new List<string>();
            int totalInputTokens = 0;
            int totalOutputTokens = 0;
            decimal totalCost = 0m;

            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    return await _llmClient.SummarizeAsync(chunk, ct);
                }, cancellationToken);

                summaries.Add(result.Summary);
                totalInputTokens += result.InputTokens;
                totalOutputTokens += result.OutputTokens;
                
                decimal chunkCost = (result.InputTokens / 1000m) * 0.005m + (result.OutputTokens / 1000m) * 0.015m;
                totalCost += chunkCost;
                
                _logger.LogInformation("Job {JobId} processed chunk {ChunkIndex}/{TotalChunks}. InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, ChunkCost: {ChunkCost:C4}", 
                    job.Id, job.ChunksDone + 1, chunks.Count, result.InputTokens, result.OutputTokens, chunkCost);

                job.ChunksDone++;
            }

            string finalSummary;
            if (summaries.Count > 1)
            {
                var combinedText = string.Join("\n\n", summaries);
                var finalResult = await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    return await _llmClient.SummarizeAsync(combinedText, ct);
                }, cancellationToken);
                
                finalSummary = finalResult.Summary;
                totalInputTokens += finalResult.InputTokens;
                totalOutputTokens += finalResult.OutputTokens;
                
                decimal finalChunkCost = (finalResult.InputTokens / 1000m) * 0.005m + (finalResult.OutputTokens / 1000m) * 0.015m;
                totalCost += finalChunkCost;
                
                _logger.LogInformation("Job {JobId} processed final merge chunk. InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, ChunkCost: {ChunkCost:C4}", 
                    job.Id, finalResult.InputTokens, finalResult.OutputTokens, finalChunkCost);
            }
            else
            {
                finalSummary = summaries.FirstOrDefault() ?? "Empty";
            }

            job.Summary = finalSummary;
            job.Tokens = totalInputTokens + totalOutputTokens;
            job.CostUsd = totalCost;
            job.Status = JobStatus.Completed;

            _logger.LogInformation("Job {JobId} completed. TotalTokens: {TotalTokens}, TotalCost: {TotalCost:C4}", job.Id, job.Tokens, job.CostUsd);
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            _logger.LogInformation("Job {JobId} was cancelled.", job.Id);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            _logger.LogError(ex, "Job {JobId} failed.", job.Id);
        }
    }

    private List<string> ChunkText(string text, int maxChunkSize)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text))
            return chunks;
            
        for (int i = 0; i < text.Length; i += maxChunkSize)
        {
            chunks.Add(text.Substring(i, Math.Min(maxChunkSize, text.Length - i)));
        }
        return chunks;
    }
}