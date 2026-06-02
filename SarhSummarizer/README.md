# Sarh Summarizer API

This is an asynchronous backend service that orchestrates calls to an AI/LLM API for summarization. It is designed with bounded concurrency, queueing, and resilience in mind.

## Core Requirements Implemented

- **Bounded Concurrency:** The `SummarizationWorker` processes background jobs with a configurable concurrency limit (default 3) using a `SemaphoreSlim`. This acts as a worker pipeline and enforces back-pressure.
- **Priority Queue:** The `JobQueue` uses a two-tier internal queue system, always yielding `high` priority jobs before `normal` jobs.
- **Chunking & Reassembly (Map-Reduce):** Documents are chunked into pieces in `LlmService`. Each chunk is sent to the mock LLM. If there are multiple chunks, their resulting summaries are concatenated and sent for a final summarization pass.
- **Resilience:** Polly is used for transient fault handling in `LlmService`. A retry policy with exponential backoff triggers on `HttpRequestException` (which is randomly thrown by the `MockLlmClient`).
- **Monitoring & Logging:** `ILogger` is used throughout to record tokens used, cost per job, and to trace the job lifecycle. If used with the included `seq` container, structured logs provide deep visibility.
- **Validation:** Strict validation rules apply to incoming text: requests with over 50,000 words are rejected with a 400 Bad Request. Empty content is also rejected.

## Stretch Goals Implemented

- **Listing Endpoint:** The `GET /jobs?status=...&limit=10&offset=0` endpoint is fully implemented and operational, enabling an administrative dashboard to monitor queue length and statuses.
- **Idempotency Key:** Implemented basic idempotency based on an `Idempotency-Key` header during job submission to prevent duplicate job creation.

## Stretch Goals Skipped & Trade-offs (Time Constraints)

To prioritize robust functionality and correct concurrency logic over half-working features, the following stretch goals were omitted:

- **Persistence (SQLite/File-based):** Currently, jobs are stored in an in-memory `ConcurrentDictionary`. 
  - *How I would implement it:* I would use Entity Framework Core with a SQLite provider, mapping the `Job` model to a database table. The background worker would query the DB for "Pending" jobs or use a transactional outbox pattern rather than relying on an in-memory queue.
- **Deduplication by Content Hash:** 
  - *How I would implement it:* Before creating a job, compute a SHA-256 hash of the input text. Check a cache (e.g., Redis or an in-memory cache) for this hash. If a completed result exists, return it immediately to save LLM costs.
- **Unit/Integration Testing:** 
  - *How I would implement it:* I would create an xUnit project, using `WebApplicationFactory` for integration tests to verify the API endpoints. I would inject a test-specific `ILlmClient` to verify that `N` concurrent calls are strictly enforced and priority queueing works as expected under load.

## Running the Application

Use Docker Compose to spin up the API and the Seq logging instance:

```bash
docker compose up --build
```

- API Base URL: `http://localhost:5005`
- Seq Logging UI: `http://localhost:5341`

## Example Flow

1. Submit a job:
```bash
curl -X POST http://localhost:5005/jobs \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: my-unique-key" \
  -d '{"text": "A very long document goes here...", "priority": "high"}'
```

2. Check Status:
```bash
curl http://localhost:5005/jobs/{jobId}
```

3. List Jobs:
```bash
curl "http://localhost:5005/jobs?status=Processing&limit=5"
```us=Processing&limit=5"
```