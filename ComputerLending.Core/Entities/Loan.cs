using ComputerLending.Core.Enums;

namespace ComputerLending.Core.Entities;

public class Loan
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ComputerId { get; set; }
    public DateTime LoanedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public LoanStatus Status { get; set; }
    public User User { get; set; } = null!;
    public Computer Computer { get; set; } = null!;
}
