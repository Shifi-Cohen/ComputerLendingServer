using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.Entities;

namespace ComputerLending.Core.Interfaces;

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Loan?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<Loan>> GetPagedAsync(LoanQuery query, CancellationToken cancellationToken = default);
    Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task AddAsync(Loan loan, CancellationToken cancellationToken = default);
}
