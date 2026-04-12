using SixLabors.ImageSharp;

namespace ImageWatermarker.Core;

internal static class WatermarkPlacementCalculator
{
    public static WatermarkPlacement Calculate(
        Size imageSize,
        float watermarkAspectRatio,
        WatermarkOptions options)
    {
        var margin = MathF.Max(8f, MathF.Min(imageSize.Width, imageSize.Height) * options.MarginRatio);
        var sizeRatio = Math.Clamp(options.SizeRatio, 0f, 1f);
        var availableWidth = MathF.Max(0f, imageSize.Width - (margin * 2f));
        var availableHeight = MathF.Max(0f, imageSize.Height - (margin * 2f));
        var normalizedAspectRatio = watermarkAspectRatio <= 0f ? 1f : watermarkAspectRatio;
        var targetLongSide = options.SizeHandling switch
        {
            // Keep the existing behavior so rotated images preserve the same apparent scale.
            WatermarkSizeHandling.Relative => MathF.Min(availableWidth, availableHeight) * sizeRatio,
            WatermarkSizeHandling.Width => availableWidth * sizeRatio,
            _ => MathF.Min(availableWidth, availableHeight) * sizeRatio
        };

        var finalWidth = normalizedAspectRatio >= 1f
            ? targetLongSide
            : targetLongSide * normalizedAspectRatio;
        var finalHeight = normalizedAspectRatio >= 1f
            ? finalWidth / normalizedAspectRatio
            : targetLongSide;

        if (finalWidth > availableWidth)
        {
            finalWidth = availableWidth;
            finalHeight = finalWidth / normalizedAspectRatio;
        }

        if (finalHeight > availableHeight)
        {
            finalHeight = availableHeight;
            finalWidth = finalHeight * normalizedAspectRatio;
        }

        var x = options.Position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.MiddleLeft or WatermarkPosition.BottomLeft => margin,
            WatermarkPosition.TopRight or WatermarkPosition.MiddleRight or WatermarkPosition.BottomRight => imageSize.Width - finalWidth - margin,
            _ => (imageSize.Width - finalWidth) / 2f
        };

        var y = options.Position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.TopCenter or WatermarkPosition.TopRight => margin,
            WatermarkPosition.BottomLeft or WatermarkPosition.BottomCenter or WatermarkPosition.BottomRight => imageSize.Height - finalHeight - margin,
            _ => (imageSize.Height - finalHeight) / 2f
        };

        var horizontalTravel = MathF.Max(0f, imageSize.Width - finalWidth - (margin * 2f));
        var verticalTravel = MathF.Max(0f, imageSize.Height - finalHeight - (margin * 2f));

        if (options.HorizontalPositionRatio is float horizontalPositionRatio)
        {
            x = margin + (horizontalTravel * Math.Clamp(horizontalPositionRatio, 0f, 1f));
        }

        if (options.VerticalPositionRatio is float verticalPositionRatio)
        {
            y = margin + (verticalTravel * Math.Clamp(verticalPositionRatio, 0f, 1f));
        }

        return new WatermarkPlacement(
            Math.Max(1, (int)MathF.Round(finalWidth)),
            Math.Max(1, (int)MathF.Round(finalHeight)),
            Math.Max(0, (int)MathF.Round(x)),
            Math.Max(0, (int)MathF.Round(y)));
    }
}
