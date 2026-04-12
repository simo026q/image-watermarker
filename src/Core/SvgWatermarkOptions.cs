namespace ImageWatermarker.Core;

public sealed class SvgWatermarkOptions
{
    public WatermarkPosition Position { get; init; } = WatermarkPosition.BottomRight;

    public float MaxWidthRatio { get; init; } = 0.28f;

    public float MaxHeightRatio { get; init; } = 0.18f;

    public float MarginRatio { get; init; } = 0.035f;

    public float Opacity { get; init; } = 0.9f;
}
