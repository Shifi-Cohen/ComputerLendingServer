using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Computers;
using ComputerLending.Core.Results;

namespace ComputerLending.Core.Interfaces;

public interface IComputerService
{
    Task<Result<PagedResult<ComputerResponse>>> GetPagedAsync(ComputerQuery query, CancellationToken cancellationToken = default);
    Task<Result<ComputerResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<ComputerResponse>> CreateAsync(CreateComputerRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int id, UpdateComputerRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<Result> SetTagsAsync(int id, SetComputerTagsRequest request, CancellationToken cancellationToken = default);
}
