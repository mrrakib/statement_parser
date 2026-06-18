using Microsoft.AspNetCore.Mvc;
using StatementParser.Models;

namespace StatementParser.Controllers;

public class CustomerController : Controller
{
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(ILogger<CustomerController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Customer search / landing page.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        // Read previously saved customer info from TempData to pre-fill
        var saved = TempData["CustomerInfo"] as string;
        if (saved is not null)
        {
            var info = System.Text.Json.JsonSerializer.Deserialize<CustomerInfo>(saved);
            TempData.Keep("CustomerInfo");
            return View("Create", info);
        }

        return View();
    }

    /// <summary>
    /// Customer input form (GET).
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CustomerInfo
        {
            AccountType = "Savings",
            Currency = "BDT"
        });
    }

    /// <summary>
    /// Customer input form (POST) — stores info and redirects to service selection.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CustomerInfo model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Persist customer info across redirect
        var json = System.Text.Json.JsonSerializer.Serialize(model);
        TempData["CustomerInfo"] = json;
        _logger.LogInformation("Customer created: {AccountNo} {AccountName}", model.AccountNo, model.AccountName);

        return RedirectToAction(nameof(SelectService));
    }

    /// <summary>
    /// Service selection page — Bank Statement or Solvency Certificate.
    /// </summary>
    [HttpGet]
    public IActionResult SelectService()
    {
        var saved = TempData["CustomerInfo"] as string;
        if (saved is null)
            return RedirectToAction(nameof(Create));

        var info = System.Text.Json.JsonSerializer.Deserialize<CustomerInfo>(saved);
        TempData.Keep("CustomerInfo");
        return View(info);
    }

    /// <summary>
    /// Route to the chosen service.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SelectService(string service, CustomerInfo model)
    {
        // Persist customer info
        var json = System.Text.Json.JsonSerializer.Serialize(model);
        TempData["CustomerInfo"] = json;

        _logger.LogInformation("Service selected: {Service} for {AccountNo}", service, model.AccountNo);

        if (service == "BankStatement")
        {
            // Redirect to Statement Upload with customer context
            return RedirectToAction("Upload", "Statement", new
            {
                accountNo = model.AccountNo,
                accountName = model.AccountName,
                accountType = model.AccountType,
                currency = model.Currency,
                branch = model.Branch,
                cif = model.CIF
            });
        }

        if (service == "SolvencyCertificate")
        {
            return View("SolvencyComingSoon", model);
        }

        return RedirectToAction(nameof(Index));
    }
}
