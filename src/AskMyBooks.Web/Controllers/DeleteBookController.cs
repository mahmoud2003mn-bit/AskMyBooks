using AskMyBooks.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Web.Controllers;

[ApiController]
[Route("books")]
public sealed class DeleteBookController : ControllerBase
{
    private readonly IBookService _bookService;

    public DeleteBookController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _bookService.DeleteAsync(id, ct);
        return NoContent();
    }
}