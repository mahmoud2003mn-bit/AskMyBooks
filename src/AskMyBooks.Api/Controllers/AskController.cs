using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Controllers;

[ApiController]
[Route("ask")]
public sealed class AskController : ControllerBase
{
    private readonly IAskService _askService;

    public AskController(IAskService askService)
    {
        _askService = askService;
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken ct)
    {
        var result = await _askService.AskAsync(request, ct);
        return Ok(result);
    }
}