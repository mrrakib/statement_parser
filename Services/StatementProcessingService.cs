using StatementParser.Models;
using StatementParser.Services.Parsers;
using StatementParser.Services.Pdf;

namespace StatementParser.Services;

/// <summary>
/// Orchestrator — coordinates PDF extraction → bank detection → parsing.
/// </summary>
public class StatementProcessingService
{
    private readonly IPdfTextExtractor _textExtractor;
    private readonly ParserRegistry _parserRegistry;

    public StatementProcessingService(
        IPdfTextExtractor textExtractor,
        ParserRegistry parserRegistry)
    {
        _textExtractor = textExtractor;
        _parserRegistry = parserRegistry;
    }

    /// <summary>
    /// Full processing pipeline: upload stream → extract → detect → parse.
    /// </summary>
    public async Task<ProcessingResult> ProcessAsync(
        Stream pdfStream,
        string? forceBankKey = null,
        CancellationToken ct = default)
    {
        var result = new ProcessingResult();

        try
        {
            // Step 1: Extract text with positional layout
            result.PdfDocument = await _textExtractor.ExtractAsync(pdfStream, ct);
            result.HasTextContent = result.PdfDocument.Pages.Any(p => p.Lines.Count > 0);

            // Step 2: Detect bank parser
            IBankStatementParser parser;

            if (!string.IsNullOrEmpty(forceBankKey))
            {
                parser = _parserRegistry.AllParsers
                    .FirstOrDefault(p =>
                        p.BankKey.Equals(forceBankKey, StringComparison.OrdinalIgnoreCase))
                    ?? throw new UnsupportedStatementException(
                        $"Unknown bank key: {forceBankKey}");
            }
            else
            {
                parser = _parserRegistry.Resolve(result.PdfDocument);
            }

            result.DetectedBank = parser.DisplayName;
            result.BankKey = parser.BankKey;
            result.Confidence = parser.DetectConfidence(result.PdfDocument);

            // Step 3: Parse into common model
            result.Statement = parser.Parse(result.PdfDocument);
            result.Success = true;
        }
        catch (UnsupportedStatementException)
        {
            // Return available banks for manual selection
            result.AvailableBanks = _parserRegistry.AllParsers
                .Select(p => new BankOption
                {
                    BankKey = p.BankKey,
                    DisplayName = p.DisplayName
                }).ToList();
            throw;
        }
        catch (Exception ex) when (ex is not UnsupportedStatementException)
        {
            result.ErrorMessage = $"Processing failed: {ex.Message}";
            result.Success = false;
        }

        return result;
    }
}

public class ProcessingResult
{
    public bool Success { get; set; }
    public string? DetectedBank { get; set; }
    public string? BankKey { get; set; }
    public double Confidence { get; set; }
    public BankStatement? Statement { get; set; }
    public PdfDocument? PdfDocument { get; set; }
    public bool HasTextContent { get; set; }
    public string? ErrorMessage { get; set; }
    public List<BankOption>? AvailableBanks { get; set; }
}

public class BankOption
{
    public string BankKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
