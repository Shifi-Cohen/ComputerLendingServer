using ComputerLending.Core.Enums;

namespace ComputerLending.Core.DTOs.Loans;

public class LoanResponse
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime LoanedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public LoanStatus Status { get; set; }
}
