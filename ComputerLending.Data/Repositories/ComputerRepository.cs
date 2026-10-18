using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Computers;
using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ComputerLending.Data.Repositories;

public class ComputerRepository : IComputerRepository
{
    private readonly AppDbContext _db;

    public ComputerRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Computer?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Computers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Computer?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Computers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Computer?> GetByIdWithTagsAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Computers
            .AsNoTracking()
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Computer?> GetByIdTrackedWithTagsAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Computers
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<PagedResult<Computer>> GetPagedAsync(ComputerQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(_db.Computers.AsNoTracking(), query);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await ApplySort(filtered.Include(c => c.Tags).AsSplitQuery(), query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Computer>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public Task<bool> AssetTagExistsAsync(string assetTag, CancellationToken cancellationToken = default) =>
        _db.Computers.AsNoTracking().AnyAsync(c => c.AssetTag == assetTag, cancellationToken);

    public Task<bool> HasActiveLoanAsync(int computerId, CancellationToken cancellationToken = default) =>
        _db.Loans.AsNoTracking().AnyAsync(
            l => l.ComputerId == computerId && l.Status == LoanStatus.Active,
            cancellationToken);

    public async Task AddAsync(Computer computer, CancellationToken cancellationToken = default) =>
        await _db.Computers.AddAsync(computer, cancellationToken);

    public void Remove(Computer computer) => _db.Computers.Remove(computer);

    private static IQueryable<Computer> ApplyFilters(IQueryable<Computer> source, ComputerQuery query)
    {
        if (query.Status is not null)
            source = source.Where(c => c.Status == query.Status);

        if (query.TagId is not null)
            source = source.Where(c => c.Tags.Any(t => t.Id == query.TagId));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            source = source.Where(c =>
                c.Name.ToLower().Contains(term) || c.AssetTag.ToLower().Contains(term));
        }

        return source;
    }

    private static IQueryable<Computer> ApplySort(IQueryable<Computer> source, ComputerQuery query)
    {
        var sortBy = query.SortBy?.Trim().ToLowerInvariant();

        return (sortBy, query.Desc) switch
        {
            ("name", false) => source.OrderBy(c => c.Name),
            ("name", true) => source.OrderByDescending(c => c.Name),
            ("assettag", false) => source.OrderBy(c => c.AssetTag),
            ("assettag", true) => source.OrderByDescending(c => c.AssetTag),
            ("status", false) => source.OrderBy(c => c.Status),
            ("status", true) => source.OrderByDescending(c => c.Status),
            ("createdatutc" or "createdat", false) => source.OrderBy(c => c.CreatedAtUtc),
            ("createdatutc" or "createdat", true) => source.OrderByDescending(c => c.CreatedAtUtc),
            (_, true) => source.OrderByDescending(c => c.Id),
            _ => source.OrderBy(c => c.Id)
        };
    }
}
