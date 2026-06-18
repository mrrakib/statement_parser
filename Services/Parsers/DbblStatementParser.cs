using System.Text.RegularExpressions;
using StatementParser.Models;
using StatementParser.Services.Pdf;

namespace StatementParser.Services.Parsers;

/// <summary>
/// Parser for Dutch-Bangla Bank Ltd. (DBBL) statements.
/// Covers both English and Bengali statement formats.
/// </summary>
public class DbblStatementParser : BaseStatementParser
{
    public override string BankKey => "dutch-bangla";
    public override string DisplayName => "Dutch-Bangla Bank Ltd.";

    private static readonly string[] BankNamePatterns =
    [
        "Dutch-Bangla", "Dutch Bangla", "DBBL",
        "ডাচ-বাংলা", "ডাচ বাংলা ব্যাংক"
    ];

    public override double DetectConfidence(PdfDocument document)
    {
        var headerText = GetHeaderText(document, 30);
        double score = 0;

        if (BankNamePatterns.Any(p => headerText.Contains(p, StringComparison.OrdinalIgnoreCase)))
            score += 0.6;

        // DBBL account pattern: "ACC: 123-456-789" or "A/C: 123.456.789"
        if (Regex.IsMatch(headerText, @"ACC[:\s]+[\d]{3}[-\.][\d]{3}[-\.][\d]{3}", RegexOptions.IgnoreCase))
            score += 0.3;

        // DBBL specific headers
        if (headerText.Contains("Transaction History", StringComparison.OrdinalIgnoreCase) ||
            headerText.Contains("STATEMENT OF ACCOUNT", StringComparison.OrdinalIgnoreCase))
            score += 0.2;

        return Math.Min(score, 1.0);
    }

    public override BankStatement Parse(PdfDocument document)
    {
        var statement = new BankStatement
        {
            BankName = "Dutch-Bangla Bank Limited",
            Currency = "BDT"
        };

        var headerText = GetHeaderText(document, 30);

        // Extract account info
        var accMatch = Regex.Match(headerText,
            @"(?:ACC|A/C|Account)[:\s#]*([\d\-\.]{6,20})", RegexOptions.IgnoreCase);
        if (accMatch.Success)
            statement.AccountNumber = accMatch.Groups[1].Value.Trim();

        // DBBL format: Date | Particulars | Cheque | Debit | Credit | Balance
        foreach (var page in document.Pages)
        {
            var rows = ExtractTableRows(page,
            [
                "date", "particulars", "cheque", "debit", "credit", "balance"
            ]);

            foreach (var row in rows)
            {
                var tx = ParseTransactionRow(row.Text);
                if (tx is not null)
                    statement.Transactions.Add(tx);
            }
        }

        if (statement.Transactions.Count > 0)
            statement.ClosingBalance = statement.Transactions.Last().Balance;

        return statement;
    }

    private Transaction? ParseTransactionRow(string line)
    {
        line = line.Trim();
        if (string.IsNullOrWhiteSpace(line)) return null;

        var tx = new Transaction();
        var parts = Regex.Split(line, @"\s{2,}") // split by 2+ spaces
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count < 2) return null;

        // First part is date
        tx.Date = ParseDate(parts[0]) ?? default;

        // Middle parts are description + reference
        if (parts.Count >= 5)
        {
            // Has Cheque column
            tx.Description = parts[1];
            tx.Reference = parts[2];
            tx.Debit = ParseAmountOrNull(parts[^3]);
            tx.Credit = ParseAmountOrNull(parts[^2]);
            tx.Balance = ParseAmountOrNull(parts[^1]);
        }
        else if (parts.Count == 4)
        {
            tx.Description = parts[1];
            tx.Debit = ParseAmountOrNull(parts[^3]);
            tx.Credit = ParseAmountOrNull(parts[^2]);
            tx.Balance = ParseAmountOrNull(parts[^1]);
        }
        else if (parts.Count == 3)
        {
            tx.Description = parts[1];
            // Could be Debit+Balance or Credit+Balance
            var amt1 = ParseAmountOrNull(parts[^2]);
            var amt2 = ParseAmountOrNull(parts[^1]);
            if (amt1.HasValue && amt2.HasValue)
            {
                tx.Balance = amt2;
                // Figure out if amt1 is debit or credit
                if (parts[^2].Contains('-') || parts[^2].Contains("DR"))
                    tx.Debit = amt1;
                else
                    tx.Credit = amt1;
            }
        }

        return tx;
    }
}
