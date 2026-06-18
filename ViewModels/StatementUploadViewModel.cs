using System.ComponentModel.DataAnnotations;

namespace StatementParser.ViewModels;

public class StatementUploadViewModel
{
    [Required(ErrorMessage = "Please select a PDF file.")]
    [DataType(DataType.Upload)]
    [Display(Name = "Bank Statement (PDF)")]
    public IFormFile? File { get; set; }

    /// <summary>
    /// Optional: force a specific bank parser (shown when auto-detect fails).
    /// </summary>
    public string? SelectedBankKey { get; set; }

    /// <summary>
    /// Available banks for manual selection fallback.
    /// </summary>
    public List<BankOptionViewModel>? AvailableBanks { get; set; }

    /// <summary>
    /// True when user selected a specific bank after auto-detect failure.
    /// </summary>
    public bool IsRetry => !string.IsNullOrEmpty(SelectedBankKey);
}

public class BankOptionViewModel
{
    public string BankKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
