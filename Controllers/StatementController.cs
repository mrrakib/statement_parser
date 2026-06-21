using Microsoft.AspNetCore.Mvc;
using StatementParser.Models;
using StatementParser.Services;
using StatementParser.Services.Pdf;
using StatementParser.ViewModels;
using System.Text.Json;

namespace StatementParser.Controllers;

public class StatementController : Controller
{
    private readonly ILogger<StatementController> _logger;
    private readonly StatementProcessingService _processingService;

    public StatementController(
        ILogger<StatementController> logger,
        StatementProcessingService processingService)
    {
        _logger = logger;
        _processingService = processingService;
    }

    /// <summary>
    /// Upload page — accepts customer context from Customer flow via query params.
    /// </summary>
    public IActionResult Upload(
        string? accountNo = null,
        string? accountName = null,
        string? accountType = null,
        string? currency = null,
        string? branch = null,
        string? cif = null)
    {
        // If called from Customer flow, persist info in ViewBag + TempData
        if (accountNo is not null)
        {
            ViewBag.CustomerAccountNo = accountNo;
            ViewBag.CustomerName = accountName;
            ViewBag.CustomerAccountType = accountType;
            ViewBag.CustomerCurrency = currency;
            ViewBag.CustomerBranch = branch;
            ViewBag.CustomerCIF = cif;

            // Save for POST flow (survives form submission)
            TempData["CustomerAccountNo"] = accountNo;
            TempData["CustomerName"] = accountName;
            TempData["CustomerAccountType"] = accountType;
            TempData["CustomerCurrency"] = currency;
            TempData["CustomerBranch"] = branch;
            TempData["CustomerCIF"] = cif;
        }

        return View(new StatementUploadViewModel());
    }

    /// <summary>
    /// Process uploaded PDF and show editable preview.
    /// </summary>
    [HttpPost]
    public IActionResult Upload(StatementUploadViewModel model)
    {
        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError("File", "Please select a file to upload.");
            model.AvailableBanks = GetAvailableBankOptions();
            return View(model);
        }

        if (!model.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("File", "Only PDF files are supported.");
            model.AvailableBanks = GetAvailableBankOptions();
            return View(model);
        }

        try
        {
            var result = _processingService.ProcessAsync(model.File.OpenReadStream()).Result;

            if (!result.Success)
            {
                TempData["Error"] = $"Could not parse this PDF. Detection result: {result.DetectedBank ?? "Unknown"} with {result.Confidence:P1} confidence.";
                model.AvailableBanks = GetAvailableBankOptions();
                return View(model);
            }

            TempData["Success"] = $"Parsed {result.Statement?.TransactionCount ?? 0} transactions from {result.DetectedBank}.";
            TempData["Confidence"] = $"{result.Confidence:P1}";

            // Build editable ViewModel — merge PDF data + customer flow data
            var editVm = new StatementEditViewModel
            {
                // From PDF parser
                BankName = result.Statement!.BankName,
                AccountNumber = result.Statement.AccountNumber,
                AccountHolderName = result.Statement.AccountHolderName,
                AccountType = result.Statement.AccountType,
                Currency = result.Statement.Currency,

                // From customer flow (overrides PDF data where available)
                CustomerName = TempData["CustomerName"] as string ?? result.Statement.AccountHolderName,
                CustomerAccountNo = TempData["CustomerAccountNo"] as string ?? result.Statement.AccountNumber,
                CustomerAccountType = TempData["CustomerAccountType"] as string ?? result.Statement.AccountType,
                CustomerCurrency = TempData["CustomerCurrency"] as string ?? result.Statement.Currency,
                CustomerBranch = TempData["CustomerBranch"] as string ?? "",
                CustomerCIF = TempData["CustomerCIF"] as string ?? "",

                Transactions = result.Statement.Transactions.Select((tx, idx) => new EditableTransaction
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
            editVm.ComputeHeaderFromTransactions();

            return View("Preview", editVm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing statement: {FileName}", model.File.FileName);
            TempData["Error"] = $"An unexpected error occurred: {ex.Message}";
            model.AvailableBanks = GetAvailableBankOptions();
            return View(model);
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

        TempData["Success"] = $"{model.Transactions.Count} transactions submitted successfully.";

        _logger.LogInformation(
            "Statement submitted: {Bank} A/C {Account}, {Count} transactions, Total Dr: {Dr:N2}, Total Cr: {Cr:N2}",
            model.BankName, model.AccountNumber, model.Transactions.Count,
            model.TotalDebit, model.TotalCredit);

        return RedirectToAction(nameof(Upload));
    }

    private static List<BankOptionViewModel> GetAvailableBankOptions()
    {
        return
        [
            new BankOptionViewModel { BankKey = "ibbl", DisplayName = "Islami Bank Bangladesh PLC" },
            new BankOptionViewModel { BankKey = "dbbl", DisplayName = "Dutch-Bangla Bank PLC" }
        ];
    }
}
