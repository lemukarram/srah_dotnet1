# SarhSummarizer

A production-quality .NET 8 ASP.NET Core Web API for document summarization. It processes long text documents asynchronously using a background job queue and provides a polling endpoint for status and results.

## Features

- **Asynchronous Processing**: Immediate response with `jobId` while processing happens in the background.
- **Robust Chunking**: Automatically splits documents larger than 3,000 words into chunks for processing.
- **Resilience**: Implements Polly v8 resilience pipelines with exponential backoff retries and timeouts for LLM calls.
- **Observability**: Structured logging of job status, chunking details, token usage, and simulated costs.
- **Clean Architecture**: Separation of concerns between models, services, and background workers.

## Tech Stack

- **Framework**: .NET 8 (ASP.NET Core Minimal API)
- **Job Queue**: `System.Threading.Channels` (In-memory)
- **Background Worker**: `BackgroundService`
- **Resilience**: `Polly`
- **Logging**: `ILogger`
- **Mock LLM**: `MockLlmClient` (simulates latency and 10% failure rate)

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the Application

1. Navigate to the project directory:
   ```bash
   cd SarhSummarizer
   ```
2. Run the application:
   ```bash
   dotnet run
   ```
3. The API will be available at `http://localhost:5000` (or the port specified in your console output). Swagger UI is available at `http://localhost:5000/swagger`.

## API Usage & Demo

### 1. Submit a short document
```bash
curl -X POST http://localhost:5000/summarize \
  -H "Content-Type: application/json" \
  -d '{"text": "The quick brown fox jumps over the lazy dog."}'
```
*Response: 202 Accepted with jobId.*

### 2. Check Job Status
Replace `{jobId}` with the ID from the previous step:
```bash
curl http://localhost:5000/summarize/{jobId}
```
*Response: Pending -> Processing -> Completed.*

### 3. Submit a long document (> 3000 words)
You can use a large text file or repeat text to test chunking logic. The application will log chunking details to the console.

### 4. Validation Errors
Submit empty text to see a 400 Bad Request:
```bash
curl -X POST http://localhost:5000/summarize \
  -H "Content-Type: application/json" \
  -d '{"text": ""}'
```

## Architecture Notes

- **In-Memory Storage**: For the scope of this assessment, jobs are stored in a `ConcurrentDictionary`. In a real-world scenario, this would be replaced by a persistent database (e.g., PostgreSQL or Redis).
- **Background Worker**: The `SummarizationWorker` processes jobs sequentially from the channel. For high-scale, multiple worker instances or a distributed queue (like RabbitMQ) could be used.
- **Polly Retries**: The `MockLlmClient` has a 10% failure rate (TimeoutException). You can observe Polly retrying the requests in the console logs.
- **Cost Calculation**: Costs are calculated using mock GPT-4 pricing: $0.01/1k input tokens and $0.03/1k output tokens.

## Future Improvements (Next Steps)

- **Persistent Storage**: Integrate Entity Framework Core for database persistence.
- **Distributed Queue**: Use MassTransit with RabbitMQ or Azure Service Bus for distributed background processing.
- **Unit & Integration Tests**: Add xUnit tests for `TextChunker`, `MockLlmClient`, and API endpoints.
- **Authentication**: Secure the API with JWT or API Keys.
- **Real LLM Integration**: Replace `MockLlmClient` with a real OpenAI or Azure OpenAI implementation.
