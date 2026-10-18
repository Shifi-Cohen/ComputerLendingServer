using System.ComponentModel.DataAnnotations;

namespace ComputerLending.Core.DTOs.Tags;

public class CreateTagRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}
