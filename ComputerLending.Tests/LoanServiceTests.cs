using AutoMapper;
using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Exceptions;
using ComputerLending.Core.Interfaces;
using ComputerLending.Core.Options;
using ComputerLending.Core.Results;
using ComputerLending.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ComputerLending.Tests;

public class LoanServiceTests
{
    private const int OwnerId = 1;
    private const int OtherUserId = 2;
    private const int ComputerId = 10;
    private const int LoanId = 5;

    private readonly Mock<ILoanRepository> _loans = new();
    private readonly Mock<IComputerRepository> _computers = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITransactionScope> _transaction = new();
    private readonly Mock<ILogger<LoanService>> _logger = new();

    private readonly User _owner = new() { Id = OwnerId, FullName = "Owner User", Role = UserRole.Borrower };
    private readonly Computer _computer = new()
    {
        Id = ComputerId,
        AssetTag = "LAP-010",
        Name = "Dell Latitude",
        Status = ComputerStatus.Available
    };

    private readonly LoanService _sut;

    public LoanServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transaction.Object);
        _users
            .Setup(u => u.GetByIdAsync(OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_owner);
        _loans
            .Setup(l => l.CountActiveByUserIdAsync(OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _computers
            .Setup(c => c.GetByIdTrackedAsync(ComputerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_computer);

        _sut = new LoanService(
            _loans.Object,
            _computers.Object,
            _users.Object,
            _unitOfWork.Object,
            mapper,
            Options.Create(new LoanRulesOptions()),
            _logger.Object);
    }

    // ---------- BorrowAsync ----------

    [Fact]
    public async Task Borrow_ValidRequest_ReturnsCreated_AndComputerBecomesLoaned()
    {
        Loan? added = null;
        _loans
            .Setup(l => l.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>()))
            .Callback<Loan, CancellationToken>((loan, _) =>
            {
                loan.User = _owner;
                added = loan;
            })
            .Returns(Task.CompletedTask);
        _loans
            .Setup(l => l.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => added);

        var result = await _sut.BorrowAsync(OwnerId, NewRequest(7));

        Assert.Equal(ResultStatus.Created, result.Status);
        Assert.Equal(ComputerStatus.Loaned, _computer.Status);
        Assert.Equal(LoanStatus.Active, result.Value!.Status);
        Assert.Equal("Dell Latitude", result.Value.ComputerName);
        Assert.Equal("Owner User", result.Value.UserName);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Borrow_ComputerNotAvailable_ReturnsConflict_AndDoesNotSave()
    {
        _computer.Status = ComputerStatus.Loaned;

        var result = await _sut.BorrowAsync(OwnerId, NewRequest(7));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrow_ComputerDoesNotExist_ReturnsNotFound()
    {
        _computers
            .Setup(c => c.GetByIdTrackedAsync(ComputerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Computer?)null);

        var result = await _sut.BorrowAsync(OwnerId, NewRequest(7));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrow_UserAlreadyAtLimit_ReturnsValidation_AndDoesNotOpenTransaction()
    {
        _loans
            .Setup(l => l.CountActiveByUserIdAsync(OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoanRulesOptions().MaxActiveLoansPerUser);

        var result = await _sut.BorrowAsync(OwnerId, NewRequest(7));

        Assert.Equal(ResultStatus.Validation, result.Status);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(ComputerStatus.Available, _computer.Status);
    }

    [Fact]
    public async Task Borrow_UserDoesNotExist_ReturnsNotFound()
    {
        _users
            .Setup(u => u.GetByIdAsync(OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.BorrowAsync(OwnerId, NewRequest(7));

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Borrow_ConcurrencyConflictOnSave_ReturnsConflict_LogsWarning_AndDoesNotCommit()
    {
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict"));

        var result = await _sut.BorrowAsync(OwnerId, NewRequest(7));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("נסי שוב", result.Message);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarningLogged();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(15)]
    public async Task Borrow_DueDateOutOfAllowedRange_ReturnsValidation(int daysFromNow)
    {
        var result = await _sut.BorrowAsync(OwnerId, NewRequest(daysFromNow));

        Assert.Equal(ResultStatus.Validation, result.Status);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrow_DueDateWithoutUtcKind_ReturnsValidation()
    {
        var unspecified = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(3), DateTimeKind.Unspecified);

        var result = await _sut.BorrowAsync(
            OwnerId,
            new CreateLoanRequest { ComputerId = ComputerId, DueAtUtc = unspecified });

        Assert.Equal(ResultStatus.Validation, result.Status);
    }

    // ---------- ReturnAsync ----------

    [Fact]
    public async Task Return_ByOwner_SetsReturnedAndMakesComputerAvailable()
    {
        var loan = ActiveLoan();

        var result = await _sut.ReturnAsync(LoanId, OwnerId, UserRole.Borrower, ReturnRequest());

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.NotNull(loan.ReturnedAtUtc);
        Assert.Equal(DateTimeKind.Utc, loan.ReturnedAtUtc!.Value.Kind);
        Assert.Equal(ComputerStatus.Available, _computer.Status);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Return_ByAdminWhoIsNotOwner_Succeeds()
    {
        ActiveLoan();

        var result = await _sut.ReturnAsync(LoanId, OtherUserId, UserRole.Admin, ReturnRequest());

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(ComputerStatus.Available, _computer.Status);
    }

    [Fact]
    public async Task Return_ByNonOwnerBorrower_ReturnsForbidden_AndDoesNotSave()
    {
        var loan = ActiveLoan();

        var result = await _sut.ReturnAsync(LoanId, OtherUserId, UserRole.Borrower, ReturnRequest());

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(ComputerStatus.Loaned, _computer.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Return_AlreadyReturned_ReturnsConflict()
    {
        var loan = ActiveLoan();
        loan.Status = LoanStatus.Returned;

        var result = await _sut.ReturnAsync(LoanId, OwnerId, UserRole.Borrower, ReturnRequest());

        Assert.Equal(ResultStatus.Conflict, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Return_LoanDoesNotExist_ReturnsNotFound()
    {
        _loans
            .Setup(l => l.GetByIdTrackedAsync(LoanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);

        var result = await _sut.ReturnAsync(LoanId, OwnerId, UserRole.Borrower, ReturnRequest());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Return_StatusOtherThanReturned_ReturnsValidation()
    {
        ActiveLoan();

        var result = await _sut.ReturnAsync(
            LoanId, OwnerId, UserRole.Borrower, new UpdateLoanRequest { Status = LoanStatus.Active });

        Assert.Equal(ResultStatus.Validation, result.Status);
    }

    [Fact]
    public async Task Return_ConcurrencyConflictOnSave_ReturnsConflict_AndLogsWarning()
    {
        ActiveLoan();
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict"));

        var result = await _sut.ReturnAsync(LoanId, OwnerId, UserRole.Borrower, ReturnRequest());

        Assert.Equal(ResultStatus.Conflict, result.Status);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarningLogged();
    }

    // ---------- GetByIdAsync / GetPagedAsync ----------

    [Fact]
    public async Task GetById_ByNonOwnerBorrower_ReturnsForbidden()
    {
        ActiveLoan();

        var result = await _sut.GetByIdAsync(LoanId, OtherUserId, UserRole.Borrower);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetById_ByAdmin_ReturnsOk()
    {
        ActiveLoan();

        var result = await _sut.GetByIdAsync(LoanId, OtherUserId, UserRole.Admin);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(LoanId, result.Value!.Id);
    }

    [Fact]
    public async Task GetPaged_Borrower_IsRestrictedToHerOwnLoans()
    {
        SetupEmptyPage();

        await _sut.GetPagedAsync(OwnerId, UserRole.Borrower, new LoanQuery { UserId = OtherUserId });

        _loans.Verify(
            l => l.GetPagedAsync(It.Is<LoanQuery>(q => q.UserId == OwnerId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPaged_Admin_KeepsRequestedFilter()
    {
        SetupEmptyPage();

        await _sut.GetPagedAsync(OtherUserId, UserRole.Admin, new LoanQuery { UserId = OwnerId });

        _loans.Verify(
            l => l.GetPagedAsync(It.Is<LoanQuery>(q => q.UserId == OwnerId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------- helpers ----------

    private static CreateLoanRequest NewRequest(int daysFromNow) =>
        new() { ComputerId = ComputerId, DueAtUtc = DateTime.UtcNow.AddDays(daysFromNow) };

    private static UpdateLoanRequest ReturnRequest() => new() { Status = LoanStatus.Returned };

    private Loan ActiveLoan()
    {
        _computer.Status = ComputerStatus.Loaned;
        var loan = new Loan
        {
            Id = LoanId,
            UserId = OwnerId,
            ComputerId = ComputerId,
            Computer = _computer,
            User = _owner,
            LoanedAtUtc = DateTime.UtcNow.AddDays(-1),
            DueAtUtc = DateTime.UtcNow.AddDays(5),
            Status = LoanStatus.Active
        };

        _loans.Setup(l => l.GetByIdTrackedAsync(LoanId, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        _loans.Setup(l => l.GetByIdAsync(LoanId, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        return loan;
    }

    private void SetupEmptyPage() =>
        _loans
            .Setup(l => l.GetPagedAsync(It.IsAny<LoanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Loan> { Items = [], TotalCount = 0, Page = 1, PageSize = 10 });

    private void VerifyWarningLogged() =>
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
}
