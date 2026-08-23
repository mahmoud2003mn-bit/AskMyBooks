using System.Text.Json;
using System.Text.Json.Serialization;
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

    private const string ExtractionSystemPrompt = """
        You are a data-extraction assistant for a personal book-reading journal.
        Given a short piece of free text describing a book someone read, is reading,
        or wants to read, extract a single structured record.

        Return ONLY a single JSON object (no markdown, no commentary) with exactly
        these keys:
        {
          "title": string,
          "author": string,
          "genre": string,
          "status": "Wishlist" | "Reading" | "Finished",
          "notes": string,
          "rating": number or null,
          "finishedOn": string or null
        }

        Rules:
        - "title" and "author" are required; infer them as best you can from the text.
        - "rating" is 1-5 and ONLY allowed when status is "Finished". Otherwise null.
        - "finishedOn" is an ISO date "YYYY-MM-DD" and ONLY allowed when status is
          "Finished". Otherwise null.
        - If status is "Finished" but the text gives no rating or no date, still set
          them to null — do not invent values.
        - Never invent a rating or date that isn't implied by the text.
        """;

    public async Task<BookResponse> ExtractAsync(ExtractRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Text is required.", nameof(request));

        var json = await _llm.CompleteJsonAsync(ExtractionSystemPrompt, request.Text, ct);

        ExtractedBook extracted;
        try
        {
            extracted = JsonSerializer.Deserialize<ExtractedBook>(json, JsonOptions)
                ?? throw new LlmContractException("The model returned a null extraction result.", json);
        }
        catch (JsonException)
        {
            // CompleteJsonAsync already guarantees syntactically valid JSON; this catches
            // JSON that parses but doesn't match the shape we asked for (wrong types etc).
            throw new LlmContractException("The model's JSON did not match the expected book schema.", json);
        }

        if (string.IsNullOrWhiteSpace(extracted.Title) || string.IsNullOrWhiteSpace(extracted.Author))
            throw new LlmContractException("The model could not identify a title and author in the text.", json);

        var status = string.IsNullOrWhiteSpace(extracted.Status) ? "Wishlist" : extracted.Status.Trim();
        var isFinished = string.Equals(status, "Finished", StringComparison.OrdinalIgnoreCase);

        DateOnly? finishedOn = null;
        if (isFinished && !string.IsNullOrWhiteSpace(extracted.FinishedOn)
            && DateOnly.TryParse(extracted.FinishedOn, out var parsedDate))
        {
            finishedOn = parsedDate;
        }

        // The domain forbids rating/finishedOn unless Finished, so drop anything the
        // model attached to a non-finished book rather than letting it fail validation.
        var rating = isFinished ? extracted.Rating : null;

        // A Finished book needs BOTH rating and date per the domain rules. If the text
        // didn't supply them, downgrade to Reading rather than rejecting the extraction.
        if (isFinished && (rating is null || finishedOn is null))
        {
            status = "Reading";
            rating = null;
            finishedOn = null;
        }

        var createRequest = new CreateBookRequest(
            Title: extracted.Title.Trim(),
            Author: extracted.Author.Trim(),
            Genre: string.IsNullOrWhiteSpace(extracted.Genre) ? "Unspecified" : extracted.Genre.Trim(),
            Status: status,
            Notes: extracted.Notes,
            Rating: rating,
            FinishedOn: finishedOn);

        // Extraction ends in the exact same save pipeline as a normal create:
        // domain validation, Postgres write, embed, Qdrant index, and the same
        // compensating rollback if indexing fails. One path, one set of guarantees.
        return await CreateAsync(createRequest, ct);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record ExtractedBook(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("author")] string? Author,
        [property: JsonPropertyName("genre")] string? Genre,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("notes")] string? Notes,
        [property: JsonPropertyName("rating")] int? Rating,
        [property: JsonPropertyName("finishedOn")] string? FinishedOn);

    private static BookResponse ToResponse(BookEntry b) => new(
        b.Id, b.Title, b.Author, b.Genre, b.Status.ToString(),
        b.Rating, b.Notes, b.FinishedOn, b.CreatedAt);
}