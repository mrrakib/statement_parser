using System.Globalization;
using System.Text.RegularExpressions;
using StatementParser.Models;
using StatementParser.Services.Pdf;

namespace StatementParser.Services.Parsers;

/// <summary>
/// Parser for Islami Bank Bangladesh Ltd. (IBBL) Mudaraba/MSA statement formats.
///
/// Typical layout (observed):
///   Trans Date | Particulars | Instrument No | Withdraw | Deposit | Balance
///   05/04/2026 | TRNSFR>desc...                 | 0.00     | 1,000.00 | 1,000.00
///
/// Headers: ISLAMI BANK BANGLADESH PLC., NATORE BRANCH, ACCOUNT STATEMENT
/// </summary>
public class IbblStatementParser : BaseStatementParser
{
    public override string BankKey => "ibbl";
    public override string DisplayName => "Islami Bank Bangladesh Ltd.";

    public override double DetectConfidence(PdfDocument document)
    {
        var headerText = GetHeaderText(document, 30).ToUpperInvariant();

        double score = 0;

        if (headerText.Contains("ISLAMI BANK") || headerText.Contains("IBBL"))
            score += 0.5;

        if (headerText.Contains("ACCOUNT STATEMENT"))
            score += 0.3;

        if (headerText.Contains("MUDARABA") || headerText.Contains("MSA"))
            score += 0.15;

        if (headerText.Contains("TRANS DATE") && headerText.Contains("PARTICULARS"))
            score += 0.2;
        if (headerText.Contains("WITHDRAW") && headerText.Contains("DEPOSIT"))
            score += 0.15;

        return Math.Min(score, 1.0);
    }

    public override BankStatement Parse(PdfDocument document)
    {
        var statement = new BankStatement
        {
            BankName = "Islami Bank Bangladesh Limited",
            Currency = "BDT"
        };

        var headerText = GetHeaderText(document, 50);
        var allLines = document.Pages
            .SelectMany(p => p.Lines)
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .ToList();  // Keep PdfLine objects with spatial data

        var textOnly = allLines.Select(l => l.Text.Trim()).ToList();

        // --- 1. Extract account info ---
        ExtractHeaderInfo(textOnly, statement);

        // --- 2. Find table boundaries ---
        int tableStart = -1;
        int tableEnd = textOnly.Count;
        bool headerFound = false;

        for (int i = 0; i < textOnly.Count; i++)
        {
            var upper = textOnly[i].ToUpperInvariant();

            if (!headerFound && upper.Contains("TRANS DATE") && upper.Contains("PARTICULARS"))
            {
                headerFound = true;
                tableStart = i + 1;
                for (int j = i + 1; j < textOnly.Count; j++)
                {
                    var nextUpper = textOnly[j].ToUpperInvariant();
                    bool hasColKeyword = nextUpper.Contains("WITHDRAW") || nextUpper.Contains("DEPOSIT") 
                                        || nextUpper.Contains("BALANCE") || nextUpper.Contains("INSTRUMENT");
                    bool isDate = Regex.IsMatch(textOnly[j].Trim(), @"^\d{2}/\d{2}/\d{4}\s");
                    
                    if (hasColKeyword && !isDate)
                        tableStart = j + 1;
                    else
                        break;
                }
                continue;
            }

            if (tableStart > 0 && upper.Contains("TOTAL"))
            {
                ExtractTotals(textOnly[i], statement);
                tableEnd = i;
                break;
            }
        }

        if (tableStart < 0)
        {
            statement.Warnings.Add("Could not locate transaction table.");
            return statement;
        }

        // --- 3. Gather all lines in the table region (with spatial data) ---
        // Exclude page headers, footers, and non-data text
        var tableLines = allLines
            .Skip(tableStart)
            .Take(tableEnd - tableStart)
            .Where(l => !l.Text.Contains("Trans Date Particulars")
                     && !l.Text.Contains("Print Date")
                     && !l.Text.Contains("Page 1 of")
                     && !l.Text.Contains("Authorized"))
            .ToList();

        // --- 4. Identify date lines (transaction anchors) ---
        var dateLines = tableLines
            .Select((line, idx) => new { line, idx })
            .Where(x => Regex.IsMatch(x.line.Text.Trim(), @"^\d{2}/\d{2}/\d{4}\s"))
            .ToList();

        if (dateLines.Count == 0)
        {
            statement.Warnings.Add("No transaction date lines found in table.");
            return statement;
        }

        // --- 5. Assign every non-date line to the nearest date line by Y ---
        // All extra lines (description continuation, instrument numbers) are appended
        // AFTER the date line text so ParseTransactionBlock can find the date at the start.
        var txExtra = new Dictionary<int, List<string>>();
        foreach (var dl in dateLines)
            txExtra[dl.idx] = new List<string>();

        for (int i = 0; i < tableLines.Count; i++)
        {
            var line = tableLines[i];
            if (Regex.IsMatch(line.Text.Trim(), @"^\d{2}/\d{2}/\d{4}\s"))
                continue;
            if (line.Text.Trim().StartsWith("Total", StringComparison.OrdinalIgnoreCase))
                continue;

            // Find nearest date line by Y proximity
            var nearest = dateLines
                .OrderBy(dl => Math.Abs(dl.line.Y - line.Y))
                .First();
            
            txExtra[nearest.idx].Add(line.Text.Trim());
        }

        // --- 6. Parse each transaction block ---
        foreach (var dl in dateLines.OrderBy(d => d.idx))
        {
            string extraStr = txExtra[dl.idx].Count > 0 ? " " + string.Join(" ", txExtra[dl.idx]) : "";
            var block = dl.line.Text.Trim() + extraStr;
            
            var tx = ParseTransactionBlock(block);
            if (tx is not null)
                statement.Transactions.Add(tx);
        }

        // --- 7. Derive balances ---
        if (statement.Transactions.Count > 0)
        {
            statement.ClosingBalance = statement.Transactions.Last().Balance;
            statement.OpeningBalance = statement.ClosingBalance - statement.TotalCredit + statement.TotalDebit;

            if (statement.Transactions.First().Balance.HasValue)
            {
                var firstTx = statement.Transactions.First();
                var priorBalance = (firstTx.Balance ?? 0)
                    - (firstTx.Credit ?? 0) + (firstTx.Debit ?? 0);
                if (Math.Abs((statement.OpeningBalance ?? 0) - priorBalance) < 1)
                    statement.OpeningBalance = priorBalance;
            }
        }

        return statement;
    }

    private void ExtractHeaderInfo(List<string> lines, BankStatement statement)
    {
        var text = string.Join(" ", lines.Take(30));

        var accMatch = Regex.Match(text,
            @"Account\s*(?:No|Number|#)[:\s]*(\d+)", RegexOptions.IgnoreCase);
        if (accMatch.Success)
            statement.AccountNumber = accMatch.Groups[1].Value;

        var nameMatch = Regex.Match(text,
            @"Name\s+([A-Za-z\s.]+?)(?:\n|Father|Mother|Spouse|Account)",
            RegexOptions.IgnoreCase);
        if (nameMatch.Success)
            statement.AccountHolderName = nameMatch.Groups[1].Value.Trim();

        var typeMatch = Regex.Match(text,
            @"Account\s+Type\s+(.+?)(?:Currency|$)", RegexOptions.IgnoreCase);
        if (typeMatch.Success)
            statement.AccountType = typeMatch.Groups[1].Value.Trim();
        else if (text.Contains("MSA"))
            statement.AccountType = "MSA (Regular)";
        else if (text.Contains("MUDARABA"))
            statement.AccountType = "Mudaraba";

        var periodMatch = Regex.Match(text,
            @"FROM\s+DATE\s+(\d{2}/\d{2}/\d{4})\s+TO\s+(\d{2}/\d{2}/\d{4})",
            RegexOptions.IgnoreCase);
        if (periodMatch.Success)
        {
            statement.PeriodFrom = ParseDate(periodMatch.Groups[1].Value);
            statement.PeriodTo = ParseDate(periodMatch.Groups[2].Value);
        }
    }

    private void ExtractTotals(string line, BankStatement statement)
    {
    }

    private Transaction? ParseTransactionBlock(string block)
    {
        var tx = new Transaction();

        var dateMatch = Regex.Match(block, @"^(\d{2}/\d{2}/\d{4})");
        if (!dateMatch.Success)
            return null;

        tx.Date = ParseDate(dateMatch.Groups[1].Value) ?? default;
        var rest = block[dateMatch.Length..].Trim();

        var amounts = Regex.Matches(rest, @"[\d,]+\.[0-9]{2}(?![\d])");  // amounts: whole part + decimal
        
        if (amounts.Count >= 1)
        {
            if (amounts.Count >= 3)
            {
                tx.Debit = ParseAmountOrNull(amounts[^3].Value);
                tx.Credit = ParseAmountOrNull(amounts[^2].Value);
                tx.Balance = ParseAmountOrNull(amounts[^1].Value);
            }
            else if (amounts.Count >= 2)
            {
                tx.Credit = ParseAmountOrNull(amounts[^2].Value);
                tx.Balance = ParseAmountOrNull(amounts[^1].Value);
            }
            else
            {
                tx.Balance = ParseAmountOrNull(amounts[^1].Value);
            }
        }

        if (amounts.Count > 0)
        {
            int descStart = amounts[0].Index;
            int descEnd = amounts[^1].Index + amounts[^1].Length;
            
            string prefix = rest[..descStart].Trim();
            string suffix = (descEnd < rest.Length) ? rest[descEnd..].Trim() : "";
            
            // Try to extract instrument number from prefix (may be mixed with description text)
            // IBBL instrument numbers are typically 14-16 digits, appearing right before amounts
            var instrMatch = Regex.Match(prefix, @"(\d{14,})\s*$");
            if (instrMatch.Success)
            {
                tx.Reference = Regex.Replace(instrMatch.Groups[1].Value, @",", "");
                // Remove the instrument number from prefix description
                string descPrefix = prefix[..^instrMatch.Groups[1].Value.Length].Trim().Trim('-', ' ', ',');
                tx.Description = string.IsNullOrEmpty(descPrefix) ? "" : descPrefix;
                if (!string.IsNullOrEmpty(suffix))
                {
                    tx.Description = (string.IsNullOrEmpty(tx.Description) ? "" : tx.Description + " ") + suffix;
                    tx.Description = tx.Description.Trim();
                }
            }
            else
            {
                // Prefix is the actual description, suffix is postamble
                tx.Description = prefix;
                if (!string.IsNullOrEmpty(suffix))
                    tx.Description += " " + suffix;
            }
        }
        else
        {
            tx.Description = rest;
        }

        tx.Description = Regex.Replace(tx.Description, @"\s+", " ").Trim()
            .Trim('-', '|', ':', ' ');

        // If no reference found yet, try RRN/Trace patterns
        if (string.IsNullOrEmpty(tx.Reference))
        {
            // First try explicit RRN pattern
            var rrnMatch = Regex.Match(rest,
                @"(?:RRN|Ref|Instrument\s*(?:No|#))[:\s]*(\d+)", RegexOptions.IgnoreCase);
            if (rrnMatch.Success)
            {
                tx.Reference = rrnMatch.Groups[1].Value;
                tx.Description = Regex.Replace(tx.Description, 
                    @"RRN[:\s]*\d+,?\s*", "", RegexOptions.IgnoreCase).Trim();
                tx.Description = Regex.Replace(tx.Description, @"\s{2,}", " ").Trim();
            }
            else
            {
                // Fallback: try Trace number
                var traceMatch = Regex.Match(tx.Description,
                    @"(?:Trace|RRN|Ref)[:\s]*(\d+)", RegexOptions.IgnoreCase);
                if (traceMatch.Success)
                    tx.Reference = traceMatch.Groups[1].Value;
                else
                {
                    // Last resort: any 14+ digit number in the full rest is likely reference
                    var anyNumMatch = Regex.Match(rest, @"(\d{14,})");
                    if (anyNumMatch.Success)
                    {
                        tx.Reference = anyNumMatch.Groups[1].Value;
                        tx.Description = Regex.Replace(tx.Description, 
                            @"\d{14,}", "", RegexOptions.IgnoreCase).Trim();
                        tx.Description = Regex.Replace(tx.Description, @"\s{2,}", " ").Trim();
                    }
                }
            }
        }

        return tx;
    }
}
