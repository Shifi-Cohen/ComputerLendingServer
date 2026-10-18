using AutoMapper;
using ComputerLending.Core.DTOs.Tags;
using ComputerLending.Core.Entities;
using ComputerLending.Core.Exceptions;
using ComputerLending.Core.Interfaces;
using ComputerLending.Core.Results;
using Microsoft.Extensions.Logging;

namespace ComputerLending.Service;

public class TagService : ITagService
{
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<TagService> _logger;

    public TagService(
        ITagRepository tags,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<TagService> logger)
    {
        _tags = tags;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<TagResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tags = await _tags.GetAllAsync(cancellationToken);
        return Result.Ok(_mapper.Map<IReadOnlyList<TagResponse>>(tags));
    }

    public async Task<Result<TagResponse>> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (await _tags.NameExistsAsync(name, cancellationToken))
            return Result<TagResponse>.Conflict("התגית כבר קיימת.");

        var tag = _mapper.Map<Tag>(request);
        tag.Name = name;

        await _tags.AddAsync(tag, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<TagResponse>.Conflict("התגית כבר קיימת.");
        }

        _logger.LogInformation("Tag {TagId} created", tag.Id);
        return Result.Created(_mapper.Map<TagResponse>(tag));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tags.GetByIdAsync(id, cancellationToken);
        if (tag is null)
            return Result.NotFound("התגית לא נמצאה.");

        _tags.Remove(tag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag {TagId} deleted", id);
        return Result.NoContent();
    }
}
