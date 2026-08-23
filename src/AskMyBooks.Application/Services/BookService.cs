using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using AskMyBooks.Application.Exceptions;
using AskMyBooks.Domain;

namespace AskMyBooks.Application.Services;

public sealed class BookService : IBookService
{
    private readonly IBookRepository _repo;
    private readonly ILlmClient _llm;
    private readonly IVectorStore _vectors;

    public BookService(IBookRepository repo, ILlmClient llm, IVectorStore vectors)
    {
        _repo = repo;
        _llm = llm;
        _vectors = vectors;
    }

    public async Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<ReadingStatus>(request.Status, ignoreCase: true, out var status))
            throw new ArgumentException($"Invalid status: {request.Status}");

        var entry = BookEntry.Create(
            request.Title, request.Author, request.Genre,
            status, request.Notes, request.Rating, request.FinishedOn);

        await _repo.AddAsync(entry, ct);
        await _repo.SaveChangesAsync(ct);

        try
        {
            var vector = await _llm.EmbedAsync(entry.ToEmbeddingText(), ct);
            var payload = new VectorPayload(entry.Title, entry.Author, entry.ToEmbeddingText());
            await _vectors.UpsertAsync(entry.Id, vector, payload, ct);
        }
        catch
        {
            await _repo.RemoveAsync(entry, ct);
            await _repo.SaveChangesAsync(ct);
            throw;
        }

        return ToResponse(entry);
    }

    public async Task<IReadOnlyList<BookResponse>> GetAllAsync(ReadingStatusFilter? filter, CancellationToken ct)
    {
        ReadingStatus? status = null;
        if (!string.IsNullOrWhiteSpace(filter?.Status)
            && Enum.TryParse<ReadingStatus>(filter.Status, ignoreCase: true, out var parsed))
        {
            status = parsed;
        }

        var books = await _repo.GetAllAsync(status, filter?.Author, ct);
        return books.Select(ToResponse).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entry = await _repo.FindAsync(id, ct)
            ?? throw new NotFoundException($"Book {id} not found.");

        await _repo.RemoveAsync(entry, ct);
        await _repo.SaveChangesAsync(ct);
        await _vectors.DeleteAsync(id, ct);
    }

    public Task<BookResponse> ExtractAsync(ExtractRequest request, CancellationToken ct)
        => throw new NotImplementedException("LLM extraction not wired yet.");

    private static BookResponse ToResponse(BookEntry b) => new(
        b.Id, b.Title, b.Author, b.Genre, b.Status.ToString(),
        b.Rating, b.Notes, b.FinishedOn, b.CreatedAt);
}