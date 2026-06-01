# GEMINI.md — Master Instructions for Sarh Backend Assessment

You are a senior .NET backend engineer completing a timed technical assessment.
Read every instruction in this file before writing a single line of code.

---

## YOUR IDENTITY & GOAL

You are building a production-quality .NET 8 ASP.NET Core Web API that:
1. Accepts long text documents for summarization
2. Processes them asynchronously via a background job queue
3. Returns job status/results via polling endpoint
4. Handles chunking, retries, validation, and cost logging

The evaluators are looking for: **async patterns, error handling, clean architecture, observability**.
They are NOT looking for: perfect AI integration, fancy prompts, UI.

---

## TECH STACK — USE EXACTLY THESE

- **Framework**: .NET 8, ASP.NET Core Minimal API (faster to scaffold, cleaner for small services)
- **Job Queue**: `System.Threading.Channels` — in-memory, no external deps needed
- **Background Worker**: `BackgroundService` hosted service
- **Retry/Resilience**: `Polly` v8 (`Microsoft.Extensions.Http.Polly`)
- **Logging**: Built-in `ILogger<T>` with structured logging (no extra packages needed)
- **Validation**: Data Annotations + manual guard clauses (no FluentValidation needed for scope)
- **Storage**: `ConcurrentDictionary<string, SummarizationJob>` — in-memory is fine, mention DB in README
- **LLM Client**: MockLlmClient (Option A) — do NOT waste time on real API key setup

---

## PROJECT STRUCTURE — CREATE EXACTLY THIS

```
SarhSummarizer/
├── SarhSummarizer.csproj
├── Program.cs                  # Minimal API setup, DI registration, endpoint mapping
├── Models/
│   ├── SummarizationJob.cs     # Job entity with status enum
│   ├── LlmResponse.cs          # LLM response record
│   └── ApiModels.cs            # Request/Response DTOs
├── Services/
│   ├── ILlmClient.cs           # Interface
│   ├── MockLlmClient.cs        # Mock implementation (as per brief)
│   ├── IJobStore.cs            # Interface for job storage
│   ├── InMemoryJobStore.cs     # ConcurrentDictionary implementation
│   └── TextChunker.cs          # Chunking logic for >context window docs
├── Workers/
│   └── SummarizationWorker.cs  # BackgroundService that processes the Channel queue
└── README.md
```

---

## IMPLEMENTATION RULES — FOLLOW STRICTLY

### Rule 1: Async-First
- POST /summarize MUST return immediately (< 5ms) with jobId
- NEVER await the LLM call in the endpoint handler
- Enqueue to Channel, return jobId, done

### Rule 2: Job Status Flow
```
Pending → Processing → Completed
                    ↘ Failed (with ErrorReason)
```

### Rule 3: Chunking Logic (CRITICAL — evaluators will test with 12,000 word doc)
- Max chunk size: 3,000 words (~4,000 tokens, safe for most models)
- Split on paragraph boundaries (`\n\n`) first, then sentence boundaries
- If doc fits in one chunk → single LLM call
- If doc exceeds limit → chunk → summarize each → combine summaries → final summarize
- Log how many chunks were created

### Rule 4: Resilience with Polly
```csharp
// Apply to ILlmClient calls:
// - Retry: 3 attempts, exponential backoff (1s, 2s, 4s)
// - Timeout: 30 seconds per attempt
// - The Mock already throws TimeoutException 10% of the time — handle it
```

### Rule 5: Token & Cost Logging (evaluators will check logs)
```
// Log at Information level after every LLM call:
// "Job {jobId} | Model: {model} | InputTokens: {input} | OutputTokens: {output} | CostUSD: {cost:F4}"
// Cost formula: (InputTokens / 1000 * 0.01) + (OutputTokens / 1000 * 0.03)
// Use mock-gpt-4 pricing above — mention real pricing in README
```

### Rule 6: Input Validation
- Empty/null text → 400 Bad Request, message: "Text is required"
- Text > 10,000 words → 400 Bad Request, message: "Document exceeds 10,000 word limit"
- Non-string body / missing field → 400 Bad Request
- JobId not found → 404 Not Found

### Rule 7: Error Handling
- All exceptions in BackgroundService MUST be caught — an unhandled exception kills the worker
- Failed jobs store the exception message in `ErrorReason`
- Worker continues processing after a job failure

---

## EXACT API CONTRACT

### POST /summarize
```json
// Request
{ "text": "document content here..." }

// Response 202 Accepted
{ "jobId": "uuid-here", "status": "Pending" }

// Response 400
{ "error": "Text is required" }
```

### GET /summarize/{jobId}
```json
// Pending/Processing
{ "jobId": "abc", "status": "Pending" }

// Completed
{
  "jobId": "abc",
  "status": "Completed",
  "summary": "...",
  "tokens": 1284,
  "costUsd": 0.0123
}

// Failed
{
  "jobId": "abc",
  "status": "Failed",
  "errorReason": "LLM timeout after 3 retries"
}
```

---

## CODE TO WRITE — IN THIS ORDER

1. `SarhSummarizer.csproj` — add Polly NuGet package
2. `Models/SummarizationJob.cs` — job entity
3. `Models/ApiModels.cs` — DTOs
4. `Models/LlmResponse.cs` — record
5. `Services/ILlmClient.cs` + `MockLlmClient.cs`
6. `Services/IJobStore.cs` + `InMemoryJobStore.cs`
7. `Services/TextChunker.cs`
8. `Workers/SummarizationWorker.cs`
9. `Program.cs` — wire everything together
10. `README.md` — last, after all code works

---

## PROGRAM.CS STRUCTURE (Minimal API)

```csharp
// Registration order:
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<Channel<string>>(/* bounded channel, capacity 100 */);
builder.Services.AddSingleton<ILlmClient, MockLlmClient>();
builder.Services.AddHostedService<SummarizationWorker>();

// Polly retry pipeline on ILlmClient (add as decorator or use ResiliencePipeline directly)

// Endpoints:
app.MapPost("/summarize", ...)
app.MapGet("/summarize/{jobId}", ...)
```

---

## WHAT TO SAY IN THE DEMO

When running, show these curl commands working:
1. POST with short text → immediate jobId response
2. GET status → Pending, then Completed
3. POST with 12,000 word text → show chunking in logs
4. GET result → combined summary with tokens + cost
5. Mention: "10% mock failure rate triggers Polly retry — you can see retries in the logs"

---

## DO NOT DO THESE THINGS

- Do NOT use Entity Framework or any database (in-memory is correct for scope)
- Do NOT use Hangfire (overkill, adds complexity)
- Do NOT use FluentValidation (not worth the setup time)
- Do NOT use controller-based API (Minimal API is faster and cleaner here)
- Do NOT call a real LLM API (waste of time)
- Do NOT add authentication/authorization (out of scope)
- Do NOT add Swagger UI setup beyond the default (auto-included in .NET 8)
- Do NOT add unit tests (no time — mention in README "what I'd add next")
