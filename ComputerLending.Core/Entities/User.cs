using ComputerLending.Core.Enums;

namespace ComputerLending.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
