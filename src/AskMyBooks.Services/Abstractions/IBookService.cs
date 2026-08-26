using AskMyBooks.Services.Dtos;

namespace AskMyBooks.Services.Abstractions;

public interface IBookService
{
    Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken ct);
    Task<IReadOnlyList<BookResponse>> GetAllAsync(ReadingStatusFilter? filter, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<BookResponse> ExtractAsync(ExtractRequest request, CancellationToken ct);
}

public sealed record ReadingStatusFilter(string? Status, string? Author);