# Sarh Summarizer Service

An asynchronous, resilient document summarization service built with ASP.NET Core 8.0. This service orchestrates calls to an LLM API to summarize long documents while ensuring system stability through bounded concurrency and chunking logic.

## Key Features

### 1. Bounded Concurrency & Queueing
The service uses a background worker (`SummarizationWorker`) and a priority-aware queue (`JobQueue`) to manage workload. 
- **Concurrency Limit:** Set to `N` (default 3), ensuring that no more than 3 model calls are active simultaneously across the entire system.
- **Back-pressure:** Incoming jobs are queued and processed as workers become available, preventing model overload.

### 2. Document Chunking & Reassembly (Map-Reduce)
Documents exceeding the model's context window are split into chunks.
- **Map:** Each chunk is summarized individually.
- **Reduce:** Summaries are combined and summarized once more to produce a coherent final output.
- **Aggregation:** Token usage and USD costs are aggregated across all chunks for accurate reporting.

### 3. Resilience & Stability
Built-in resilience using **Polly**:
- **Retry Policy:** Automatic retries for transient failures (timeouts, rate limits, 5xx) with exponential backoff.
- **Fault Isolation:** A single failed chunk correctly fails the entire job with a detailed error message, preventing partial or misleading results.

### 4. Priority Handling
The service supports `high` and `normal` priority jobs. High-priority jobs jump to the front of the queue to ensure faster processing for critical tasks.

### 5. Input Validation & Robustness
- **Strict Validation:** Rejects empty inputs, documents exceeding 50,000 words, and invalid priority levels.
- **Large Text Input Issue (Fixed):** Resolved a parsing failure caused by unescaped newlines in JSON bodies. Instead of destructive string replacement or strict parser failure, a character-by-character scanner safely escapes control characters (`\n`, `\r`, `\t`) only when they are inside JSON string values, preserving the payload's integrity and special characters.
- **Multi-Format Support:** Accepts both `application/json` and `text/plain` content types.

---

## Technical Design Choices

- **Minimal APIs:** Used for high-performance, low-boilerplate endpoint mapping.
- **BackgroundService:** Leverages .NET's native `IHostedService` for the worker pipeline.
- **Structured Logging:** All token counts, costs, and job events are logged using structured data for easy monitoring and dashboarding.
- **In-Memory Store:** Job states are currently managed in-memory (using `ConcurrentDictionary`) for speed, which can be easily swapped for a persistent store (SQLite/Redis) if needed.

---

## How to Run

### Using Docker Compose
The easiest way to start the service and its dependencies:

```bash
docker-compose up --build
```

The API will be available at `http://localhost:5000`.

### Local Execution (requires .NET 8 SDK)
```bash
cd SarhSummarizer
dotnet run
```

---

## API Endpoints

- `POST /jobs`: Submit a document for summarization.
- `GET /jobs/{jobId}`: Check job status, progress, and retrieve results.
- `GET /jobs`: List all jobs (with optional status filtering and pagination).
- `DELETE /jobs/{jobId}`: Cancel a pending or processing job.

---

## What I Would Add With More Time

1. **Persistence:** Implement a SQLite or Redis back-end to allow jobs to survive service restarts.
2. **Deduplication:** Use a hash (e.g., SHA-256) of the input text to cache results and avoid redundant model calls.
3. **Webhooks:** Allow users to provide a callback URL for job completion notifications instead of polling.
4. **Enhanced Monitoring:** Add a Grafana/Prometheus dashboard to visualize concurrency, error rates, and costs in real-time.
5. **Unit/Integration Tests:** Expand test coverage to include more edge cases and simulated race conditions in the priority queue.
