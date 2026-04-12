using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace ImageWatermarker.Core;

internal static class ImageEncoderResolver
{
    public static IImageEncoder Resolve(ImageWriteOptions? options, string? outputFileName)
    {
        var resolvedFormat = options?.OutputFormat ?? ImageOutputFormat.Auto;

        if (resolvedFormat is ImageOutputFormat.Auto)
        {
            resolvedFormat = ResolveFromFileName(outputFileName);
        }

        return resolvedFormat switch
        {
            ImageOutputFormat.Jpeg => new JpegEncoder
            {
                Quality = Math.Clamp(options?.JpegQuality ?? 100, 1, 100)
            },
            ImageOutputFormat.Bmp => new BmpEncoder(),
            ImageOutputFormat.Gif => new GifEncoder(),
            _ => new PngEncoder()
        };
    }

    private static ImageOutputFormat ResolveFromFileName(string? outputFileName)
    {
        var extension = Path.GetExtension(outputFileName) ?? string.Empty;

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageOutputFormat.Jpeg,
            ".bmp" => ImageOutputFormat.Bmp,
            ".gif" => ImageOutputFormat.Gif,
            ".png" => ImageOutputFormat.Png,
            _ => ImageOutputFormat.Png
        };
    }
}
