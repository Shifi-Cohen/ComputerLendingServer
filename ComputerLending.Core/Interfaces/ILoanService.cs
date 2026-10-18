using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Results;

namespace ComputerLending.Core.Interfaces;

public interface ILoanService
{
    Task<Result<LoanResponse>> BorrowAsync(int userId, CreateLoanRequest request, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<LoanResponse>>> GetPagedAsync(int requesterId, UserRole role, LoanQuery query, CancellationToken cancellationToken = default);
    Task<Result<LoanResponse>> GetByIdAsync(int id, int requesterId, UserRole role, CancellationToken cancellationToken = default);
    Task<Result<LoanResponse>> ReturnAsync(int id, int requesterId, UserRole role, UpdateLoanRequest request, CancellationToken cancellationToken = default);
}
