namespace StatementParser.Models;

/// <summary>
/// Normalized bank statement — every bank parser maps into this.
/// </summary>
public class BankStatement
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public string Currency { get; set; } = "BDT";

    public List<Transaction> Transactions { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public decimal TotalDebit => Transactions.Sum(t => t.Debit ?? 0);
    public decimal TotalCredit => Transactions.Sum(t => t.Credit ?? 0);
    public int TransactionCount => Transactions.Count;
}

public class Transaction
{
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
    public decimal? Balance { get; set; }
}
