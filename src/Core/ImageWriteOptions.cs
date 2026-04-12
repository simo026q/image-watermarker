namespace ImageWatermarker.Core;

public sealed class ImageWriteOptions
{
    public ImageOutputFormat OutputFormat { get; init; } = ImageOutputFormat.Auto;

    public int JpegQuality { get; init; } = 100;
}
