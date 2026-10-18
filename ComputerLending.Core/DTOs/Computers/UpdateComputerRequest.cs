using System.ComponentModel.DataAnnotations;
using ComputerLending.Core.Enums;

namespace ComputerLending.Core.DTOs.Computers;

public class UpdateComputerRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Specs { get; set; }

    [Required]
    public ComputerStatus Status { get; set; }
}
