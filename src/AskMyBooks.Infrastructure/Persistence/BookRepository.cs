using AskMyBooks.Application.Abstractions;
using AskMyBooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace AskMyBooks.Infrastructure.Persistence;

public sealed class BookRepository : IBookRepository
{
    private readonly AppDbContext _db;

    public BookRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(BookEntry entry, CancellationToken ct)
        => await _db.Books.AddAsync(entry, ct);

    public async Task<IReadOnlyList<BookEntry>> GetAllAsync(ReadingStatus? status, string? author, CancellationToken ct)
    {
        var query = _db.Books.AsNoTracking().AsQueryable();

        if (status is not null)
            query = query.Where(b => b.Status == status);

        if (!string.IsNullOrWhiteSpace(author))
            query = query.Where(b => b.Author == author);

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
    }

    public async Task<BookEntry?> FindAsync(Guid id, CancellationToken ct)
        => await _db.Books.FindAsync([id], ct);

    public Task RemoveAsync(BookEntry entry, CancellationToken ct)
    {
        _db.Books.Remove(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);
}   