using StatementParser.Models;
using StatementParser.Services.Pdf;

namespace StatementParser.Services.Parsers;

/// <summary>
/// Strategy contract — each bank format gets its own parser implementation.
/// </summary>
public interface IBankStatementParser
{
    /// <summary>Unique key, e.g. "ibbl", "dutch-bangla", "city-bank".</summary>
    string BankKey { get; }

    /// <summary>Display name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Confidence score 0.0–1.0 that this parser can handle the document.
    /// Parsers examine headers, logos, or known phrases to decide.
    /// </summary>
    double DetectConfidence(PdfDocument document);

    /// <summary>Parse the extracted PDF content into the normalized model.</summary>
    BankStatement Parse(PdfDocument document);
}
