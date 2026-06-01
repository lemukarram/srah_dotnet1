using System.Threading.Channels;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using SarhSummarizer.Models;
using SarhSummarizer.Services;

namespace SarhSummarizer.Workers;

public class SummarizationWorker : BackgroundService
{
    private readonly Channel<string> _queue;
    private readonly IJobStore _jobStore;
    private readonly ILlmClient _llmClient;
    private readonly TextChunker _chunker;
    private readonly ILogger<SummarizationWorker> _logger;
    private readonly ResiliencePipeline<LlmResponse> _pipeline;

    public SummarizationWorker(
        Channel<string> queue,
        IJobStore jobStore,
        ILlmClient llmClient,
        TextChunker chunker,
        ILogger<SummarizationWorker> logger)
    {
        _queue = queue;
        _jobStore = jobStore;
        _llmClient = llmClient;
        _chunker = chunker;
        _logger = logger;

        // Rule 4: Polly Resilience Pipeline
        _pipeline = new ResiliencePipelineBuilder<LlmResponse>()
            .AddRetry(new RetryStrategyOptions<LlmResponse>
            {
                ShouldHandle = new PredicateBuilder<LlmResponse>()
                    .Handle<TimeoutException>()
                    .Handle<HttpRequestException>(),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                OnRetry = args =>
                {
                    _logger.LogWarning("Retry attempt {AttemptNumber} due to: {Exception}", args.AttemptNumber, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SummarizationWorker is starting.");

        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error processing job {JobId}", jobId);
            }
        }

        _logger.LogInformation("SummarizationWorker is stopping.");
    }

    private async Task ProcessJobAsync(string jobId, CancellationToken ct)
    {
        var job = _jobStore.GetJob(jobId);
        if (job == null) return;

        _logger.LogInformation("Processing job {JobId}", jobId);
        job.Status = JobStatus.Processing;
        _jobStore.UpdateJob(job);

        try
        {
            var chunks = _chunker.ChunkText(job.Text);
            _logger.LogInformation("Job {JobId} | Chunks created: {Count}", jobId, chunks.Count);

            var summaries = new List<string>();
            int totalInputTokens = 0;
            int totalOutputTokens = 0;
            decimal totalCost = 0;

            foreach (var chunk in chunks)
            {
                var response = await _pipeline.ExecuteAsync(
                    async token => await _llmClient.SummarizeAsync(chunk, token), ct);

                summaries.Add(response.Content);
                totalInputTokens += response.InputTokens;
                totalOutputTokens += response.OutputTokens;

                // Rule 5: Cost formula: (InputTokens / 1000 * 0.01) + (OutputTokens / 1000 * 0.03)
                decimal chunkCost = (decimal)((response.InputTokens / 1000.0 * 0.01) + (response.OutputTokens / 1000.0 * 0.03));
                totalCost += chunkCost;

                _logger.LogInformation("Job {JobId} | Model: {Model} | InputTokens: {Input} | OutputTokens: {Output} | CostUSD: {Cost:F4}",
                    jobId, response.Model, response.InputTokens, response.OutputTokens, chunkCost);
            }

            string finalSummary;
            if (summaries.Count > 1)
            {
                _logger.LogInformation("Combining {Count} chunks for job {JobId}", summaries.Count, jobId);
                var combinedText = string.Join("\n\n", summaries);
                var finalResponse = await _pipeline.ExecuteAsync(
                    async token => await _llmClient.SummarizeAsync(combinedText, token), ct);

                finalSummary = finalResponse.Content;
                totalInputTokens += finalResponse.InputTokens;
                totalOutputTokens += finalResponse.OutputTokens;

                decimal finalCost = (decimal)((finalResponse.InputTokens / 1000.0 * 0.01) + (finalResponse.OutputTokens / 1000.0 * 0.03));
                totalCost += finalCost;

                _logger.LogInformation("Job {JobId} (Final) | Model: {Model} | InputTokens: {Input} | OutputTokens: {Output} | CostUSD: {Cost:F4}",
                    jobId, finalResponse.Model, finalResponse.InputTokens, finalResponse.OutputTokens, finalCost);
            }
            else
            {
                finalSummary = summaries[0];
            }

            job.Status = JobStatus.Completed;
            job.Summary = finalSummary;
            job.Tokens = totalInputTokens + totalOutputTokens;
            job.CostUsd = totalCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", jobId);
            job.Status = JobStatus.Failed;
            job.ErrorReason = ex.Message;
        }

        _jobStore.UpdateJob(job);
    }
}
