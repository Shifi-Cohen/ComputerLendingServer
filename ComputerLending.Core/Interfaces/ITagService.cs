using ComputerLending.Core.DTOs.Tags;
using ComputerLending.Core.Results;

namespace ComputerLending.Core.Interfaces;

public interface ITagService
{
    Task<Result<IReadOnlyList<TagResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<TagResponse>> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
