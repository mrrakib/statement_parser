namespace StatementParser.Services.Pdf;

/// <summary>
/// No-op OCR engine — used when Tesseract is not installed.
/// Provides clear guidance on how to enable OCR.
/// </summary>
public class NullOcrEngine : IOcrEngine
{
    public bool IsAvailable => false;

    public Task<OcrResult> RecognizeAsync(
        Stream imageStream,
        string language = "eng+ben",
        CancellationToken ct = default)
    {
        return Task.FromResult(new OcrResult
        {
            Success = false,
            FullText = string.Empty,
            Confidence = 0,
            Lines = []
        });
    }
}
