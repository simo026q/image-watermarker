using SkiaSharp;
using Svg.Skia;

namespace ImageWatermarker.Core;

internal static class SvgRasterizer
{
    public static (byte[] PngBytes, float AspectRatio) RasterizePng(Stream svgStream, int width, int height)
    {
        using var skSvg = new SKSvg();
        var picture = skSvg.Load(svgStream) ?? throw new InvalidOperationException("Unable to load watermark SVG.");
        var bounds = picture.CullRect;
        var sourceWidth = bounds.Width <= 0 ? 1f : bounds.Width;
        var sourceHeight = bounds.Height <= 0 ? 1f : bounds.Height;
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Unable to create drawing surface for watermark SVG.");

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var scaleX = width / sourceWidth;
        var scaleY = height / sourceHeight;
        canvas.Scale(scaleX, scaleY);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return (data.ToArray(), sourceWidth / sourceHeight);
    }

    public static float GetAspectRatio(Stream svgStream)
    {
        using var skSvg = new SKSvg();
        var picture = skSvg.Load(svgStream) ?? throw new InvalidOperationException("Unable to load watermark SVG.");
        var bounds = picture.CullRect;
        var sourceWidth = bounds.Width <= 0 ? 1f : bounds.Width;
        var sourceHeight = bounds.Height <= 0 ? 1f : bounds.Height;
        return sourceWidth / sourceHeight;
    }
}
