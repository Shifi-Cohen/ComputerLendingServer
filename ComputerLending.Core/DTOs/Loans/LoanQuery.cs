using System.ComponentModel.DataAnnotations;
using ComputerLending.Core.Enums;

namespace ComputerLending.Core.DTOs.Loans;

public class LoanQuery
{
    public LoanStatus? Status { get; set; }
    public int? ComputerId { get; set; }
    public int? UserId { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}
