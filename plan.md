# API Specification: AI Summarization Service

This service provides an asynchronous, queue-based pipeline for processing long-form documents via an LLM.

## Base URL
`http://localhost:5000`

---

## Endpoints

### 1. POST /jobs
[cite_start]**Purpose**: Submit a document for summarization[cite: 30].
* **Request Body**:
    * [cite_start]`text` (string): The document content (max 50,000 words)[cite: 31].
    * [cite_start]`priority` (string, optional): "normal" or "high"[cite: 31].
* **Headers**:
    * [cite_start]`Idempotency-Key` (optional): Ensures that repeated requests with the same key return the original job without duplication[cite: 33].
* **Response**:
    * `202 Accepted`
    * [cite_start]`jobId` (string): Unique identifier for the job[cite: 32].
    * [cite_start]`status` (string): "Pending"[cite: 32].

### 2. GET /jobs/{jobId}
[cite_start]**Purpose**: Retrieve the status and results of a specific job[cite: 34].
* **Response**:
    * [cite_start]`status`: Current state (Pending, Processing, Completed, Failed, Cancelled)[cite: 35].
    * [cite_start]`summary`: The resulting text (if Completed)[cite: 36].
    * [cite_start]`tokens`: Total token usage[cite: 36].
    * [cite_start]`costUsd`: Approximate cost[cite: 36].
    * [cite_start]`progress`: Object containing `chunksDone` and `chunksTotal`[cite: 38].
    * [cite_start]`error`: Structured error information (if Failed)[cite: 37].

### 3. DELETE /jobs/{jobId}
[cite_start]**Purpose**: Cancel a job that is currently Pending or Processing[cite: 39, 40].
* **Response**:
    * `200 OK`
    * [cite_start]`status`: "Cancelled"[cite: 160].
    * [cite_start]*Note*: If the job is already Completed, this acts as a no-op and returns the result[cite: 41].

### 4. GET /jobs (Stretch Goal)
[cite_start]**Purpose**: List jobs for administrative dashboard monitoring[cite: 43].
* **Query Parameters**:
    * [cite_start]`status`: Filter by job status[cite: 42].
    * [cite_start]`limit` / `offset`: Pagination support[cite: 43].
* **Response**:
    * [cite_start]List of job objects, sorted by newest first[cite: 43].