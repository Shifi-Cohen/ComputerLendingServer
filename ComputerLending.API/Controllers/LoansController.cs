using System.Security.Claims;
using ComputerLending.API.Extensions;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComputerLending.API.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loans;

    public LoansController(ILoanService loans)
    {
        _loans = loans;
    }

    [HttpPost]
    public async Task<IActionResult> Borrow([FromBody] CreateLoanRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out var userId, out _))
            return Unauthorized();

        var result = await _loans.BorrowAsync(userId, request, cancellationToken);
        var location = result.Value is null ? null : $"/api/loans/{result.Value.Id}";
        return result.ToActionResult(this, location);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] LoanQuery query, CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out var userId, out var role))
            return Unauthorized();

        var result = await _loans.GetPagedAsync(userId, role, query, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out var userId, out var role))
            return Unauthorized();

        var result = await _loans.GetByIdAsync(id, userId, role, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Return(
        [FromRoute] int id,
        [FromBody] UpdateLoanRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out var userId, out var role))
            return Unauthorized();

        var result = await _loans.ReturnAsync(id, userId, role, request, cancellationToken);
        return result.ToActionResult(this);
    }

    private bool TryGetCaller(out int userId, out UserRole role)
    {
        role = default;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
            && Enum.TryParse(User.FindFirstValue(ClaimTypes.Role), out role);
    }
}
