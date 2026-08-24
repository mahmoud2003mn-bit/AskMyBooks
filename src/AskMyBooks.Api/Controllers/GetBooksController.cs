using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Controllers;

[ApiController]
[Route("books")]
public sealed class GetBooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public GetBooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? author,
        CancellationToken ct)
    {
        var filter = new ReadingStatusFilter(status, author);
        var result = await _bookService.GetAllAsync(filter, ct);
        return Ok(result);
    }
}