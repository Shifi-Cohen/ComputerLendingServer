using System.ComponentModel.DataAnnotations;
using ComputerLending.Core.Enums;

namespace ComputerLending.Core.Entities;

public class Computer
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Specs { get; set; }
    public ComputerStatus Status { get; set; }
    [ConcurrencyCheck]
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
