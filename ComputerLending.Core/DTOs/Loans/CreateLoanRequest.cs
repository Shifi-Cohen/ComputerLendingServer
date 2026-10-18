using System.ComponentModel.DataAnnotations;

namespace ComputerLending.Core.DTOs.Loans;

public class CreateLoanRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int ComputerId { get; set; }

    [Required]
    public DateTime DueAtUtc { get; set; }
}
