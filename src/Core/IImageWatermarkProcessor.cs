namespace ImageWatermarker.Core;

public interface IImageWatermarkProcessor
{
    Task ApplyPngWatermarkAsync(
        Stream inputImage,
        Stream watermarkImage,
        Stream outputImage,
        WatermarkOptions watermark,
        ImageWriteOptions? writeOptions = null,
        string? outputFileName = null,
        CancellationToken cancellationToken = default);
}
