using AskMyBooks.Services.Dtos;

namespace AskMyBooks.Services.Abstractions;

public interface IAskService
{
    Task<AskResponse> AskAsync(AskRequest request, CancellationToken ct);
}