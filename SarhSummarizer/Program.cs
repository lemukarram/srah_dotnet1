using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using SarhSummarizer.Models;
using SarhSummarizer.Services;
using SarhSummarizer.Workers;

var builder = WebApplication.CreateBuilder(args);

// --- Dependency Injection Registration ---

// Job storage (In-memory)
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();

// Text chunking utility
builder.Services.AddSingleton<TextChunker>();

// LLM Client (Mock)
builder.Services.AddSingleton<ILlmClient, MockLlmClient>();

// Bounded Channel for background processing (Capacity 100 as per brief)
builder.Services.AddSingleton(Channel.CreateBounded<string>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait
}));

// Background Worker
builder.Services.AddHostedService<SummarizationWorker>();

// Add Swagger/OpenAPI for testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- API Endpoints ---

// POST /summarize
app.MapPost("/summarize", async (
    [FromBody] SummarizeRequest request,
    [FromServices] IJobStore jobStore,
    [FromServices] Channel<string> queue) =>
{
    // Rule 6: Input Validation
    if (string.IsNullOrWhiteSpace(request?.Text))
    {
        return Results.BadRequest(new ErrorResponse("Text is required"));
    }

    var wordCount = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    if (wordCount > 10000)
    {
        return Results.BadRequest(new ErrorResponse("Document exceeds 10,000 word limit"));
    }

    // Create job
    var job = new SummarizationJob { Text = request.Text };
    jobStore.AddJob(job);

    // Enqueue for background processing
    // Rule 1: Return immediately
    await queue.Writer.WriteAsync(job.JobId);

    return Results.Accepted($"/summarize/{job.JobId}", new SummarizeResponse(job.JobId, job.Status.ToString()));
})
.WithName("PostSummarize")
.WithOpenApi();

// GET /summarize/{jobId}
app.MapGet("/summarize/{jobId}", (
    string jobId,
    [FromServices] IJobStore jobStore) =>
{
    var job = jobStore.GetJob(jobId);

    // Rule 6: JobId not found
    if (job == null)
    {
        return Results.NotFound(new ErrorResponse("Job not found"));
    }

    // Return status/results based on job state
    return Results.Ok(new JobStatusResponse(
        job.JobId,
        job.Status.ToString(),
        job.Summary,
        job.Tokens,
        job.CostUsd,
        job.ErrorReason
    ));
})
.WithName("GetSummarizeStatus")
.WithOpenApi();

app.Run();
