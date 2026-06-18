using System.ComponentModel.DataAnnotations;
using StatementParser.Models;

namespace StatementParser.ViewModels;

/// <summary>
/// ViewModel for the editable bank statement preview form.
/// Each transaction row is editable, and the form posts all rows at once.
/// </summary>
public class StatementEditViewModel
{
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountHolderName { get; set; } = "";
    public string AccountType { get; set; } = "";
    public string Currency { get; set; } = "BDT";

    public List<EditableTransaction> Transactions { get; set; } = [];

    /// <summary>Total credit across all transactions</summary>
    public decimal TotalCredit => Transactions.Sum(t => t.Credit ?? 0);
    /// <summary>Total debit across all transactions</summary>
    public decimal TotalDebit => Transactions.Sum(t => t.Debit ?? 0);
}

public class EditableTransaction
{
    public int RowIndex { get; set; }

    [Required]
    [Display(Name = "Date")]
    public string Date { get; set; } = "";

    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Display(Name = "Reference")]
    public string? Reference { get; set; }

    [Display(Name = "Withdraw (Dr)")]
    [Range(0, double.MaxValue)]
    public decimal? Debit { get; set; }

    [Display(Name = "Deposit (Cr)")]
    [Range(0, double.MaxValue)]
    public decimal? Credit { get; set; }

    [Display(Name = "Balance")]
    public decimal? Balance { get; set; }
}
