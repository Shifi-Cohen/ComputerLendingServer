using System.ComponentModel.DataAnnotations;

namespace ComputerLending.Core.DTOs.Computers;

public class CreateComputerRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Specs { get; set; }
}
