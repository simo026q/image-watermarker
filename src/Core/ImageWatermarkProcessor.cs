using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageWatermarker.Core;

public sealed class ImageWatermarkProcessor : IImageWatermarkProcessor
{
    public async Task ApplySvgWatermarkAsync(
        Stream inputImage,
        Stream watermarkSvg,
        Stream outputImage,
        SvgWatermarkOptions watermark,
        ImageWriteOptions? writeOptions = null,
        string? outputFileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputImage);
        ArgumentNullException.ThrowIfNull(watermarkSvg);
        ArgumentNullException.ThrowIfNull(outputImage);
        ArgumentNullException.ThrowIfNull(watermark);

        using var image = await Image.LoadAsync<Rgba32>(inputImage, cancellationToken);

        ApplyWatermark(image, watermarkSvg, watermark);

        var encoder = ImageEncoderResolver.Resolve(writeOptions, outputFileName);
        await image.SaveAsync(outputImage, encoder, cancellationToken);
    }

    private static void ApplyWatermark(Image<Rgba32> image, Stream watermarkSvg, SvgWatermarkOptions options)
    {
        if (watermarkSvg.CanSeek)
        {
            watermarkSvg.Position = 0;
        }

        using var svgBuffer = new MemoryStream();
        watermarkSvg.CopyTo(svgBuffer);
        var svgBytes = svgBuffer.ToArray();

        using var aspectRatioStream = new MemoryStream(svgBytes, writable: false);
        var aspectRatio = SvgRasterizer.GetAspectRatio(aspectRatioStream);
        var placement = SvgWatermarkPlacementCalculator.Calculate(image.Size, aspectRatio, options);

        using var rasterizeStream = new MemoryStream(svgBytes, writable: false);
        var (overlayPng, _) = SvgRasterizer.RasterizePng(rasterizeStream, placement.Width, placement.Height);

        using var overlayStream = new MemoryStream(overlayPng);
        using var overlay = Image.Load<Rgba32>(overlayStream);

        image.Mutate(context => context.DrawImage(
            overlay,
            new Point(placement.X, placement.Y),
            1f));
    }
}
