namespace AskMyBooks.Application.Dtos;

public sealed record AskResponse(
    string Answer,
    IReadOnlyList<AskSource> Sources);

public sealed record AskSource(Guid Id, string Title, string Author, double Score);
