using ComputerLending.API.Extensions;
using ComputerLending.Core.DTOs.Computers;
using ComputerLending.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComputerLending.API.Controllers;

[ApiController]
[Route("api/computers")]
[Authorize]
public class ComputersController : ControllerBase
{
    private readonly IComputerService _computers;

    public ComputersController(IComputerService computers)
    {
        _computers = computers;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] ComputerQuery query, CancellationToken cancellationToken)
    {
        var result = await _computers.GetPagedAsync(query, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        var result = await _computers.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateComputerRequest request, CancellationToken cancellationToken)
    {
        var result = await _computers.CreateAsync(request, cancellationToken);
        var location = result.Value is null ? null : $"/api/computers/{result.Value.Id}";
        return result.ToActionResult(this, location);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        [FromRoute] int id,
        [FromBody] UpdateComputerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _computers.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var result = await _computers.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:int}/tags")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetTags(
        [FromRoute] int id,
        [FromBody] SetComputerTagsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _computers.SetTagsAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }
}
