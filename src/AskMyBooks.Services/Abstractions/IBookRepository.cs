using AskMyBooks.Domain;

namespace AskMyBooks.Services.Abstractions;

public interface IBookRepository
{
    Task AddAsync(BookEntry entry, CancellationToken ct);
    Task<IReadOnlyList<BookEntry>> GetAllAsync(ReadingStatus? status, string? author, CancellationToken ct);
    Task<BookEntry?> FindAsync(Guid id, CancellationToken ct);
    Task RemoveAsync(BookEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}