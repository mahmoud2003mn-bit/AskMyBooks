using AskMyBooks.Application.Abstractions;

namespace AskMyBooks.Infrastructure.Vectors;

public sealed class NoOpVectorStore : IVectorStore
{
    public Task EnsureCollectionAsync(CancellationToken ct) => Task.CompletedTask;

    public Task UpsertAsync(Guid id, float[] vector, VectorPayload payload, CancellationToken ct)
        => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<SearchHit>> SearchAsync(float[] q, int topK, double minScore, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SearchHit>>(Array.Empty<SearchHit>());
}