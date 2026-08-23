using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using AskMyBooks.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Controllers;

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
        try
        {
            var result = await _bookService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
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
        try
        {
            await _bookService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }
}