using Microsoft.AspNetCore.Mvc;
using SarhSummarizer.Models;
using SarhSummarizer.Services;
using SarhSummarizer.Workers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure global JSON options for serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Register App Services
builder.Services.AddSingleton<IJobQueue, JobQueue>();
builder.Services.AddSingleton<IJobManager, JobManager>();

// Register LLM Services
builder.Services.AddSingleton<ILlmClient, MockLlmClient>();
builder.Services.AddTransient<ILlmService, LlmService>();

// Register Background Worker
builder.Services.AddHostedService<SummarizationWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map Endpoints

app.MapPost("/jobs", async (
    HttpContext httpContext, 
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, 
    IJobManager jobManager) =>
{
    SubmitJobRequest? request = null;
    string contentType = httpContext.Request.ContentType ?? "";

    if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            // .NET 8's System.Text.Json is strict about unescaped control characters in strings.
            // To handle literal newlines, we read the body and manually escape them before deserialization.
            using var reader = new StreamReader(httpContext.Request.Body);
            var rawBody = await reader.ReadToEndAsync();
            
            // This is a simple heuristic: replace literal newlines with escaped versions.
            var sanitizedJson = rawBody.Replace("\n", "\\n").Replace("\r", "\\r");
            
            var serializerOptions = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            request = JsonSerializer.Deserialize<SubmitJobRequest>(sanitizedJson, serializerOptions);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { error = "Invalid JSON format: " + ex.Message });
        }
        catch (BadHttpRequestException ex)
        {
            return Results.BadRequest(new { error = "Invalid request: " + ex.Message });
        }
    }
    else if (contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
    {
        using var reader = new StreamReader(httpContext.Request.Body);
        var text = await reader.ReadToEndAsync();
        request = new SubmitJobRequest { Text = text, Priority = "normal" };
    }
    else
    {
        return Results.Json(new { error = "Content type must be application/json or text/plain." }, statusCode: 415);
    }

    if (request == null)
    {
        return Results.BadRequest(new { error = "Request body could not be parsed." });
    }
    
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "Text cannot be empty." });
    }
    
    int wordCount = request.Text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    if (wordCount > 50000)
    {
        return Results.BadRequest(new { error = "Text exceeds maximum allowed word count of 50,000." });
    }

    if (!string.IsNullOrEmpty(request.Priority) && 
        !request.Priority.Equals("high", StringComparison.OrdinalIgnoreCase) && 
        !request.Priority.Equals("normal", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Priority must be 'high' or 'normal'." });
    }

    var job = jobManager.CreateJob(request, idempotencyKey);
    return Results.Accepted($"/jobs/{job.Id}", new { jobId = job.Id, status = job.Status.ToString() });
});

app.MapGet("/jobs/{jobId}", (string jobId, IJobManager jobManager) =>
{
    var job = jobManager.GetJob(jobId);
    if (job == null)
    {
        return Results.NotFound(new { error = "Job not found." });
    }

    var response = new JobResponse
    {
        Id = job.Id,
        Status = job.Status.ToString(),
        Summary = job.Summary,
        Tokens = job.Status == JobStatus.Completed ? job.Tokens : null,
        CostUsd = job.Status == JobStatus.Completed ? job.CostUsd : null,
        Progress = new JobProgress { ChunksDone = job.ChunksDone, ChunksTotal = job.ChunksTotal },
        Error = job.Error
    };

    return Results.Ok(response);
});

app.MapDelete("/jobs/{jobId}", (string jobId, IJobManager jobManager) =>
{
    var job = jobManager.GetJob(jobId);
    if (job == null)
    {
        return Results.NotFound(new { error = "Job not found." });
    }

    if (jobManager.CancelJob(jobId))
    {
        return Results.Ok(new { status = JobStatus.Cancelled.ToString() });
    }

    return Results.Ok(new { status = job.Status.ToString() });
});

app.MapGet("/jobs", ([FromQuery] string? status, IJobManager jobManager, [FromQuery] int limit = 10, [FromQuery] int offset = 0) =>
{
    var jobs = jobManager.GetJobs(status, limit, offset);
    
    var response = jobs.Select(job => new JobResponse
    {
        Id = job.Id,
        Status = job.Status.ToString(),
        Summary = job.Summary,
        Tokens = job.Status == JobStatus.Completed ? job.Tokens : null,
        CostUsd = job.Status == JobStatus.Completed ? job.CostUsd : null,
        Progress = new JobProgress { ChunksDone = job.ChunksDone, ChunksTotal = job.ChunksTotal },
        Error = job.Error
    });

    return Results.Ok(response);
});

app.Run();
