using System.ComponentModel.DataAnnotations;
using ComputerLending.Core.Enums;

namespace ComputerLending.Core.DTOs.Computers;

public class ComputerQuery
{
    public ComputerStatus? Status { get; set; }
    public int? TagId { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool Desc { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}
