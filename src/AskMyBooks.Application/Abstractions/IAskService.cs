using AskMyBooks.Application.Dtos;

namespace AskMyBooks.Application.Abstractions;

public interface IAskService
{
    Task<AskResponse> AskAsync(AskRequest request, CancellationToken ct);
}