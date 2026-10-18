using ComputerLending.Core.DTOs.Tags;
using ComputerLending.Core.Enums;

namespace ComputerLending.Core.DTOs.Computers;

public class ComputerResponse
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Specs { get; set; }
    public ComputerStatus Status { get; set; }
    public IReadOnlyList<TagResponse> Tags { get; set; } = [];
}
