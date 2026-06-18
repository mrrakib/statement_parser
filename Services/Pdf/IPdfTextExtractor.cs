namespace StatementParser.Services.Pdf;

/// <summary>
/// Extracts layout-preserved text from PDF documents.
/// Handles both text-based PDFs (via PdfPig) and scanned/image PDFs (via OCR).
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// Extract text content from a PDF stream, preserving positional layout.
    /// </summary>
    Task<PdfDocument> ExtractAsync(Stream pdfStream, CancellationToken ct = default);
}

public class PdfDocument
{
    public int PageCount { get; set; }
    public List<PdfPage> Pages { get; set; } = [];
    public bool UsedOcrFallback { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class PdfPage
{
    public int PageNumber { get; set; }
    public List<PdfLine> Lines { get; set; } = [];
}

public class PdfLine
{
    public string Text { get; set; } = string.Empty;
    public double Y { get; set; }     // vertical position — groups by row
    public double MinX { get; set; }  // left edge
    public double MaxX { get; set; }  // right edge
}
