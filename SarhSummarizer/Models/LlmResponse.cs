namespace SarhSummarizer.Models;

public record LlmResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    string Model = "mock-gpt-4");
