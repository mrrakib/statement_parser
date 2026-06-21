using System.ComponentModel.DataAnnotations;
using System.Globalization;
using StatementParser.Models;

namespace StatementParser.ViewModels;

/// <summary>
/// ViewModel for the editable bank statement preview form.
/// Header data merges customer flow (user input) + transaction data.
/// Summary fields (Opening, Closing, From/To) are editable.
/// </summary>
public class StatementEditViewModel
{
    // ── PDF-extracted data (read-only display) ──
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountHolderName { get; set; } = "";
    public string AccountType { get; set; } = "";
    public string Currency { get; set; } = "BDT";

    // ── Customer-flow data (from user input before upload) ──
    public string CustomerName { get; set; } = "";
    public string CustomerAccountNo { get; set; } = "";
    public string CustomerAccountType { get; set; } = "";
    public string CustomerCurrency { get; set; } = "";
    public string CustomerBranch { get; set; } = "";
    public string CustomerCIF { get; set; } = "";

    // ── Editable header / summary fields ──
    // Pre-populated from transactions but user can correct them.
    /// <summary>Statement period start date (editable)</summary>
    public string FromDate { get; set; } = "";

    /// <summary>Statement period end date (editable)</summary>
    public string ToDate { get; set; } = "";

    /// <summary>Opening balance — balance before the first transaction (editable)</summary>
    public decimal? OpeningBalance { get; set; }

    /// <summary>Closing balance — balance after the last transaction (editable)</summary>
    public decimal? ClosingBalance { get; set; }

    /// <summary>
    /// Pre-populate the editable header fields from the transaction data.
    /// Call this after setting Transactions.
    /// </summary>
    public void ComputeHeaderFromTransactions()
    {
        if (Transactions.Count == 0) return;

        // Parse dates chronologically — dd/MM/yyyy string sort is WRONG
        // (e.g. "10/06/2026" < "31/12/2025" alphabetically)
        var sorted = Transactions
            .Select(t => new
            {
                t.Date, t.Balance, t.Credit, t.Debit,
                Parsed = DateOnly.TryParseExact(t.Date, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateOnly.MinValue
            })
            .OrderBy(t => t.Parsed)
            .ToList();

        // From = oldest date, To = newest date
        FromDate = sorted.First().Date;
        ToDate = sorted.Last().Date;

        // Opening = first transaction's balance - its credit + its debit
        var first = sorted.First();
        var opening = (first.Balance ?? 0) - (first.Credit ?? 0) + (first.Debit ?? 0);
        OpeningBalance = opening;

        // Closing = most recent transaction's balance
        var last = sorted.Last();
        ClosingBalance = last.Balance;
    }

    // ── Transactions ──
    public List<EditableTransaction> Transactions { get; set; } = [];

    // ── Totals ──
    public decimal TotalCredit => Transactions.Sum(t => t.Credit ?? 0);
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
