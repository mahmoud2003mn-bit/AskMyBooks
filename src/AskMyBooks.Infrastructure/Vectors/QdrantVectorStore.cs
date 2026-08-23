using AskMyBooks.Application.Abstractions;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AskMyBooks.Infrastructure.Vectors;

/// <summary>
/// The only class that knows Qdrant's point/collection model.
/// To swap in Pinecone or pgvector, write another IVectorStore and change
/// one registration in ServiceCollectionExtensions.
/// </summary>
public sealed class QdrantVectorStore : IVectorStore
{
    private const string TitleKey = "title";
    private const string AuthorKey = "author";
    private const string TextKey = "text";

    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;

    public QdrantVectorStore(QdrantClient client, IOptions<QdrantOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        var exists = await _client.CollectionExistsAsync(_options.Collection, ct);
        if (exists)
            return;

        await _client.CreateCollectionAsync(
            _options.Collection,
            new VectorParams { Size = (ulong)_options.VectorSize, Distance = Distance.Cosine },
            cancellationToken: ct);
    }

    public async Task UpsertAsync(Guid id, float[] vector, VectorPayload payload, CancellationToken ct)
    {
        // Build the dense vector explicitly. The implicit float[] -> Vectors
        // conversion in this SDK version sends an empty vector, which Qdrant
        // rejects with "expected dim: 768, got 0".
        // Types are fully qualified because this file's namespace ends in ".Vectors".
#pragma warning disable CS0612 // Vector.Data is marked obsolete but is what the server reads
        var qdrantVector = new Qdrant.Client.Grpc.Vector();
        qdrantVector.Data.AddRange(vector);
#pragma warning restore CS0612

        var point = new PointStruct
        {
            Id = id,
            Vectors = new Qdrant.Client.Grpc.Vectors { Vector = qdrantVector },
            Payload =
            {
                [TitleKey] = payload.Title,
                [AuthorKey] = payload.Author,
                [TextKey] = payload.Text
            }
        };

        await _client.UpsertAsync(_options.Collection, [point], cancellationToken: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        // Deleting a non-existent id is a no-op in Qdrant, not an error —
        // which keeps DELETE idempotent like the rest of the app expects.
        await _client.DeleteAsync(_options.Collection, [(PointId)id], cancellationToken: ct);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(float[] q, int topK, double minScore, CancellationToken ct)
    {
        var results = await _client.QueryAsync(
            _options.Collection,
            query: q,
            limit: (ulong)topK,
            scoreThreshold: (float)minScore,
            payloadSelector: true,
            cancellationToken: ct);

        return results
            .Select(r => new SearchHit(
                Guid.Parse(r.Id.Uuid),
                r.Score,
                new VectorPayload(
                    GetString(r.Payload, TitleKey),
                    GetString(r.Payload, AuthorKey),
                    GetString(r.Payload, TextKey))))
            .ToList();
    }

    private static string GetString(MapField<string, Value> payload, string key)
        => payload.TryGetValue(key, out var value) ? value.StringValue : "";
}