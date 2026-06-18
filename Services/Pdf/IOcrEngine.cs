namespace StatementParser.Services.Pdf;

/// <summary>
/// OCR engine abstraction for scanned/image-based PDFs.
/// Implementations can use Tesseract, Windows.Media.Ocr, or cloud APIs.
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Perform OCR on an image stream and return recognized text lines.
    /// </summary>
    Task<OcrResult> RecognizeAsync(Stream imageStream, string language = "eng+ben", CancellationToken ct = default);

    /// <summary>
    /// Whether this OCR engine is available on the current system.
    /// </summary>
    bool IsAvailable { get; }
}

public class OcrResult
{
    public string FullText { get; set; } = string.Empty;
    public List<OcrLine> Lines { get; set; } = [];
    public double Confidence { get; set; }
    public bool Success { get; set; }
}

public class OcrLine
{
    public string Text { get; set; } = string.Empty;
    public double Y { get; set; }
    public double MinX { get; set; }
    public double MaxX { get; set; }
}
