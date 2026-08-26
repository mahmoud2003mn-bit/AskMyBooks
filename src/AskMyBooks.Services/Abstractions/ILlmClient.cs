namespace AskMyBooks.Services.Abstractions;

public interface ILlmClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<string> CompleteJsonAsync(string system, string user, CancellationToken ct);
    Task<string> CompleteAsync(string system, string user, CancellationToken ct);
}
