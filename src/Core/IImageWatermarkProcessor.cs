namespace ImageWatermarker.Core;

public interface IImageWatermarkProcessor
{
    Task ApplySvgWatermarkAsync(
        Stream inputImage,
        Stream watermarkSvg,
        Stream outputImage,
        SvgWatermarkOptions watermark,
        ImageWriteOptions? writeOptions = null,
        string? outputFileName = null,
        CancellationToken cancellationToken = default);
}
