using ImageWatermarker.Core;

try
{
    var invocation = args.Length == 0
        ? PromptForInvocation()
        : ParseArguments(args);

    if (invocation.ShowHelp)
    {
        WriteHelp();
        return;
    }

    var processor = new ImageWatermarkProcessor();
    var targets = ResolveTargets(invocation).ToList();
    var watermarkSvgBytes = await File.ReadAllBytesAsync(invocation.WatermarkSvgPath);

    if (targets.Count == 0)
    {
        Console.Error.WriteLine("No supported image files were found to process.");
        return;
    }

    foreach (var target in targets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target.OutputPath)!);

        await using var input = File.OpenRead(target.InputPath);
        await using var watermarkSvg = new MemoryStream(watermarkSvgBytes, writable: false);
        await using var output = File.Create(target.OutputPath);

        await processor.ApplySvgWatermarkAsync(
            input,
            watermarkSvg,
            output,
            new SvgWatermarkOptions
            {
                Position = invocation.Position
            },
            new ImageWriteOptions
            {
                OutputFormat = invocation.OutputFormat,
                JpegQuality = invocation.JpegQuality
            },
            target.OutputPath);

        Console.WriteLine($"Watermarked: {target.InputPath} -> {target.OutputPath}");
    }
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}

return;

static CliInvocation ParseArguments(IReadOnlyList<string> args)
{
    var invocation = new CliInvocation();
    var positionals = new List<string>();

    for (var index = 0; index < args.Count; index++)
    {
        var current = args[index];

        if (!current.StartsWith('-'))
        {
            positionals.Add(current);
            continue;
        }

        string NextValue()
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Missing value for {current}.");
            }

            index++;
            return args[index];
        }

        switch (current)
        {
            case "--help":
            case "-h":
                invocation.ShowHelp = true;
                return invocation;
            case "--watermark":
            case "-w":
                invocation.WatermarkSvgPath = NextValue();
                break;
            case "--position":
            case "-p":
                invocation.Position = ParsePosition(NextValue());
                break;
            case "--format":
                invocation.OutputFormat = ParseFormat(NextValue());
                break;
            case "--jpeg-quality":
                invocation.JpegQuality = ParseJpegQuality(NextValue());
                break;
            case "--recursive":
            case "-r":
                invocation.Recursive = true;
                break;
            default:
                throw new ArgumentException($"Unknown option: {current}");
        }
    }

    if (positionals.Count == 0)
    {
        throw new ArgumentException("An input file or folder path is required.");
    }

    invocation.InputPath = positionals[0];
    invocation.OutputPath = positionals.Count > 1 ? positionals[1] : null;

    if (string.IsNullOrWhiteSpace(invocation.WatermarkSvgPath))
    {
        throw new ArgumentException("A watermark SVG path is required. Use --watermark logo.svg.");
    }

    if (!File.Exists(invocation.WatermarkSvgPath))
    {
        throw new ArgumentException($"Watermark SVG file not found: {invocation.WatermarkSvgPath}");
    }

    return invocation;
}

static CliInvocation PromptForInvocation()
{
    Console.WriteLine("Interactive watermark setup");
    Console.WriteLine("Press Enter to accept defaults.");
    Console.WriteLine();

    var inputPath = PromptRequired("Input image or folder path");
    var watermarkSvgPath = PromptRequired("Watermark SVG path");
    var outputFormat = ParseFormat(Prompt("Output format", "png"));
    var defaultOutputPath = BuildDefaultOutputPath(inputPath, outputFormat);
    var outputPath = Prompt("Output path", defaultOutputPath);
    var position = ParsePosition(Prompt("Position", "bottom-right"));
    var recursive = Directory.Exists(inputPath) && ParseBoolean(Prompt("Recursive folder scan", "yes"));
    var jpegQualityText = Prompt("JPEG quality", "100");

    return new CliInvocation
    {
        InputPath = inputPath,
        OutputPath = outputPath,
        WatermarkSvgPath = watermarkSvgPath,
        Position = position,
        OutputFormat = outputFormat,
        Recursive = recursive,
        JpegQuality = ParseJpegQuality(jpegQualityText)
    };
}

static IEnumerable<ProcessingTarget> ResolveTargets(CliInvocation invocation)
{
    if (File.Exists(invocation.InputPath))
    {
        yield return new ProcessingTarget(
            Path.GetFullPath(invocation.InputPath),
            invocation.OutputPath is null
                ? BuildDefaultOutputPath(invocation.InputPath, invocation.OutputFormat)
                : Path.GetFullPath(invocation.OutputPath));
        yield break;
    }

    if (!Directory.Exists(invocation.InputPath))
    {
        throw new ArgumentException($"Input path not found: {invocation.InputPath}");
    }

    var inputRoot = Path.GetFullPath(invocation.InputPath);
    var outputRoot = invocation.OutputPath is null
        ? BuildDefaultOutputPath(invocation.InputPath, invocation.OutputFormat)
        : Path.GetFullPath(invocation.OutputPath);
    var searchOption = invocation.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

    foreach (var file in Directory.EnumerateFiles(inputRoot, "*.*", searchOption).Where(IsSupportedInputFile))
    {
        var relativePath = Path.GetRelativePath(inputRoot, file);
        var outputFile = Path.Combine(outputRoot, ChangeExtension(relativePath, invocation.OutputFormat));
        yield return new ProcessingTarget(file, outputFile);
    }
}

static bool IsSupportedInputFile(string path)
{
    var extension = Path.GetExtension(path).ToLowerInvariant();
    return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
}

static string ChangeExtension(string relativePath, ImageOutputFormat outputFormat)
{
    var extension = outputFormat switch
    {
        ImageOutputFormat.Auto => Path.GetExtension(relativePath),
        ImageOutputFormat.Jpeg => ".jpg",
        ImageOutputFormat.Bmp => ".bmp",
        ImageOutputFormat.Gif => ".gif",
        _ => ".png"
    };

    return Path.ChangeExtension(relativePath, extension);
}

static string PromptRequired(string label)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var value = Console.ReadLine()?.Trim();

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }
}

static string Prompt(string label, string defaultValue)
{
    Console.Write($"{label} [{defaultValue}]: ");
    var value = Console.ReadLine()?.Trim();
    return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
}

static bool ParseBoolean(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "y" or "yes" or "true" or "1" => true,
        "n" or "no" or "false" or "0" => false,
        _ => throw new ArgumentException($"Unsupported boolean value: {value}")
    };
}

static void WriteHelp()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  image-watermarker <input-file> [output-file] --watermark <watermark.svg> [options]");
    Console.WriteLine("  image-watermarker <input-folder> [output-folder] --watermark <watermark.svg> [options]");
    Console.WriteLine();
    Console.WriteLine("If you run the tool without arguments, it starts in interactive mode.");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --watermark, -w      Path to the SVG watermark file");
    Console.WriteLine("  --position, -p       top-left | top-right | bottom-left | bottom-right | center");
    Console.WriteLine("  --format             png | jpeg | bmp | gif | auto");
    Console.WriteLine("  --jpeg-quality       1-100, used only for JPEG output");
    Console.WriteLine("  --recursive, -r      Scan subfolders when input is a folder");
    Console.WriteLine("  --help, -h           Show help");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  image-watermarker photo.jpg --watermark logo.svg");
    Console.WriteLine("  image-watermarker photos branded --watermark logo.svg --recursive");
    Console.WriteLine("  image-watermarker photo.jpg branded.jpg --watermark logo.svg --format jpeg --jpeg-quality 100");
}

static WatermarkPosition ParsePosition(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "top-left" => WatermarkPosition.TopLeft,
        "top-right" => WatermarkPosition.TopRight,
        "bottom-left" => WatermarkPosition.BottomLeft,
        "bottom-right" => WatermarkPosition.BottomRight,
        "center" => WatermarkPosition.Center,
        _ => throw new ArgumentException($"Unsupported watermark position: {value}")
    };
}

static ImageOutputFormat ParseFormat(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "auto" => ImageOutputFormat.Auto,
        "png" => ImageOutputFormat.Png,
        "jpg" or "jpeg" => ImageOutputFormat.Jpeg,
        "bmp" => ImageOutputFormat.Bmp,
        "gif" => ImageOutputFormat.Gif,
        _ => throw new ArgumentException($"Unsupported output format: {value}")
    };
}

static int ParseJpegQuality(string value)
{
    if (!int.TryParse(value, out var quality) || quality is < 1 or > 100)
    {
        throw new ArgumentException($"Invalid JPEG quality: {value}");
    }

    return quality;
}

static string BuildDefaultOutputPath(string inputPath, ImageOutputFormat outputFormat)
{
    if (Directory.Exists(inputPath))
    {
        var fullPath = Path.GetFullPath(inputPath);
        var parent = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        return Path.Combine(parent, $"{Path.GetFileName(fullPath)}-watermarked");
    }

    var directory = Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? Environment.CurrentDirectory;
    var name = Path.GetFileNameWithoutExtension(inputPath);
    var extension = outputFormat switch
    {
        ImageOutputFormat.Auto => ".png",
        ImageOutputFormat.Jpeg => ".jpg",
        ImageOutputFormat.Bmp => ".bmp",
        ImageOutputFormat.Gif => ".gif",
        _ => ".png"
    };

    return Path.Combine(directory, $"{name}.watermarked{extension}");
}

internal sealed class CliInvocation
{
    public bool ShowHelp { get; set; }

    public string InputPath { get; set; } = string.Empty;

    public string? OutputPath { get; set; }

    public string WatermarkSvgPath { get; set; } = string.Empty;

    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;

    public ImageOutputFormat OutputFormat { get; set; } = ImageOutputFormat.Auto;

    public bool Recursive { get; set; }

    public int JpegQuality { get; set; } = 100;
}

internal sealed record ProcessingTarget(string InputPath, string OutputPath);
