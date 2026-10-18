using ComputerLending.Core.DTOs.Auth;
using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using ComputerLending.Core.Exceptions;
using ComputerLending.Core.Interfaces;
using ComputerLending.Core.Results;
using Microsoft.Extensions.Logging;

namespace ComputerLending.Service;

public class AuthService : IAuthService
{
    private const string InvalidCredentials = "אימייל או סיסמה שגויים";

    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        ILogger<AuthService> logger)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await _users.EmailExistsAsync(email, cancellationToken))
            return Result<AuthResponse>.Conflict("האימייל כבר קיים.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Borrower,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _users.AddAsync(user, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<AuthResponse>.Conflict("האימייל כבר קיים.");
        }

        _logger.LogInformation("User registered with id {UserId}", user.Id);
        return Result.Created(BuildAuthResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Unauthorized(InvalidCredentials);

        _logger.LogInformation("User {UserId} logged in", user.Id);
        return Result.Ok(BuildAuthResponse(user));
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var expiresAtUtc = _tokens.GetExpiresAtUtc();
        return new AuthResponse
        {
            Token = _tokens.CreateToken(user, expiresAtUtc),
            ExpiresAtUtc = expiresAtUtc,
            Email = user.Email,
            Role = user.Role
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
