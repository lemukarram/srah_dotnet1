
# Sarh Backend Developer Assessment Analysis

This document provides a comprehensive breakdown and strategic guide for the **Sarh Advanced .NET Backend Developer Assessment**. This assessment is designed to evaluate your ability to architect a robust, concurrent backend system under time pressure.

## Assessment Overview
* **Duration:** 1 hour 30 minutes total (10 mins planning, 80 mins building) [cite: 16].
* **Goal:** Build an asynchronous backend service that orchestrates calls to an AI/LLM API while ensuring stability, concurrency control, and resilience [cite: 9, 27].
* **Philosophy:** Quality over quantity. Well-reasoned, smaller, correct code is preferred over a large amount of half-working code [cite: 12].

---

## Technical Requirements (Core First)
The assessment emphasizes "Core" requirements [cite: 13, 14, 48]. Focus on these before attempting "Stretch" goals.

### 1. Core Requirements
* **Bounded Concurrency:** Use a background worker pipeline to limit model calls to `N` (configurable, default 3) [cite: 49]. Must implement queuing/back-pressure [cite: 50, 72].
* **Chunking & Reassembly:** Implement a map-reduce style logic: summarize chunks individually, then aggregate into a final summary [cite: 51].
* **Resilience:** Handle transient failures (5xx, timeouts, 429s) using retry + backoff strategies (Polly is recommended) [cite: 53, 71].
* **Priority Queue:** "High" priority jobs must be processed before "normal" jobs [cite: 56].
* **Monitoring:** Log tokens (input/output) and calculate costs using structured logging (e.g., Serilog) [cite: 57, 75].
* **Validation:** Strict input validation (empty input, max 50,000 words, content types) [cite: 58].

### 2. Stretch Goals
* **Persistence:** Survive restarts using SQLite/File-based storage [cite: 61].
* **Deduplication:** Cache results by content hash [cite: 63].
* **Listing Endpoint:** Support GET `/jobs?status=...` [cite: 64].
* **Testing:** Include at least one meaningful integration/unit test verifying core guarantees (e.g., concurrency limits) [cite: 65].

---

## Strategy & Implementation Roadmap

### Phase 1: Planning (10 Minutes)
* **Decision Making:** Do not start coding immediately [cite: 19].
* **Data Model:** Define the `Job` structure, `JobStatus` enum, and how you will store state in memory (or persist if time allows).
* **Concurrency Strategy:** Plan how you will use `SemaphoreSlim` or `System.Threading.Channels` to manage the worker pipeline and enforce the `N` concurrency limit [cite: 72, 74].

### Phase 2: Building (80 Minutes)
* **API Layer:** Keep controllers/minimal APIs thin. Delegate logic to services [cite: 68].
* **Mocking:** Use the provided `MockLlmClient` interface to simulate latency and failures. This prevents wasting time on network issues [cite: 85, 87].
* **Resilience:** Implement Polly policies for retries and circuit breaking early [cite: 71].

---

## Evaluation Criteria (Scoring Weights)

| Area | Focus | Weight |
| :--- | :--- | :--- |
| **Async & Concurrency** | Bounded concurrency, priority handling, correct use of channels/semaphores. | 30% |
| **Resilience** | Retries, backoff, cancellation handling. | 20% |
| **Correctness** | Chunking, idempotency, token/cost aggregation. | 20% |
| **Code Quality** | Structure, dependency injection, testability. | 15% |
| **Validation & Errors** | Status codes, error handling. | 10% |
| **Communication** | README, demo, trade-off reasoning. | 5% |

---

## Key Tips for Success
* **Honesty:** Be explicit in your `README` about what features are finished and what were skipped. Never claim functionality that isn't implemented [cite: 134, 135].
* **Trade-offs:** If you run out of time, write a clear comment or README note explaining *how* you would have implemented the missing feature [cite: 169].
* **Communication:** Prepare to discuss your design decisions and concurrency trade-offs during the demo [cite: 133].
