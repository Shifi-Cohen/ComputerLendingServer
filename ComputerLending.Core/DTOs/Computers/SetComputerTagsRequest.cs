using System.ComponentModel.DataAnnotations;

namespace ComputerLending.Core.DTOs.Computers;

public class SetComputerTagsRequest
{
    [Required]
    public IReadOnlyList<int> TagIds { get; set; } = [];
}
