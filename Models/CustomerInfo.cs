namespace StatementParser.Models;

/// <summary>
/// Represents customer information for the service selection flow.
/// </summary>
public class CustomerInfo
{
    public string AccountNo { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountType { get; set; } = "Savings";
    public string Currency { get; set; } = "BDT";
    public string Branch { get; set; } = "";
    public string CIF { get; set; } = "";
}

public static class CustomerAccountTypes
{
    public static readonly string[] Types = ["Savings", "Current", "Fixed Deposit", "SOD", "Loan", "Other"];
}

public static class CustomerCurrencies
{
    public static readonly string[] Codes = ["BDT", "USD", "EUR", "GBP"];
}
