using SixLabors.ImageSharp;

namespace ImageWatermarker.Core;

internal static class SvgWatermarkPlacementCalculator
{
    public static SvgWatermarkPlacement Calculate(
        Size imageSize,
        float watermarkAspectRatio,
        SvgWatermarkOptions options)
    {
        var margin = MathF.Max(8f, MathF.Min(imageSize.Width, imageSize.Height) * options.MarginRatio);
        var maxWidth = MathF.Max(1f, imageSize.Width * options.MaxWidthRatio);
        var maxHeight = MathF.Max(1f, imageSize.Height * options.MaxHeightRatio);

        var widthFromHeight = maxHeight * watermarkAspectRatio;
        var finalWidth = MathF.Min(maxWidth, widthFromHeight);
        var finalHeight = finalWidth / watermarkAspectRatio;

        if (finalHeight > maxHeight)
        {
            finalHeight = maxHeight;
            finalWidth = finalHeight * watermarkAspectRatio;
        }

        var x = options.Position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.BottomLeft => margin,
            WatermarkPosition.TopRight or WatermarkPosition.BottomRight => imageSize.Width - finalWidth - margin,
            _ => (imageSize.Width - finalWidth) / 2f
        };

        var y = options.Position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.TopRight => margin,
            WatermarkPosition.BottomLeft or WatermarkPosition.BottomRight => imageSize.Height - finalHeight - margin,
            _ => (imageSize.Height - finalHeight) / 2f
        };

        return new SvgWatermarkPlacement(
            Math.Max(1, (int)MathF.Round(finalWidth)),
            Math.Max(1, (int)MathF.Round(finalHeight)),
            Math.Max(0, (int)MathF.Round(x)),
            Math.Max(0, (int)MathF.Round(y)));
    }
}
