using ComputerLending.Core.Entities;
using ComputerLending.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ComputerLending.Data.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _db;

    public TagRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Tags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tag>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        // בלי AsNoTracking: התגיות מתווספות למחשב שכבר במעקב באותו context,
        // ומופע מנותק עם אותו מפתח היה מתנגש עם התגיות שכבר נטענו.
        var idList = ids.ToList();
        return await _db.Tags.Where(t => idList.Contains(t.Id)).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default) =>
        _db.Tags.AsNoTracking().AnyAsync(t => t.Name == name, cancellationToken);

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default) =>
        await _db.Tags.AddAsync(tag, cancellationToken);

    public void Remove(Tag tag) => _db.Tags.Remove(tag);
}
