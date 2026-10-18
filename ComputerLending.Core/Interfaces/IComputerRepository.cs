using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Computers;
using ComputerLending.Core.Entities;

namespace ComputerLending.Core.Interfaces;

public interface IComputerRepository
{
    Task<Computer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Computer?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default);
    Task<Computer?> GetByIdWithTagsAsync(int id, CancellationToken cancellationToken = default);
    Task<Computer?> GetByIdTrackedWithTagsAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<Computer>> GetPagedAsync(ComputerQuery query, CancellationToken cancellationToken = default);
    Task<bool> AssetTagExistsAsync(string assetTag, CancellationToken cancellationToken = default);
    Task<bool> HasActiveLoanAsync(int computerId, CancellationToken cancellationToken = default);
    Task AddAsync(Computer computer, CancellationToken cancellationToken = default);
    void Remove(Computer computer);
}
