using System.Globalization;
using System.Text.RegularExpressions;
using StatementParser.Models;
using StatementParser.Services.Pdf;

namespace StatementParser.Services.Parsers;

/// <summary>
/// Base class with shared parsing utilities for all bank parsers.
/// </summary>
public abstract class BaseStatementParser : IBankStatementParser
{
    public abstract string BankKey { get; }
    public abstract string DisplayName { get; }
    public abstract double DetectConfidence(PdfDocument document);
    public abstract BankStatement Parse(PdfDocument document);

    /// <summary>
    /// Try to parse a date string in common Bangladesh statement formats.
    /// </summary>
    protected static DateOnly? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        input = input.Trim();

        string[] formats =
        [
            "dd/MM/yyyy", "d/M/yyyy",
            "dd-MM-yyyy", "d-M-yyyy",
            "yyyy-MM-dd",
            "dd MMM yyyy", "dd MMMM yyyy",
            "dd-MMM-yyyy", "d-MMM-yyyy",
            "MM/dd/yyyy" // fallback
        ];

        if (DateOnly.TryParseExact(input, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return date;

        // Try flexible parse as last resort
        if (DateOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        return null;
    }

    /// <summary>
    /// Parse a BDT amount string like "1,234.56" or "১২৩৪.৫৬" or "(1,234.56)" for negative.
    /// </summary>
    protected static decimal? ParseAmount(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        input = input.Trim()
            .Replace(",", "")
            .Replace(" ", "")
            .Replace("(", "-")
            .Replace(")", "")
            .Replace("BDT", "")
            .Replace("TK", "")
            .Replace("Tk", "")
            .Replace("টাকা", "")
            .Trim();

        // Handle Bengali digits (০১২৩৪৫৬৭৮৯)
        input = ConvertBengaliDigits(input);

        if (string.IsNullOrEmpty(input)) return null;

        // Handle negative indicator
        bool isNegative = input.StartsWith('-') || input.StartsWith('(');
        input = input.TrimStart('(', '-').TrimEnd(')').Trim();

        if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return isNegative ? -value : value;
        }

        return null;
    }

    /// <summary>
    /// Safe version: returns null for empty/non-numeric instead of 0.
    /// </summary>
    protected static decimal? ParseAmountOrNull(string? input)
    {
        var result = ParseAmount(input);
        if (result.HasValue && result.Value == 0)
        {
            // Only return 0 if the input actually contained a number
            if (!string.IsNullOrWhiteSpace(input) && Regex.IsMatch(input.Trim(), @"[0-9০-৯]"))
                return result;
            return null;
        }
        return result;
    }

    /// <summary>
    /// Get all text from all pages as a single string (for detection/pattern matching).
    /// </summary>
    protected static string GetAllText(PdfDocument document)
    {
        return string.Join("\n", document.Pages
            .SelectMany(p => p.Lines)
            .Select(l => l.Text));
    }

    /// <summary>
    /// Get text from the first N lines (head of document — for header detection).
    /// </summary>
    protected static string GetHeaderText(PdfDocument document, int lineCount = 30)
    {
        return string.Join("\n", document.Pages
            .FirstOrDefault()?.Lines
            ?.Take(lineCount)
            ?.Select(l => l.Text) ?? []);
    }

    private static string ConvertBengaliDigits(string input)
    {
        var bengaliDigits = new Dictionary<char, char>
        {
            {'০', '0'}, {'১', '1'}, {'২', '2'}, {'৩', '3'}, {'৪', '4'},
            {'৫', '5'}, {'৬', '6'}, {'৭', '7'}, {'৮', '8'}, {'৯', '9'}
        };

        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (bengaliDigits.TryGetValue(chars[i], out var latin))
                chars[i] = latin;
        }
        return new string(chars);
    }

    /// <summary>
    /// Detect a table region in the page by finding the header row and
    /// returning all rows below it until a footer pattern is found.
    /// </summary>
    protected static List<PdfLine> ExtractTableRows(PdfPage page, string[] headerKeywords)
    {
        var lines = page.Lines;
        int headerIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Text.ToLowerInvariant();
            if (headerKeywords.Any(k => text.Contains(k.ToLowerInvariant())))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            // No header found — assume all lines after account info are table rows
            // (skip first 5-8 lines which are usually header info)
            headerIndex = Math.Min(8, lines.Count - 1);
        }

        // Collect rows after header until we hit a footer pattern
        var rows = new List<PdfLine>();
        string[] footerPatterns =
        [
            "total", "মোট", "summary", "closing", "page", "continue",
            "thank", "ধন্যবাদ", "authorized", "signature", "সই"
        ];

        for (int i = headerIndex + 1; i < lines.Count; i++)
        {
            var text = lines[i].Text.ToLowerInvariant().Trim();

            if (string.IsNullOrWhiteSpace(text)) continue;

            // Skip footer lines
            if (footerPatterns.Any(f => text.Contains(f))) break;

            rows.Add(lines[i]);
        }

        return rows;
    }
}
