namespace Scraps.Application.Downloads;

public sealed record DownloadProgress(long BytesTransferred, long? TotalBytes, double? ProgressFraction = null)
{
    public double? Fraction => ProgressFraction is double fraction && double.IsFinite(fraction)
        ? Math.Clamp(fraction, 0, 1)
        : TotalBytes is > 0 ? (double)BytesTransferred / TotalBytes.Value : null;
}
