using System.Diagnostics;

namespace StatementParser.Services.Pdf;

/// <summary>
/// PDF extractor that first tries PdfPig, and falls back to OCR for scanned PDFs.
/// </summary>
public class SmartPdfExtractor : IPdfTextExtractor
{
    private readonly IPdfTextExtractor _pdfPigExtractor;
    private readonly IOcrEngine _ocrEngine;

    public SmartPdfExtractor(
        IPdfTextExtractor pdfPigExtractor,
        IOcrEngine ocrEngine)
    {
        _pdfPigExtractor = pdfPigExtractor;
        _ocrEngine = ocrEngine;
    }

    public async Task<PdfDocument> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        // First pass: try PdfPig for text-based PDFs
        pdfStream.Position = 0;
        var pdfDoc = await _pdfPigExtractor.ExtractAsync(pdfStream, ct);

        bool hasText = pdfDoc.Pages.Any(p => p.Lines.Count > 0);

        if (hasText)
        {
            return pdfDoc; // Text-based PDF, use as-is
        }

        // No text found — try OCR fallback
        if (!_ocrEngine.IsAvailable)
        {
            pdfDoc.Warnings ??= [];
            pdfDoc.Pages.ForEach(p => pdfDoc.Warnings.Add(
                "This PDF appears to be a scanned image. " +
                "Install Tesseract OCR to extract text from scanned documents."));
            return pdfDoc;
        }

        // Convert PDF pages to images and run OCR (simplified — real impl needs PDF-to-image conversion)
        // For a production system, use PdfPig to render pages or use a library like PdfiumViewer
        pdfDoc.UsedOcrFallback = true;

        // NOTE: Full PDF-to-image conversion requires additional libraries.
        // For now, this serves as the architecture placeholder.
        pdfDoc.Warnings ??= [];
        pdfDoc.Warnings.Add("OCR processing requires PDF-to-image conversion layer. " +
            "Consider adding a library like PdfiumViewer or ImageMagick for production use.");

        return pdfDoc;
    }
}
