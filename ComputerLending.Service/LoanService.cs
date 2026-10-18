using AutoMapper;
using ComputerLending.Core.DTOs.Common;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Exceptions;
using ComputerLending.Core.Interfaces;
using ComputerLending.Core.Options;
using ComputerLending.Core.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ComputerLending.Service;

public class LoanService : ILoanService
{
    private const string AlreadyLoaned = "המחשב כבר מושאל.";
    private const string TakenByAnother = "המחשב נתפס על ידי משתמש אחר. נסי שוב.";

    private readonly ILoanRepository _loans;
    private readonly IComputerRepository _computers;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly LoanRulesOptions _rules;
    private readonly ILogger<LoanService> _logger;

    public LoanService(
        ILoanRepository loans,
        IComputerRepository computers,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IOptions<LoanRulesOptions> rules,
        ILogger<LoanService> logger)
    {
        _loans = loans;
        _computers = computers;
        _users = users;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _rules = rules.Value;
        _logger = logger;
    }

    public async Task<Result<LoanResponse>> BorrowAsync(
        int userId,
        CreateLoanRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // תאריך שהגיע ב-JSON בלי "Z" הוא Unspecified, ו-Npgsql היה זורק חריגה בשמירה.
        if (request.DueAtUtc.Kind != DateTimeKind.Utc)
            return Result<LoanResponse>.Validation("תאריך ההחזרה חייב להיות ב-UTC, למשל 2026-10-20T10:00:00Z.");

        if (request.DueAtUtc <= now)
            return Result<LoanResponse>.Validation("תאריך ההחזרה חייב להיות בעתיד.");

        if (request.DueAtUtc > now.AddDays(_rules.MaxLoanDays))
            return Result<LoanResponse>.Validation($"ניתן להשאיל עד {_rules.MaxLoanDays} ימים.");

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result<LoanResponse>.NotFound("המשתמשת לא נמצאה.");

        var activeLoans = await _loans.CountActiveByUserIdAsync(userId, cancellationToken);
        if (activeLoans >= _rules.MaxActiveLoansPerUser)
            return Result<LoanResponse>.Validation($"ניתן להחזיק עד {_rules.MaxActiveLoansPerUser} הלוואות פעילות.");

        int loanId;
        try
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var computer = await _computers.GetByIdTrackedAsync(request.ComputerId, cancellationToken);
            if (computer is null)
                return Result<LoanResponse>.NotFound("המחשב לא נמצא.");

            if (computer.Status != ComputerStatus.Available)
                return Result<LoanResponse>.Conflict(AlreadyLoaned);

            computer.Status = ComputerStatus.Loaned;
            var loan = new Loan
            {
                UserId = userId,
                Computer = computer,
                LoanedAtUtc = now,
                DueAtUtc = request.DueAtUtc,
                Status = LoanStatus.Active
            };

            await _loans.AddAsync(loan, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            loanId = loan.Id;
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Concurrency conflict while borrowing computer {ComputerId} for user {UserId}",
                request.ComputerId,
                userId);
            return Result<LoanResponse>.Conflict(TakenByAnother);
        }

        _logger.LogInformation("User {UserId} borrowed computer {ComputerId} (loan {LoanId})", userId, request.ComputerId, loanId);
        return await LoadResponseAsync(loanId, created: true, cancellationToken);
    }

    public async Task<Result<LoanResponse>> ReturnAsync(
        int id,
        int requesterId,
        UserRole role,
        UpdateLoanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status != LoanStatus.Returned)
            return Result<LoanResponse>.Validation("ניתן לעדכן הלוואה רק לסטטוס Returned.");

        int computerId;
        try
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var loan = await _loans.GetByIdTrackedAsync(id, cancellationToken);
            if (loan is null)
                return Result<LoanResponse>.NotFound("ההלוואה לא נמצאה.");

            if (!CanAccess(loan, requesterId, role))
                return Result<LoanResponse>.Forbidden("אין הרשאה להחזיר הלוואה זו.");

            if (loan.Status != LoanStatus.Active)
                return Result<LoanResponse>.Conflict("ההלוואה כבר הוחזרה.");

            loan.Status = LoanStatus.Returned;
            loan.ReturnedAtUtc = DateTime.UtcNow;
            loan.Computer.Status = ComputerStatus.Available;
            computerId = loan.ComputerId;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Concurrency conflict while returning loan {LoanId} by user {UserId}",
                id,
                requesterId);
            return Result<LoanResponse>.Conflict(TakenByAnother);
        }

        _logger.LogInformation("Loan {LoanId} returned by user {UserId} (computer {ComputerId})", id, requesterId, computerId);
        return await LoadResponseAsync(id, created: false, cancellationToken);
    }

    public async Task<Result<PagedResult<LoanResponse>>> GetPagedAsync(
        int requesterId,
        UserRole role,
        LoanQuery query,
        CancellationToken cancellationToken = default)
    {
        // Borrower רואה רק את ההלוואות שלה, בלי קשר למה שביקשה בשאילתה.
        if (role != UserRole.Admin)
            query.UserId = requesterId;

        var page = await _loans.GetPagedAsync(query, cancellationToken);
        return Result.Ok(new PagedResult<LoanResponse>
        {
            Items = _mapper.Map<IReadOnlyList<LoanResponse>>(page.Items),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        });
    }

    public async Task<Result<LoanResponse>> GetByIdAsync(
        int id,
        int requesterId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var loan = await _loans.GetByIdAsync(id, cancellationToken);
        if (loan is null)
            return Result<LoanResponse>.NotFound("ההלוואה לא נמצאה.");

        if (!CanAccess(loan, requesterId, role))
            return Result<LoanResponse>.Forbidden("אין הרשאה לצפות בהלוואה זו.");

        return Result.Ok(_mapper.Map<LoanResponse>(loan));
    }

    private async Task<Result<LoanResponse>> LoadResponseAsync(int loanId, bool created, CancellationToken cancellationToken)
    {
        var loan = await _loans.GetByIdAsync(loanId, cancellationToken);
        if (loan is null)
            return Result<LoanResponse>.NotFound("ההלוואה לא נמצאה.");

        var response = _mapper.Map<LoanResponse>(loan);
        return created ? Result.Created(response) : Result.Ok(response);
    }

    private static bool CanAccess(Loan loan, int requesterId, UserRole role) =>
        role == UserRole.Admin || loan.UserId == requesterId;
}
