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
    var overwriteBehavior = OverwriteBehavior.Ask;
    var watermarkPngBytes = await File.ReadAllBytesAsync(invocation.WatermarkPngPath);

    if (targets.Count == 0)
    {
        Console.Error.WriteLine("No supported image files were found to process.");
        return;
    }

    foreach (var target in targets)
    {
        if (PathsMatch(target.InputPath, target.OutputPath))
        {
            throw new ArgumentException($"Output path cannot be the same as input path: {target.InputPath}");
        }

        if (!ShouldWriteTarget(target.OutputPath, invocation.Force, ref overwriteBehavior))
        {
            Console.WriteLine($"Skipped existing file: {target.OutputPath}");
            continue;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target.OutputPath)!);

        await using var input = File.OpenRead(target.InputPath);
        await using var watermarkImage = new MemoryStream(watermarkPngBytes, writable: false);
        await using var output = File.Create(target.OutputPath);

        await processor.ApplyPngWatermarkAsync(
            input,
            watermarkImage,
            output,
            new WatermarkOptions
            {
                Position = invocation.Position,
                SizeHandling = invocation.SizeHandling,
                SizeRatio = invocation.SizeRatio,
                HorizontalPositionRatio = invocation.HorizontalPositionRatio,
                VerticalPositionRatio = invocation.VerticalPositionRatio
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
                invocation.WatermarkPngPath = NextValue();
                break;
            case "--position":
            case "-p":
                invocation.Position = ParsePosition(NextValue());
                break;
            case "--position-x":
                invocation.HorizontalPositionRatio = ParseRatio(NextValue(), current);
                break;
            case "--position-y":
                invocation.VerticalPositionRatio = ParseRatio(NextValue(), current);
                break;
            case "--size":
                invocation.SizeRatio = ParseSizeRatio(NextValue(), current);
                break;
            case "--size-handling":
                invocation.SizeHandling = ParseSizeHandling(NextValue());
                break;
            case "--format":
                invocation.OutputFormat = ParseFormat(NextValue());
                break;
            case "--jpeg-quality":
                invocation.JpegQuality = ParseJpegQuality(NextValue());
                break;
            case "--output-dir":
                invocation.OutputDirectory = NextValue();
                break;
            case "--suffix":
                invocation.OutputSuffix = ParseOutputSuffix(NextValue());
                break;
            case "--recursive":
            case "-r":
                invocation.Recursive = true;
                break;
            case "--force":
            case "-f":
                invocation.Force = true;
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

    if (string.IsNullOrWhiteSpace(invocation.WatermarkPngPath))
    {
        throw new ArgumentException("A watermark PNG path is required. Use --watermark logo.png.");
    }

    if (!File.Exists(invocation.WatermarkPngPath))
    {
        throw new ArgumentException($"Watermark PNG file not found: {invocation.WatermarkPngPath}");
    }

    if (!string.Equals(Path.GetExtension(invocation.WatermarkPngPath), ".png", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException($"Watermark file must be a PNG image: {invocation.WatermarkPngPath}");
    }

    return invocation;
}

static CliInvocation PromptForInvocation()
{
    Console.WriteLine("Interactive watermark setup");
    Console.WriteLine("Press Enter to accept defaults.");
    Console.WriteLine();

    var inputPath = PromptRequired("Input image or folder path");
    var isDirectoryInput = Directory.Exists(inputPath);
    var watermarkPngPath = PromptRequired("Watermark PNG path");
    var outputFormat = ParseFormat(Prompt("Output format", "auto"));
    var outputSuffix = ParseOutputSuffix(Prompt("Output suffix", "watermarked"));
    string? outputDirectory = null;
    string? outputPath = null;

    if (isDirectoryInput)
    {
        var defaultOutputDirectory = BuildDefaultOutputPath(inputPath, outputFormat, outputSuffix, outputDirectory: null);
        outputDirectory = PromptOptional($"Output directory ({defaultOutputDirectory})");
    }
    else
    {
        var defaultOutputPath = BuildDefaultOutputPath(inputPath, outputFormat, outputSuffix, outputDirectory: null);
        outputPath = PromptOptional($"Output file ({defaultOutputPath})");
    }

    var position = ParsePosition(Prompt("Position", "bottom-right"));
    var sizeHandling = ParseSizeHandling(Prompt("Size handling", "relative"));
    var sizeRatio = ParseSizeRatio(Prompt("Size ratio in percent or decimal", "20"), "Size ratio");
    var positionX = PromptOptionalRatio("Horizontal position ratio (0 left to 1 right)");
    var positionY = PromptOptionalRatio("Vertical position ratio (0 top to 1 bottom)");
    var recursive = isDirectoryInput && ParseBoolean(Prompt("Recursive folder scan", "yes"));
    var jpegQualityText = Prompt("JPEG quality", "100");

    return new CliInvocation
    {
        InputPath = inputPath,
        OutputPath = outputPath,
        WatermarkPngPath = watermarkPngPath,
        Position = position,
        SizeHandling = sizeHandling,
        SizeRatio = sizeRatio,
        HorizontalPositionRatio = positionX,
        VerticalPositionRatio = positionY,
        OutputFormat = outputFormat,
        OutputDirectory = outputDirectory,
        OutputSuffix = outputSuffix,
        Recursive = recursive,
        JpegQuality = ParseJpegQuality(jpegQualityText)
    };
}

static IEnumerable<ProcessingTarget> ResolveTargets(CliInvocation invocation)
{
    if (!string.IsNullOrWhiteSpace(invocation.OutputPath) && !string.IsNullOrWhiteSpace(invocation.OutputDirectory))
    {
        throw new ArgumentException("Use either an explicit output path or --output-dir, not both.");
    }

    if (File.Exists(invocation.InputPath))
    {
        var inputPath = Path.GetFullPath(invocation.InputPath);
        yield return new ProcessingTarget(
            inputPath,
            ResolveSingleFileOutputPath(invocation, inputPath));
        yield break;
    }

    if (!Directory.Exists(invocation.InputPath))
    {
        throw new ArgumentException($"Input path not found: {invocation.InputPath}");
    }

    var inputRoot = Path.GetFullPath(invocation.InputPath);
    var outputRoot = ResolveOutputRoot(invocation, inputRoot);
    var searchOption = invocation.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var skippedDirectoryRoot = IsPathWithin(outputRoot, inputRoot) ? outputRoot : null;

    foreach (var file in Directory.EnumerateFiles(inputRoot, "*.*", searchOption)
        .Where(path => skippedDirectoryRoot is null || !IsPathWithin(path, skippedDirectoryRoot))
        .Where(IsSupportedInputFile)
        .Where(path => !HasOutputSuffix(path, invocation.OutputSuffix)))
    {
        var relativePath = Path.GetRelativePath(inputRoot, file);
        var outputFile = Path.Combine(outputRoot, BuildOutputRelativePath(relativePath, invocation.OutputFormat, invocation.OutputSuffix));
        yield return new ProcessingTarget(file, outputFile);
    }
}

static bool IsSupportedInputFile(string path)
{
    var extension = Path.GetExtension(path).ToLowerInvariant();
    return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
}

static string BuildOutputRelativePath(string relativePath, ImageOutputFormat outputFormat, string outputSuffix)
{
    var directory = Path.GetDirectoryName(relativePath);
    var fileName = Path.GetFileNameWithoutExtension(relativePath);
    var suffixedFileName = AppendSuffix(fileName, outputSuffix);
    var outputFileName = $"{suffixedFileName}{ResolveOutputExtension(relativePath, outputFormat)}";

    return string.IsNullOrEmpty(directory)
        ? outputFileName
        : Path.Combine(directory, outputFileName);
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

static string? PromptOptional(string label)
{
    Console.Write($"{label} [default]: ");
    var value = Console.ReadLine()?.Trim();
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static float? PromptOptionalRatio(string label)
{
    Console.Write($"{label} [preset]: ");
    var value = Console.ReadLine()?.Trim();
    return string.IsNullOrWhiteSpace(value) ? null : ParseRatio(value, label);
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
    Console.WriteLine("  image-watermarker <input-file> [output-file] --watermark <watermark.png> [options]");
    Console.WriteLine("  image-watermarker <input-folder> [output-folder] --watermark <watermark.png> [options]");
    Console.WriteLine();
    Console.WriteLine("If you run the tool without arguments, it starts in interactive mode.");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --watermark, -w      Path to the PNG watermark file");
    Console.WriteLine("  --position, -p       top-left | top-center | top-right | middle-left | middle-center | middle-right | bottom-left | bottom-center | bottom-right");
    Console.WriteLine("  --size-handling      relative | width");
    Console.WriteLine("  --size               Watermark size as 1-100 or 0.01-1");
    Console.WriteLine("  --position-x         Horizontal placement from 0 to 1");
    Console.WriteLine("  --position-y         Vertical placement from 0 to 1");
    Console.WriteLine("  --format             png | jpeg | bmp | gif | auto");
    Console.WriteLine("  --jpeg-quality       1-100, used only for JPEG output");
    Console.WriteLine("  --output-dir         Directory to write generated files into");
    Console.WriteLine("  --suffix             Suffix added to generated file or folder names");
    Console.WriteLine("  --recursive, -r      Scan subfolders when input is a folder");
    Console.WriteLine("  --force, -f          Overwrite existing files without prompting");
    Console.WriteLine("  --help, -h           Show help");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  image-watermarker photo.jpg --watermark logo.png");
    Console.WriteLine("  image-watermarker photo.jpg --watermark logo.png --size-handling width --size 35");
    Console.WriteLine("  image-watermarker photo.jpg --watermark logo.png --output-dir branded");
    Console.WriteLine("  image-watermarker photos --watermark logo.png --recursive");
    Console.WriteLine("  image-watermarker photo.jpg branded.jpg --watermark logo.png --format jpeg --jpeg-quality 100");
}

static WatermarkPosition ParsePosition(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "top-left" => WatermarkPosition.TopLeft,
        "top-center" => WatermarkPosition.TopCenter,
        "top-right" => WatermarkPosition.TopRight,
        "middle-left" => WatermarkPosition.MiddleLeft,
        "middle-center" or "center" => WatermarkPosition.MiddleCenter,
        "middle-right" => WatermarkPosition.MiddleRight,
        "bottom-left" => WatermarkPosition.BottomLeft,
        "bottom-center" => WatermarkPosition.BottomCenter,
        "bottom-right" => WatermarkPosition.BottomRight,
        _ => throw new ArgumentException($"Unsupported watermark position: {value}")
    };
}

static float ParseRatio(string value, string optionName)
{
    if (!float.TryParse(value, out var ratio) || ratio is < 0f or > 1f)
    {
        throw new ArgumentException($"{optionName} must be a number between 0 and 1.");
    }

    return ratio;
}

static float ParseSizeRatio(string value, string optionName)
{
    if (!float.TryParse(value, out var sizeRatio))
    {
        throw new ArgumentException($"{optionName} must be a number between 1 and 100 or 0.01 and 1.");
    }

    if (sizeRatio is > 1f and <= 100f)
    {
        return sizeRatio / 100f;
    }

    if (sizeRatio is >= 0.01f and <= 1f)
    {
        return sizeRatio;
    }

    throw new ArgumentException($"{optionName} must be a number between 1 and 100 or 0.01 and 1.");
}

static WatermarkSizeHandling ParseSizeHandling(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "relative" => WatermarkSizeHandling.Relative,
        "width" => WatermarkSizeHandling.Width,
        _ => throw new ArgumentException($"Unsupported size handling: {value}")
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

static string ParseOutputSuffix(string value)
{
    var suffix = value.Trim();

    if (string.IsNullOrWhiteSpace(suffix))
    {
        throw new ArgumentException("Output suffix cannot be empty.");
    }

    if (suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
    {
        throw new ArgumentException($"Output suffix contains invalid file name characters: {value}");
    }

    return suffix;
}

static string BuildDefaultOutputPath(string inputPath, ImageOutputFormat outputFormat, string outputSuffix, string? outputDirectory)
{
    if (Directory.Exists(inputPath))
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (outputDirectory is not null)
        {
            return Path.GetFullPath(outputDirectory);
        }

        var parent = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        return Path.Combine(parent, AppendSuffix(Path.GetFileName(fullPath), outputSuffix));
    }

    var directory = outputDirectory is null
        ? Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? Environment.CurrentDirectory
        : Path.GetFullPath(outputDirectory);
    var name = Path.GetFileNameWithoutExtension(inputPath);
    var extension = ResolveOutputExtension(inputPath, outputFormat);

    return Path.Combine(directory, $"{AppendSuffix(name, outputSuffix)}{extension}");
}

static string ResolveSingleFileOutputPath(CliInvocation invocation, string inputPath)
{
    if (!string.IsNullOrWhiteSpace(invocation.OutputPath))
    {
        return Path.GetFullPath(invocation.OutputPath);
    }

    return BuildDefaultOutputPath(inputPath, invocation.OutputFormat, invocation.OutputSuffix, invocation.OutputDirectory);
}

static string ResolveOutputRoot(CliInvocation invocation, string inputRoot)
{
    if (!string.IsNullOrWhiteSpace(invocation.OutputPath))
    {
        return Path.GetFullPath(invocation.OutputPath);
    }

    return BuildDefaultOutputPath(inputRoot, invocation.OutputFormat, invocation.OutputSuffix, invocation.OutputDirectory);
}

static string ResolveOutputExtension(string path, ImageOutputFormat outputFormat)
{
    return outputFormat switch
    {
        ImageOutputFormat.Auto => Path.GetExtension(path),
        ImageOutputFormat.Jpeg => ".jpg",
        ImageOutputFormat.Bmp => ".bmp",
        ImageOutputFormat.Gif => ".gif",
        _ => ".png"
    };
}

static string AppendSuffix(string name, string suffix) => $"{name}-{suffix}";

static bool HasOutputSuffix(string path, string suffix)
{
    var fileName = Path.GetFileNameWithoutExtension(path);
    return fileName.EndsWith($"-{suffix}", OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

static bool PathsMatch(string firstPath, string secondPath) =>
    string.Equals(
        Path.GetFullPath(firstPath),
        Path.GetFullPath(secondPath),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

static bool IsPathWithin(string path, string rootPath)
{
    var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    var fullRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    return fullPath.Length > fullRootPath.Length
        && fullPath.StartsWith($"{fullRootPath}{Path.DirectorySeparatorChar}", comparison);
}

static bool ShouldWriteTarget(string outputPath, bool force, ref OverwriteBehavior overwriteBehavior)
{
    if (!File.Exists(outputPath))
    {
        return true;
    }

    if (force)
    {
        return true;
    }

    if (Console.IsInputRedirected)
    {
        throw new ArgumentException($"Output file already exists: {outputPath}. Use --force to overwrite in non-interactive mode.");
    }

    if (overwriteBehavior is OverwriteBehavior.All)
    {
        return true;
    }

    while (true)
    {
        Console.Write($"Overwrite existing file '{outputPath}'? [y]es/[n]o/[a]ll: ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();

        switch (response)
        {
            case "y":
            case "yes":
                return true;
            case "n":
            case "no":
                return false;
            case "a":
            case "all":
                overwriteBehavior = OverwriteBehavior.All;
                return true;
        }
    }
}

internal sealed class CliInvocation
{
    public bool ShowHelp { get; set; }

    public string InputPath { get; set; } = string.Empty;

    public string? OutputPath { get; set; }

    public string? OutputDirectory { get; set; }

    public string OutputSuffix { get; set; } = "watermarked";

    public string WatermarkPngPath { get; set; } = string.Empty;

    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;

    public WatermarkSizeHandling SizeHandling { get; set; } = WatermarkSizeHandling.Relative;

    public float SizeRatio { get; set; } = 0.2f;

    public float? HorizontalPositionRatio { get; set; }

    public float? VerticalPositionRatio { get; set; }

    public ImageOutputFormat OutputFormat { get; set; } = ImageOutputFormat.Auto;

    public bool Recursive { get; set; }

    public int JpegQuality { get; set; } = 100;

    public bool Force { get; set; }
}

internal sealed record ProcessingTarget(string InputPath, string OutputPath);

internal enum OverwriteBehavior
{
    Ask,
    All
}
