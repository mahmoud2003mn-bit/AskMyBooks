using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Controllers;

[ApiController]
[Route("books/extract")]
public sealed class ExtractController : ControllerBase
{
    private readonly IBookService _bookService;

    public ExtractController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpPost]
    public async Task<IActionResult> Extract([FromBody] ExtractRequest request, CancellationToken ct)
    {
        var result = await _bookService.ExtractAsync(request, ct);
        return Created($"/books/{result.Id}", result);
    }
}
