using Microsoft.AspNetCore.Mvc;
using StatementParser.Services;
using StatementParser.Services.Parsers;
using StatementParser.ViewModels;

namespace StatementParser.Controllers;

public class StatementController : Controller
{
    private readonly StatementProcessingService _processingService;
    private readonly ParserRegistry _parserRegistry;
    private readonly ILogger<StatementController> _logger;

    public StatementController(
        StatementProcessingService processingService,
        ParserRegistry parserRegistry,
        ILogger<StatementController> logger)
    {
        _processingService = processingService;
        _parserRegistry = parserRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Upload form.
    /// Accepts optional customer context from the service selection flow.
    /// </summary>
    [HttpGet]
    public IActionResult Upload(
        string? accountNo = null,
        string? accountName = null,
        string? accountType = null,
        string? currency = null,
        string? branch = null,
        string? cif = null)
    {
        // Pass customer context if coming from service selection
        if (!string.IsNullOrEmpty(accountNo))
        {
            ViewBag.CustomerAccountNo = accountNo;
            ViewBag.CustomerName = accountName;
            ViewBag.CustomerAccountType = accountType;
            ViewBag.CustomerCurrency = currency;
            ViewBag.CustomerBranch = branch;
            ViewBag.CustomerCIF = cif;
        }

        var vm = new StatementUploadViewModel
        {
            AvailableBanks = _parserRegistry.AllParsers
                .Select(p => new BankOptionViewModel
                {
                    BankKey = p.BankKey,
                    DisplayName = p.DisplayName
                }).ToList()
        };
        return View(vm);
    }

    /// <summary>
    /// Handle PDF upload, auto-detect bank, parse, and preview.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB max
    public async Task<IActionResult> Upload(StatementUploadViewModel model, CancellationToken ct)
    {
        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError("File", "Please select a file to upload.");
            model.AvailableBanks = _parserRegistry.AllParsers
                .Select(p => new BankOptionViewModel
                {
                    BankKey = p.BankKey,
                    DisplayName = p.DisplayName
                }).ToList();
            return View(model);
        }

        // Validate PDF extension
        var extension = Path.GetExtension(model.File.FileName).ToLowerInvariant();
        if (extension != ".pdf")
        {
            ModelState.AddModelError("File", "Only PDF files are supported.");
            model.AvailableBanks = _parserRegistry.AllParsers
                .Select(p => new BankOptionViewModel
                {
                    BankKey = p.BankKey,
                    DisplayName = p.DisplayName
                }).ToList();
            return View(model);
        }

        try
        {
            await using var stream = model.File.OpenReadStream();

            // If user selected a specific bank, force it
            string? forceBank = model.IsRetry ? model.SelectedBankKey : null;

            var result = await _processingService.ProcessAsync(stream, forceBank, ct);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage ?? "Failed to process the statement.";
                return RedirectToAction(nameof(Upload));
            }

            TempData["Success"] = $"Parsed {result.Statement?.TransactionCount ?? 0} transactions from {result.DetectedBank}.";
            TempData["Confidence"] = $"{result.Confidence:P1}";

            // Map to editable ViewModel
            var editVm = new StatementEditViewModel
            {
                BankName = result.Statement!.BankName,
                AccountNumber = result.Statement!.AccountNumber,
                AccountHolderName = result.Statement!.AccountHolderName,
                AccountType = result.Statement!.AccountType,
                Currency = result.Statement!.Currency,
                Transactions = result.Statement!.Transactions.Select((tx, idx) => new EditableTransaction
                {
                    RowIndex = idx,
                    Date = tx.Date.ToString("dd/MM/yyyy"),
                    Description = tx.Description,
                    Reference = tx.Reference,
                    Debit = tx.Debit,
                    Credit = tx.Credit,
                    Balance = tx.Balance
                }).ToList()
            };
            return View("Preview", editVm);
        }
        catch (UnsupportedStatementException ex)
        {
            // Auto-detect failed — prompt user to select bank manually
            ModelState.AddModelError("", ex.Message);
            model.AvailableBanks = _parserRegistry.AllParsers
                .Select(p => new BankOptionViewModel
                {
                    BankKey = p.BankKey,
                    DisplayName = p.DisplayName
                }).ToList();
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing statement: {FileName}", model.File.FileName);
            TempData["Error"] = $"An unexpected error occurred: {ex.Message}";
            return RedirectToAction(nameof(Upload));
        }
    }

    /// <summary>
    /// Receive edited transactions after user review and correction.
    /// </summary>
    [HttpPost]
    public IActionResult SubmitEdits(StatementEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fix validation errors before submitting.";
            return View("Preview", model);
        }

        // Here you would save to database, trigger export, etc.
        // For now, we just confirm receipt
        TempData["Success"] = $"{model.Transactions.Count} transactions submitted successfully.";
        
        _logger.LogInformation(
            "Statement submitted: {Bank} A/C {Account}, {Count} transactions, Total Dr: {Dr:N2}, Total Cr: {Cr:N2}",
            model.BankName, model.AccountNumber, model.Transactions.Count,
            model.TotalDebit, model.TotalCredit);

        return RedirectToAction(nameof(Upload));
    }
}
