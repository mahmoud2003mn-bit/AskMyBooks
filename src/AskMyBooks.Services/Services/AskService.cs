using AskMyBooks.Services.Abstractions;
using AskMyBooks.Services.Dtos;
using Microsoft.Extensions.Options;

namespace AskMyBooks.Services.Services;

public sealed class AskService : IAskService
{
    private const string NoAnswer = "I don't have anything in your books that answers that.";

    private readonly ILlmClient _llm;
    private readonly IVectorStore _vectors;
    private readonly AskOptions _options;

    public AskService(ILlmClient llm, IVectorStore vectors, IOptions<AskOptions> options)
    {
        _llm = llm;
        _vectors = vectors;
        _options = options.Value;
    }

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new ArgumentException("Question is required.", nameof(request));

        var queryVector = await _llm.EmbedAsync(request.Question, ct);
        var hits = await _vectors.SearchAsync(queryVector, _options.TopK, _options.MinScore, ct);

        if (hits.Count == 0)
            return new AskResponse(NoAnswer, Array.Empty<AskSource>());

        var context = string.Join(
            "\n\n",
            hits.Select((h, i) => $"[{i + 1}] \"{h.Payload.Title}\" by {h.Payload.Author}\n{h.Payload.Text}"));

        var system = $"""
            You answer questions about the user's personal book journal using ONLY the
            numbered excerpts below. Do not use any outside knowledge about these books.

            {context}

            Rules:
            - Base your answer only on the excerpts above.
            - If the excerpts don't actually answer the question, say plainly that you
              don't have that information in their books. Never guess or invent details.
            - Refer to books by title, not by their [n] number.
            - Keep the answer concise.
            """;

        var answer = await _llm.CompleteAsync(system, request.Question, ct);

        var sources = hits
            .Select(h => new AskSource(h.Id, h.Payload.Title, h.Payload.Author, h.Score))
            .ToList();

        return new AskResponse(answer, sources);
    }
}