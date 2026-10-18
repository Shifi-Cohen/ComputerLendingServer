using ComputerLending.API.Extensions;
using ComputerLending.Core.DTOs.Tags;
using ComputerLending.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComputerLending.API.Controllers;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly ITagService _tags;

    public TagsController(ITagService tags)
    {
        _tags = tags;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _tags.GetAllAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken cancellationToken)
    {
        var result = await _tags.CreateAsync(request, cancellationToken);
        var location = result.Value is null ? null : $"/api/tags/{result.Value.Id}";
        return result.ToActionResult(this, location);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var result = await _tags.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }
}
