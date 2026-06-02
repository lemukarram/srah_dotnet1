using SarhSummarizer.Services;

namespace SarhSummarizer.Workers;

public class SummarizationWorker : BackgroundService
{
    private readonly IJobQueue _jobQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SummarizationWorker> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public SummarizationWorker(IJobQueue jobQueue, IServiceProvider serviceProvider, IConfiguration configuration, ILogger<SummarizationWorker> logger)
    {
        _jobQueue = jobQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        int maxConcurrency = configuration.GetValue<int>("Worker:MaxConcurrency", 3);
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SummarizationWorker is starting with MaxConcurrency {Concurrency}.", _concurrencySemaphore.CurrentCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _concurrencySemaphore.WaitAsync(stoppingToken);

            try
            {
                var job = await _jobQueue.DequeueAsync(stoppingToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var llmService = scope.ServiceProvider.GetRequiredService<ILlmService>();
                        
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, job.Cts.Token);
                        
                        await llmService.ProcessJobAsync(job, linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing job {JobId}", job.Id);
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _concurrencySemaphore.Release();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling job from queue.");
                _concurrencySemaphore.Release();
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}