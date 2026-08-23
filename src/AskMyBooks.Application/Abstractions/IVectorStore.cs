namespace AskMyBooks.Application.Abstractions;

public interface IVectorStore
{
    Task EnsureCollectionAsync(CancellationToken ct);
    Task UpsertAsync(Guid id, float[] vector, VectorPayload payload, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SearchHit>> SearchAsync(float[] q, int topK, double minScore, CancellationToken ct);
}

public sealed record VectorPayload(string Title, string Author, string Text);
public sealed record SearchHit(Guid Id, double Score, VectorPayload Payload);