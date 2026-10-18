using AutoMapper;
using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Computers;
using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Exceptions;
using ComputerLending.Core.Interfaces;
using ComputerLending.Core.Results;
using Microsoft.Extensions.Logging;

namespace ComputerLending.Service;

public class ComputerService : IComputerService
{
    private readonly IComputerRepository _computers;
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<ComputerService> _logger;

    public ComputerService(
        IComputerRepository computers,
        ITagRepository tags,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<ComputerService> logger)
    {
        _computers = computers;
        _tags = tags;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<PagedResult<ComputerResponse>>> GetPagedAsync(
        ComputerQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _computers.GetPagedAsync(query, cancellationToken);
        return Result.Ok(new PagedResult<ComputerResponse>
        {
            Items = _mapper.Map<IReadOnlyList<ComputerResponse>>(page.Items),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        });
    }

    public async Task<Result<ComputerResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var computer = await _computers.GetByIdWithTagsAsync(id, cancellationToken);
        if (computer is null)
            return Result<ComputerResponse>.NotFound("המחשב לא נמצא.");

        return Result.Ok(_mapper.Map<ComputerResponse>(computer));
    }

    public async Task<Result<ComputerResponse>> CreateAsync(
        CreateComputerRequest request,
        CancellationToken cancellationToken = default)
    {
        var assetTag = request.AssetTag.Trim();
        if (await _computers.AssetTagExistsAsync(assetTag, cancellationToken))
            return Result<ComputerResponse>.Conflict("מזהה המחשב כבר קיים.");

        var computer = _mapper.Map<Computer>(request);
        computer.AssetTag = assetTag;
        computer.Name = request.Name.Trim();
        computer.Status = ComputerStatus.Available;
        computer.CreatedAtUtc = DateTime.UtcNow;

        await _computers.AddAsync(computer, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<ComputerResponse>.Conflict("מזהה המחשב כבר קיים.");
        }

        _logger.LogInformation("Computer {ComputerId} created", computer.Id);
        var created = await _computers.GetByIdWithTagsAsync(computer.Id, cancellationToken);
        return Result.Created(_mapper.Map<ComputerResponse>(created));
    }

    public async Task<Result> UpdateAsync(
        int id,
        UpdateComputerRequest request,
        CancellationToken cancellationToken = default)
    {
        var computer = await _computers.GetByIdTrackedAsync(id, cancellationToken);
        if (computer is null)
            return Result.NotFound("המחשב לא נמצא.");

        if (request.Status == ComputerStatus.Loaned && computer.Status != ComputerStatus.Loaned)
            return Result.Validation("לא ניתן להגדיר סטטוס Loaned ידנית.");

        // מחשב מושאל חוזר ל-Available רק דרך החזרת ההלוואה, אחרת אפשר היה להשאיל אותו פעמיים.
        if (computer.Status == ComputerStatus.Loaned && request.Status != ComputerStatus.Loaned)
            return Result.Conflict("לא ניתן לשנות סטטוס של מחשב מושאל. יש להחזיר את ההלוואה קודם.");

        var hasActiveLoan = await _computers.HasActiveLoanAsync(id, cancellationToken);
        if (request.Status == ComputerStatus.InMaintenance && hasActiveLoan)
            return Result.Conflict("לא ניתן להעביר לתחזוקה מחשב עם הלוואה פעילה.");

        computer.Name = request.Name.Trim();
        computer.Specs = request.Specs;
        computer.Status = request.Status;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Computer {ComputerId} updated", id);
        return Result.NoContent();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var computer = await _computers.GetByIdTrackedAsync(id, cancellationToken);
        if (computer is null)
            return Result.NotFound("המחשב לא נמצא.");

        if (await _computers.HasActiveLoanAsync(id, cancellationToken))
            return Result.Conflict("לא ניתן למחוק מחשב עם הלוואה פעילה.");

        _computers.Remove(computer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Computer {ComputerId} deleted", id);
        return Result.NoContent();
    }

    public async Task<Result> SetTagsAsync(
        int id,
        SetComputerTagsRequest request,
        CancellationToken cancellationToken = default)
    {
        var computer = await _computers.GetByIdTrackedWithTagsAsync(id, cancellationToken);
        if (computer is null)
            return Result.NotFound("המחשב לא נמצא.");

        var tagIds = request.TagIds.Distinct().ToList();
        var tags = await _tags.GetByIdsAsync(tagIds, cancellationToken);
        if (tags.Count != tagIds.Count)
            return Result.Validation("אחת או יותר מהתגיות לא נמצאו.");

        computer.Tags.Clear();
        foreach (var tag in tags)
            computer.Tags.Add(tag);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Computer {ComputerId} tags updated", id);
        return Result.NoContent();
    }
}
