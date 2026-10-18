using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ComputerLending.Data.Repositories;

public class LoanRepository : ILoanRepository
{
    private readonly AppDbContext _db;

    public LoanRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Loan?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        WithDetails(_db.Loans.AsNoTracking())
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<Loan?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Loans
            .Include(l => l.Computer)
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<PagedResult<Loan>> GetPagedAsync(LoanQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(_db.Loans.AsNoTracking(), query);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await WithDetails(filtered)
            .AsSplitQuery()
            .OrderByDescending(l => l.LoanedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Loan>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        _db.Loans.AsNoTracking().CountAsync(
            l => l.UserId == userId && l.Status == LoanStatus.Active,
            cancellationToken);

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken = default) =>
        await _db.Loans.AddAsync(loan, cancellationToken);

    private static IQueryable<Loan> WithDetails(IQueryable<Loan> source) =>
        source
            .Include(l => l.User)
            .Include(l => l.Computer)
            .ThenInclude(c => c.Tags);

    private static IQueryable<Loan> ApplyFilters(IQueryable<Loan> source, LoanQuery query)
    {
        if (query.Status is not null)
            source = source.Where(l => l.Status == query.Status);

        if (query.ComputerId is not null)
            source = source.Where(l => l.ComputerId == query.ComputerId);

        if (query.UserId is not null)
            source = source.Where(l => l.UserId == query.UserId);

        return source;
    }
}
