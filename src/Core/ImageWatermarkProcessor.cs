using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
namespace ImageWatermarker.Core;

public sealed class ImageWatermarkProcessor : IImageWatermarkProcessor
{
    public async Task ApplyPngWatermarkAsync(
        Stream inputImage,
        Stream watermarkImage,
        Stream outputImage,
        WatermarkOptions watermark,
        ImageWriteOptions? writeOptions = null,
        string? outputFileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputImage);
        ArgumentNullException.ThrowIfNull(watermarkImage);
        ArgumentNullException.ThrowIfNull(outputImage);
        ArgumentNullException.ThrowIfNull(watermark);

        using var image = await Image.LoadAsync<Rgba32>(inputImage, cancellationToken);
        using var overlay = await LoadAndResizeWatermarkAsync(watermarkImage, image.Size, watermark, cancellationToken);

        ApplyWatermark(image, overlay, watermark);

        var encoder = ImageEncoderResolver.Resolve(writeOptions, outputFileName);
        await image.SaveAsync(outputImage, encoder, cancellationToken);
    }

    private static async Task<Image<Rgba32>> LoadAndResizeWatermarkAsync(
        Stream watermarkImage,
        Size imageSize,
        WatermarkOptions options,
        CancellationToken cancellationToken)
    {
        if (watermarkImage.CanSeek)
        {
            watermarkImage.Position = 0;
        }

        using var overlay = await Image.LoadAsync<Rgba32>(watermarkImage, cancellationToken);
        var aspectRatio = overlay.Height == 0 ? 1f : (float)overlay.Width / overlay.Height;
        var placement = WatermarkPlacementCalculator.Calculate(imageSize, aspectRatio, options);

        overlay.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(placement.Width, placement.Height),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));

        return overlay.Clone();
    }

    private static void ApplyWatermark(Image<Rgba32> image, Image<Rgba32> overlay, WatermarkOptions options)
    {
        var aspectRatio = overlay.Height == 0 ? 1f : (float)overlay.Width / overlay.Height;
        var placement = WatermarkPlacementCalculator.Calculate(image.Size, aspectRatio, options);

        image.Mutate(context => context.DrawImage(
            overlay,
            new Point(placement.X, placement.Y),
            1f));
    }
}
