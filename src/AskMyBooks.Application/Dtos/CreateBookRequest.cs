namespace AskMyBooks.Application.Dtos;

public sealed record CreateBookRequest(
    string Title,
    string Author,
    string Genre,
    string Status,
    string? Notes,
    int? Rating,
    DateOnly? FinishedOn);