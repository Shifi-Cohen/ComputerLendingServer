namespace ComputerLending.Core.Options;

public class LoanRulesOptions
{
    public const string SectionName = "LoanRules";

    public int MaxActiveLoansPerUser { get; set; } = 2;
    public int MaxLoanDays { get; set; } = 14;
}
