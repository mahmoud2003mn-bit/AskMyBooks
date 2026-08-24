using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Controllers;

// ✅ منفصل عن BooksController عمدًا — Extract مش عملية CRUD عادية، هي ميزة AI
// (استخلاص كتاب من نص حر عن طريق LLM)، زي بالظبط ما SearchApiController كان
// منفصل عن EmployeeApiController/VacationTypeApiController في VacationsSystem
// لأن البحث الدلالي كمان ميزة AI مختلفة عن الـ CRUD العادي.
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
        return CreatedAtAction("GetAll", "Books", new { id = result.Id }, result);
    }
}