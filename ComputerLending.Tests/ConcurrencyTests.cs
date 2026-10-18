using AutoMapper;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Exceptions;
using ComputerLending.Core.Options;
using ComputerLending.Core.Results;
using ComputerLending.Data;
using ComputerLending.Data.Repositories;
using ComputerLending.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ComputerLending.Tests;

/// <summary>
/// בדיקת ההתנגשות האמיתית: שני DbContext-ים על אותו מסד, בלי Mock על שכבת הנתונים.
/// </summary>
public class ConcurrencyTests : SqliteTestBase
{
    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

    [Fact]
    public async Task TwoContextsUpdatingSameComputer_SecondSaveThrowsDbUpdateConcurrencyException()
    {
        using var ctx1 = NewContext();
        using var ctx2 = NewContext();
        var c1 = await ctx1.Computers.FirstAsync();
        var c2 = await ctx2.Computers.FirstAsync();

        c1.Status = ComputerStatus.Loaned;
        c2.Status = ComputerStatus.Loaned;

        await ctx1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());
    }

    [Fact]
    public async Task UnitOfWork_ConvertsDbUpdateConcurrencyException_ToConcurrencyConflictException()
    {
        using var ctx1 = NewContext();
        using var ctx2 = NewContext();
        var c1 = await ctx1.Computers.FirstAsync();
        var c2 = await ctx2.Computers.FirstAsync();

        c1.Status = ComputerStatus.Loaned;
        c2.Status = ComputerStatus.Loaned;

        await new UnitOfWork(ctx1).SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(ctx2).SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_IncrementsComputerVersion_OnEveryUpdate()
    {
        using (var ctx = NewContext())
        {
            var computer = await ctx.Computers.FirstAsync();
            Assert.Equal(0, computer.Version);

            computer.Status = ComputerStatus.InMaintenance;
            await ctx.SaveChangesAsync();
            Assert.Equal(1, computer.Version);

            computer.Status = ComputerStatus.Available;
            await ctx.SaveChangesAsync();
        }

        using var verify = NewContext();
        Assert.Equal(2, (await verify.Computers.AsNoTracking().FirstAsync()).Version);
    }

    [Fact]
    public async Task Borrow_WhenAnotherUserTookTheComputerAfterTheRead_ReturnsConflict_AndRollsBack()
    {
        using var ctx1 = NewContext();
        using var ctx2 = NewContext();
        var service1 = CreateService(ctx1);
        var service2 = CreateService(ctx2);

        // הבקשה של ctx2 כבר קראה את המחשב כ-Available (מצב ישן ב-change tracker),
        // ובינתיים הבקשה של ctx1 מספיקה לשמור. זו בדיוק ההתחרות.
        var staleRead = await ctx2.Computers.FirstAsync();
        Assert.Equal(ComputerStatus.Available, staleRead.Status);

        var first = await service1.BorrowAsync(FirstUserId, NewRequest());
        var second = await service2.BorrowAsync(SecondUserId, NewRequest());

        Assert.Equal(ResultStatus.Created, first.Status);
        Assert.Equal(ResultStatus.Conflict, second.Status);

        using var verify = NewContext();
        var loans = await verify.Loans.AsNoTracking().ToListAsync();
        var computer = await verify.Computers.AsNoTracking().FirstAsync();

        Assert.Single(loans);
        Assert.Equal(FirstUserId, loans[0].UserId);
        Assert.Equal(ComputerStatus.Loaned, computer.Status);
        Assert.Equal(1, computer.Version);
    }

    [Fact]
    public async Task Borrow_ComputerAlreadyLoaned_ReturnsConflict()
    {
        using var ctx = NewContext();
        var service = CreateService(ctx);

        var first = await service.BorrowAsync(FirstUserId, NewRequest());
        var second = await service.BorrowAsync(SecondUserId, NewRequest());

        Assert.Equal(ResultStatus.Created, first.Status);
        Assert.Equal(ResultStatus.Conflict, second.Status);
    }

    [Fact]
    public async Task Return_MakesComputerAvailableAgain_AndItCanBeBorrowedByAnotherUser()
    {
        using var ctx = NewContext();
        var service = CreateService(ctx);

        var borrowed = await service.BorrowAsync(FirstUserId, NewRequest());
        var returned = await service.ReturnAsync(
            borrowed.Value!.Id, FirstUserId, UserRole.Borrower, new UpdateLoanRequest { Status = LoanStatus.Returned });
        var borrowedAgain = await service.BorrowAsync(SecondUserId, NewRequest());

        Assert.Equal(ResultStatus.Ok, returned.Status);
        Assert.Equal(LoanStatus.Returned, returned.Value!.Status);
        Assert.Equal(ResultStatus.Created, borrowedAgain.Status);
    }

    private static CreateLoanRequest NewRequest() =>
        new() { ComputerId = ComputerId, DueAtUtc = DateTime.UtcNow.AddDays(7) };

    private static LoanService CreateService(AppDbContext ctx) =>
        new(
            new LoanRepository(ctx),
            new ComputerRepository(ctx),
            new UserRepository(ctx),
            new UnitOfWork(ctx),
            Mapper,
            Options.Create(new LoanRulesOptions()),
            NullLogger<LoanService>.Instance);
}
