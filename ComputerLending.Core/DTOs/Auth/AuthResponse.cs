using ComputerLending.Core.Enums;

namespace ComputerLending.Core.DTOs.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
