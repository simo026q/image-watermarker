namespace ImageWatermarker.Core;

public sealed class WatermarkOptions
{
    public WatermarkPosition Position { get; init; } = WatermarkPosition.BottomRight;
    public float SizeRatio { get; init; } = 0.2f;
    public float MarginRatio { get; init; } = 0.035f;
    public float? HorizontalPositionRatio { get; init; }
    public float? VerticalPositionRatio { get; init; }
}
