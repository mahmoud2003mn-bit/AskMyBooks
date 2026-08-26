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
        // ✅ Fix: Created(uri, value) بدل CreatedAtAction — مش محتاج يولّد رابط
        // عن طريق محاكاة الـ routing (اللي كان بيفشل بـ "No route matches the
        // supplied values" بسبب تعدد الـ controllers المنفصلة لكل action).
        return Created($"/books/{result.Id}", result);
    }
}