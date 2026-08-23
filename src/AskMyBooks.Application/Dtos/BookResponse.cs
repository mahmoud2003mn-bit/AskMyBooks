namespace AskMyBooks.Application.Dtos;

public sealed record BookResponse(
    Guid Id,
    string Title,
    string Author,
    string Genre,
    string Status,
    int? Rating,
    string Notes,
    DateOnly? FinishedOn,
    DateTimeOffset CreatedAt);
