using System.ComponentModel.DataAnnotations;
using ComputerLending.Core.Enums;

namespace ComputerLending.Core.DTOs.Loans;

public class UpdateLoanRequest
{
    [Required]
    public LoanStatus Status { get; set; }
}
