using AskMyBooks.Application.Abstractions;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AskMyBooks.Infrastructure.Vectors;

/// <summary>
/// الكلاس الوحيد اللي بيعرف نموذج الـ points/collections بتاع Qdrant.
/// لو حبيت تستبدله بـ Pinecone أو pgvector، اكتب كلاس تاني بينفذ IVectorStore
/// وغيّر التسجيل في ServiceCollectionExtensions بس.
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
        var point = new PointStruct
        {
            Id = id,
            Vectors = vector,
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
        // حذف id مش موجود مش بيرمي error في Qdrant — وده اللي عايزينه
        // عشان الـ DELETE يفضل idempotent زي باقي التطبيق.
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