# PLAN.md — Execution Plan (85 Minutes Total)

## Phase 0 — Scaffold (5 min) ⏱ 0:00–0:05

```bash
dotnet new webapi -minimal -n SarhSummarizer
cd SarhSummarizer
dotnet add package Microsoft.Extensions.Http.Polly
dotnet add package Polly.Extensions.Http
# Verify it builds
dotnet build
```

Create the folder structure:
```bash
mkdir Models Services Workers
```

---

## Phase 1 — Models & Interfaces (10 min) ⏱ 0:05–0:15

### 1a. `Models/SummarizationJob.cs`
```csharp
public enum JobStatus { Pending, Processing, Completed, Failed }

public class SummarizationJob
{
    public string JobId { get; init; } = Guid.NewGuid().ToString();
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string InputText { get; init; } = string.Empty;
    public string? Summary { get; set; }
    public string? ErrorReason { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
```

### 1b. `Models/LlmResponse.cs`
```csharp
public record LlmResponse(
    string Summary,
    int InputTokens,
    int OutputTokens,
    string Model);
```

### 1c. `Models/ApiModels.cs`
```csharp
public record SummarizeRequest(string? Text);

public record SummarizeStartedResponse(string JobId, string Status);

public record JobStatusResponse(
    string JobId,
    string Status,
    string? Summary = null,
    int? Tokens = null,
    decimal? CostUsd = null,
    string? ErrorReason = null);
```

### 1d. `Services/ILlmClient.cs`
```csharp
public interface ILlmClient
{
    Task<LlmResponse> SummarizeAsync(string prompt, CancellationToken ct);
}
```

### 1e. `Services/IJobStore.cs`
```csharp
public interface IJobStore
{
    void Add(SummarizationJob job);
    SummarizationJob? Get(string jobId);
    void Update(SummarizationJob job);
}
```

---

## Phase 2 — Core Services (20 min) ⏱ 0:15–0:35

### 2a. `Services/MockLlmClient.cs`
Copy EXACTLY from the brief, then add:
- ILogger injection for logging delays
- Do NOT modify the failure rate (10% is intentional for Polly demo)

```csharp
public class MockLlmClient : ILlmClient
{
    private readonly Random _random = new();
    private readonly ILogger<MockLlmClient> _logger;

    public MockLlmClient(ILogger<MockLlmClient> logger) => _logger = logger;

    public async Task<LlmResponse> SummarizeAsync(string prompt, CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(_random.Next(2000, 3000));
        _logger.LogDebug("MockLlmClient: simulating {Delay}ms latency", delay.TotalMilliseconds);
        await Task.Delay(delay, ct);

        if (_random.NextDouble() < 0.1)
            throw new TimeoutException("Mock LLM timeout");

        return new LlmResponse(
            Summary: $"Mock summary of {prompt.Length} chars.",
            InputTokens: prompt.Length / 4,
            OutputTokens: 50,
            Model: "mock-gpt-4");
    }
}
```

### 2b. `Services/InMemoryJobStore.cs`
```csharp
public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, SummarizationJob> _jobs = new();

    public void Add(SummarizationJob job) => _jobs[job.JobId] = job;
    public SummarizationJob? Get(string jobId) => _jobs.GetValueOrDefault(jobId);
    public void Update(SummarizationJob job) => _jobs[job.JobId] = job;
}
```

### 2c. `Services/TextChunker.cs` ← MOST IMPORTANT FOR EVALUATION
```csharp
public static class TextChunker
{
    private const int MaxWordsPerChunk = 3000;

    public static List<string> Chunk(string text)
    {
        var wordCount = CountWords(text);
        if (wordCount <= MaxWordsPerChunk)
            return new List<string> { text };

        // Split on paragraph boundaries first
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new StringBuilder();
        var currentWordCount = 0;

        foreach (var para in paragraphs)
        {
            var paraWords = CountWords(para);
            if (currentWordCount + paraWords > MaxWordsPerChunk && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
                currentWordCount = 0;
            }
            currentChunk.AppendLine(para);
            currentWordCount += paraWords;
        }

        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString().Trim());

        return chunks;
    }

    public static int CountWords(string text) =>
        text.Split(new[] { ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries).Length;
}
```

---

## Phase 3 — Background Worker (20 min) ⏱ 0:35–0:55

### `Workers/SummarizationWorker.cs`

This is the heart of the system. Key responsibilities:
1. Read jobId from Channel
2. Get job from store, set status = Processing
3. Chunk the input text
4. Call LLM for each chunk (with Polly retry)
5. If multiple chunks: call LLM again to combine summaries
6. Accumulate tokens, calculate cost
7. Set status = Completed or Failed
8. NEVER let an exception escape — catch everything

```csharp
public class SummarizationWorker : BackgroundService
{
    private readonly Channel<string> _channel;
    private readonly IJobStore _jobStore;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<SummarizationWorker> _logger;

    // Polly pipeline: retry 3x with exponential backoff
    private readonly ResiliencePipeline _pipeline;

    public SummarizationWorker(
        Channel<string> channel,
        IJobStore jobStore,
        ILlmClient llmClient,
        ILogger<SummarizationWorker> logger)
    {
        _channel = channel;
        _jobStore = jobStore;
        _llmClient = llmClient;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning("LLM retry attempt {Attempt} after {Delay}",
                        args.AttemptNumber + 1, args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SummarizationWorker started");

        await foreach (var jobId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessJobAsync(jobId, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(string jobId, CancellationToken ct)
    {
        var job = _jobStore.Get(jobId);
        if (job is null) return;

        job.Status = JobStatus.Processing;
        _jobStore.Update(job);
        _logger.LogInformation("Processing job {JobId}", jobId);

        try
        {
            var chunks = TextChunker.Chunk(job.InputText);
            _logger.LogInformation("Job {JobId}: {ChunkCount} chunk(s) for {WordCount} words",
                jobId, chunks.Count, TextChunker.CountWords(job.InputText));

            var chunkSummaries = new List<string>();

            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                var prompt = chunks.Count > 1
                    ? $"Summarize this section ({index + 1} of {chunks.Count}):\n\n{chunk}"
                    : $"Summarize the following document:\n\n{chunk}";

                var response = await _pipeline.ExecuteAsync(
                    async ct2 => await _llmClient.SummarizeAsync(prompt, ct2), ct);

                chunkSummaries.Add(response.Summary);
                job.TotalInputTokens += response.InputTokens;
                job.TotalOutputTokens += response.OutputTokens;
                job.Model = response.Model;

                LogTokenUsage(jobId, index + 1, response);
            }

            // If multiple chunks, do a final consolidation pass
            string finalSummary;
            if (chunkSummaries.Count > 1)
            {
                var consolidationPrompt = $"Combine these section summaries into one coherent summary:\n\n"
                    + string.Join("\n\n---\n\n", chunkSummaries);

                var finalResponse = await _pipeline.ExecuteAsync(
                    async ct2 => await _llmClient.SummarizeAsync(consolidationPrompt, ct2), ct);

                finalSummary = finalResponse.Summary;
                job.TotalInputTokens += finalResponse.InputTokens;
                job.TotalOutputTokens += finalResponse.OutputTokens;
                LogTokenUsage(jobId, "consolidation", finalResponse);
            }
            else
            {
                finalSummary = chunkSummaries[0];
            }

            job.Summary = finalSummary;
            job.CostUsd = CalculateCost(job.TotalInputTokens, job.TotalOutputTokens);
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Job {JobId} completed | Tokens: {Total} | Cost: ${Cost:F4}",
                jobId, job.TotalInputTokens + job.TotalOutputTokens, job.CostUsd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed: {Message}", jobId, ex.Message);
            job.Status = JobStatus.Failed;
            job.ErrorReason = ex.Message;
        }
        finally
        {
            _jobStore.Update(job);
        }
    }

    private void LogTokenUsage(string jobId, object chunkLabel, LlmResponse r)
    {
        var cost = CalculateCost(r.InputTokens, r.OutputTokens);
        _logger.LogInformation(
            "Job {JobId} chunk [{Chunk}] | Model: {Model} | InputTokens: {Input} | OutputTokens: {Output} | ChunkCost: ${Cost:F4}",
            jobId, chunkLabel, r.Model, r.InputTokens, r.OutputTokens, cost);
    }

    private static decimal CalculateCost(int inputTokens, int outputTokens) =>
        (inputTokens / 1000m * 0.01m) + (outputTokens / 1000m * 0.03m);
}
```

---

## Phase 4 — Program.cs & Endpoints (15 min) ⏱ 0:55–1:10

```csharp
var builder = WebApplication.CreateBuilder(args);

// DI Registration
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<ILlmClient, MockLlmClient>();
builder.Services.AddSingleton(Channel.CreateBounded<string>(
    new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait }));
builder.Services.AddHostedService<SummarizationWorker>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// POST /summarize
app.MapPost("/summarize", async (
    SummarizeRequest request,
    IJobStore jobStore,
    Channel<string> channel,
    ILogger<Program> logger) =>
{
    // Validation
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = "Text is required" });

    var wordCount = TextChunker.CountWords(request.Text);
    if (wordCount > 10_000)
        return Results.BadRequest(new { error = $"Document exceeds 10,000 word limit ({wordCount} words received)" });

    var job = new SummarizationJob { InputText = request.Text };
    jobStore.Add(job);

    await channel.Writer.WriteAsync(job.JobId);
    logger.LogInformation("Job {JobId} queued ({WordCount} words)", job.JobId, wordCount);

    return Results.Accepted($"/summarize/{job.JobId}",
        new SummarizeStartedResponse(job.JobId, job.Status.ToString()));
});

// GET /summarize/{jobId}
app.MapGet("/summarize/{jobId}", (string jobId, IJobStore jobStore) =>
{
    var job = jobStore.Get(jobId);
    if (job is null)
        return Results.NotFound(new { error = $"Job '{jobId}' not found" });

    var totalTokens = job.TotalInputTokens + job.TotalOutputTokens;

    return Results.Ok(new JobStatusResponse(
        JobId: job.JobId,
        Status: job.Status.ToString(),
        Summary: job.Status == JobStatus.Completed ? job.Summary : null,
        Tokens: job.Status == JobStatus.Completed ? totalTokens : null,
        CostUsd: job.Status == JobStatus.Completed ? job.CostUsd : null,
        ErrorReason: job.Status == JobStatus.Failed ? job.ErrorReason : null));
});

app.Run();
```

---

## Phase 5 — README.md (10 min) ⏱ 1:10–1:20

Write these sections:

### Setup & Run
```bash
cd SarhSummarizer
dotnet run
# API available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### Design Choices (what evaluators want to read)
1. **Minimal API** — chosen over controllers for reduced boilerplate in a small service
2. **System.Threading.Channels** — lock-free, high-performance in-process queue. No Redis/Hangfire needed for this scope
3. **BackgroundService** — .NET's built-in hosted service pattern. Processes jobs off the HTTP thread
4. **ConcurrentDictionary** — thread-safe in-memory store. Noted limitation: lost on restart
5. **Polly ResiliencePipeline** — retry (3x exponential backoff) + timeout (30s) on every LLM call
6. **Map-reduce chunking** — chunk → summarize each → consolidate. Handles documents well beyond context limits
7. **Structured logging** — every LLM call logs model, tokens, cost. Ready for log aggregation (Seq, ELK)

### What I Would Add With More Time
- Persistent storage (PostgreSQL + EF Core) so jobs survive restarts
- Redis-backed Channel for multi-instance deployments
- Unit tests for TextChunker, cost calculation, worker logic
- Cancellation support (DELETE /summarize/{jobId})
- Rate limiting middleware (AspNetCoreRateLimit)
- Health check endpoint (/health)
- Real LLM integration (Azure OpenAI or OpenAI SDK)
- Metrics endpoint (Prometheus/OpenTelemetry)

---

## Phase 6 — Test & Buffer (5 min) ⏱ 1:20–1:25

Run these curl commands and confirm all work:

```bash
# 1. Valid short document
curl -X POST http://localhost:5000/summarize \
  -H "Content-Type: application/json" \
  -d '{"text": "This is a test document about procurement systems."}'

# 2. Poll result (use jobId from above)
curl http://localhost:5000/summarize/{jobId}

# 3. Empty text validation
curl -X POST http://localhost:5000/summarize \
  -H "Content-Type: application/json" \
  -d '{"text": ""}'
# Expect: 400

# 4. Missing field
curl -X POST http://localhost:5000/summarize \
  -H "Content-Type: application/json" \
  -d '{}'
# Expect: 400

# 5. Long document test (paste the 12,000-word sample they provide)
# Watch logs for: "X chunk(s)" and per-chunk token logging
```

## Final 5 min — Git commit

```bash
git init
git add .
git commit -m "feat: async summarization service with chunking, retries, and cost logging"
```

---

## SCORING RUBRIC SELF-CHECK

Before submitting, verify each evaluator requirement:

| Requirement | Implementation | Location |
|-------------|---------------|----------|
| Non-blocking POST response | Channel.Writer.WriteAsync + immediate return | Program.cs |
| Chunking for long docs | TextChunker + map-reduce in worker | TextChunker.cs + Worker |
| API failure handling | Polly retry 3x + exception catch | Worker + Polly pipeline |
| Token & cost logging | ILogger structured log per LLM call | SummarizationWorker.cs |
| Input validation | Word count + null/empty check | Program.cs |
| Job status (Pending/Processing/Completed/Failed) | JobStatus enum | SummarizationJob.cs |
| README with design choices | Done | README.md |
