using AskMyBooks.Application.Abstractions;

namespace AskMyBooks.Infrastructure.Llm;

public sealed class NoOpLlmClient : ILlmClient
{
    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
        => Task.FromResult(new float[768]); // متجه فاضي مؤقت

    public Task<string> CompleteJsonAsync(string system, string user, CancellationToken ct)
        => throw new NotImplementedException("LLM extraction not wired yet.");

    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
        => throw new NotImplementedException("LLM ask not wired yet.");
}