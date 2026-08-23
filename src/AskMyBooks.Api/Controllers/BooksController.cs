using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Controllers;

// Pure HTTP boundary: no validation, no error mapping, no business logic here.
// ExceptionHandlingMiddleware turns the exceptions thrown by the service layer
// into the right status codes for every action below.
[ApiController]
[Route("books")]
public sealed class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookRequest request, CancellationToken ct)
    {
        var result = await _bookService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _bookService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("extract")]
    public async Task<IActionResult> Extract([FromBody] ExtractRequest request, CancellationToken ct)
    {
        var result = await _bookService.ExtractAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }
}