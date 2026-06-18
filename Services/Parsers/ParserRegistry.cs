using StatementParser.Services.Pdf;

namespace StatementParser.Services.Parsers;

/// <summary>
/// Auto-detects the best parser for a given document using confidence scoring.
/// Falls back to asking the user if no parser is confident enough.
/// </summary>
public class ParserRegistry
{
    private readonly IEnumerable<IBankStatementParser> _parsers;

    public ParserRegistry(IEnumerable<IBankStatementParser> parsers)
    {
        _parsers = parsers;
    }

    /// <summary>
    /// Returns all registered parsers (for manual selection fallback).
    /// </summary>
    public IReadOnlyList<IBankStatementParser> AllParsers => _parsers.ToList();

    /// <summary>
    /// Auto-detect the best parser. Throws if none is confident enough.
    /// </summary>
    public IBankStatementParser Resolve(PdfDocument document)
    {
        var scored = _parsers
            .Select(p => (Parser: p, Confidence: p.DetectConfidence(document)))
            .OrderByDescending(x => x.Confidence)
            .ToList();

        var best = scored.FirstOrDefault();

        if (best.Parser is null || best.Confidence < 0.3)
        {
            // No parser confident enough — return null so the controller can
            // fall back to manual bank selection in the UI.
            throw new UnsupportedStatementException(
                "Could not identify the bank statement format. " +
                $"Best candidate: {best.Parser?.DisplayName ?? "none"} " +
                $"(confidence: {best.Confidence:P1}). " +
                "Please select your bank manually.");
        }

        return best.Parser;
    }

    /// <summary>
    /// Get candidates with confidence > threshold (for top-N suggestions).
    /// </summary>
    public List<(IBankStatementParser Parser, double Confidence)> GetCandidates(
        PdfDocument document, double threshold = 0.1)
    {
        return _parsers
            .Select(p => (Parser: p, Confidence: p.DetectConfidence(document)))
            .Where(x => x.Confidence >= threshold)
            .OrderByDescending(x => x.Confidence)
            .ToList();
    }
}

public class UnsupportedStatementException : Exception
{
    public UnsupportedStatementException(string message) : base(message) { }
    public UnsupportedStatementException(string message, Exception inner) : base(message, inner) { }
}
