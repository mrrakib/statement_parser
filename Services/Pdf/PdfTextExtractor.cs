using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace StatementParser.Services.Pdf;

/// <summary>
/// Extracts layout-preserved text from PDFs using PdfPig.
/// Groups words by Y-coordinate to reconstruct table rows with column positions.
/// </summary>
public class PdfTextExtractor : IPdfTextExtractor
{
    private const double YTolerance = 3.0; // pixels tolerance for grouping words on same line

    public PdfDocument Extract(string pdfPath)
    {
        using var stream = File.OpenRead(pdfPath);
        return Extract(stream, CancellationToken.None);
    }

    public async Task<PdfDocument> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        // PdfPig doesn't have async API, but we wrap in Task.Run to keep interface async-friendly
        return await Task.Run(() => Extract(pdfStream, ct), ct);
    }

    public PdfDocument Extract(Stream pdfStream, CancellationToken ct)
    {
        var result = new PdfDocument();

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfStream);

        result.PageCount = pdf.NumberOfPages;

        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var pdfPage = new PdfPage { PageNumber = page.Number };

            // Group words by Y position to form lines
            var words = page.GetWords().ToList();

            if (words.Count == 0)
            {
                // No extractable text — this is a scanned/image PDF
                result.Pages.Add(pdfPage);
                continue;
            }

            var lineGroups = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom / YTolerance) * YTolerance)
                .OrderByDescending(g => g.Key) // PDF coordinates: top of page = higher Y
                .ToList();

            foreach (var group in lineGroups)
            {
                var sortedWords = group.OrderBy(w => w.BoundingBox.Left).ToList();

                pdfPage.Lines.Add(new PdfLine
                {
                    Text = string.Join(" ", sortedWords.Select(w => w.Text)),
                    Y = group.Key,
                    MinX = sortedWords.First().BoundingBox.Left,
                    MaxX = sortedWords.Last().BoundingBox.Right
                });
            }

            result.Pages.Add(pdfPage);
        }

        // Check if the whole document had no text
        bool hadAnyText = result.Pages.Any(p => p.Lines.Count > 0);

        return result;
    }
}
