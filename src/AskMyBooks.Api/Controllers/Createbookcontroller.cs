using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Controllers;

[ApiController]
[Route("books")]
public sealed class CreateBookController : ControllerBase
{
    private readonly IBookService _bookService;

    public CreateBookController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookRequest request, CancellationToken ct)
    {
        var result = await _bookService.CreateAsync(request, ct);
        return CreatedAtAction("GetAll", "GetBooks", new { id = result.Id }, result);
    }
}