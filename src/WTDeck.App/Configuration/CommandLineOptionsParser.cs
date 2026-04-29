namespace WTDeck.App.Configuration;

public static class CommandLineOptionsParser
{
    public static CommandLineParseResult Parse(string[] args)
    {
        var debugEnabled = false;
        string? scenarioPath = null;
        var capture8111 = false;
        string? captureOutputDirectory = null;
        var captureDurationSeconds = Capture8111Options.Default.DurationSeconds;
        var captureIntervalMs = Capture8111Options.Default.IntervalMs;
        var captureDumpIntervalSeconds = Capture8111Options.Default.DumpIntervalSeconds;
        var sawCaptureOption = false;
        string? analyze8111CaptureDirectory = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--debug":
                    debugEnabled = true;
                    break;

                case "--help":
                case "-h":
                case "/?":
                    return CommandLineParseResult.Help();

                case "--emulate-api":
                    if (i + 1 >= args.Length)
                        return CommandLineParseResult.Fail("Missing path after --emulate-api.");

                    scenarioPath = args[++i];
                    debugEnabled = true;
                    break;

                case "--capture-8111":
                    capture8111 = true;
                    debugEnabled = true;
                    break;

                case "--capture-output":
                    if (!TryReadValue(args, ref i, "--capture-output", out captureOutputDirectory, out var captureOutputError))
                        return CommandLineParseResult.Fail(captureOutputError);
                    sawCaptureOption = true;
                    break;

                case "--capture-duration":
                    if (!TryReadPositiveInt(args, ref i, "--capture-duration", out captureDurationSeconds, out var durationError))
                        return CommandLineParseResult.Fail(durationError);
                    sawCaptureOption = true;
                    break;

                case "--capture-interval-ms":
                    if (!TryReadPositiveInt(args, ref i, "--capture-interval-ms", out captureIntervalMs, out var intervalError))
                        return CommandLineParseResult.Fail(intervalError);
                    sawCaptureOption = true;
                    break;

                case "--capture-dump-interval-sec":
                    if (!TryReadPositiveInt(args, ref i, "--capture-dump-interval-sec", out captureDumpIntervalSeconds, out var dumpIntervalError))
                        return CommandLineParseResult.Fail(dumpIntervalError);
                    sawCaptureOption = true;
                    break;

                case "--analyze-8111-capture":
                    if (!TryReadValue(args, ref i, "--analyze-8111-capture", out analyze8111CaptureDirectory, out var analyzeError))
                        return CommandLineParseResult.Fail(analyzeError);
                    debugEnabled = true;
                    break;

                default:
                    if (arg.StartsWith("--emulate-api=", StringComparison.Ordinal))
                    {
                        scenarioPath = arg["--emulate-api=".Length..];
                        if (string.IsNullOrWhiteSpace(scenarioPath))
                            return CommandLineParseResult.Fail("Missing path after --emulate-api=.");

                        debugEnabled = true;
                        break;
                    }

                    if (TryReadInlineValue(arg, "--capture-output", out captureOutputDirectory))
                    {
                        sawCaptureOption = true;
                        break;
                    }

                    if (TryReadInlineValue(arg, "--capture-duration", out var captureDurationText))
                    {
                        if (!TryParsePositiveInt(captureDurationText, "--capture-duration", out captureDurationSeconds, out var inlineDurationError))
                            return CommandLineParseResult.Fail(inlineDurationError);
                        sawCaptureOption = true;
                        break;
                    }

                    if (TryReadInlineValue(arg, "--capture-interval-ms", out var captureIntervalText))
                    {
                        if (!TryParsePositiveInt(captureIntervalText, "--capture-interval-ms", out captureIntervalMs, out var inlineIntervalError))
                            return CommandLineParseResult.Fail(inlineIntervalError);
                        sawCaptureOption = true;
                        break;
                    }

                    if (TryReadInlineValue(arg, "--capture-dump-interval-sec", out var dumpIntervalText))
                    {
                        if (!TryParsePositiveInt(dumpIntervalText, "--capture-dump-interval-sec", out captureDumpIntervalSeconds, out var inlineDumpIntervalError))
                            return CommandLineParseResult.Fail(inlineDumpIntervalError);
                        sawCaptureOption = true;
                        break;
                    }

                    if (TryReadInlineValue(arg, "--analyze-8111-capture", out analyze8111CaptureDirectory))
                    {
                        debugEnabled = true;
                        break;
                    }

                    return CommandLineParseResult.Fail($"Unknown argument: {arg}");
            }
        }

        if (sawCaptureOption && !capture8111)
            return CommandLineParseResult.Fail("Capture options require --capture-8111.");

        var modeCount = 0;
        if (!string.IsNullOrWhiteSpace(scenarioPath)) modeCount++;
        if (capture8111) modeCount++;
        if (!string.IsNullOrWhiteSpace(analyze8111CaptureDirectory)) modeCount++;
        if (modeCount > 1)
            return CommandLineParseResult.Fail("Choose only one of --emulate-api, --capture-8111, or --analyze-8111-capture.");

        return CommandLineParseResult.Success(new RuntimeModeOptions
        {
            DebugEnabled = debugEnabled,
            EmulateApi = !string.IsNullOrWhiteSpace(scenarioPath),
            ScenarioPath = string.IsNullOrWhiteSpace(scenarioPath) ? null : Path.GetFullPath(scenarioPath),
            Capture8111 = capture8111,
            CaptureOptions = new Capture8111Options(
                string.IsNullOrWhiteSpace(captureOutputDirectory) ? null : Path.GetFullPath(captureOutputDirectory),
                captureDurationSeconds,
                captureIntervalMs,
                captureDumpIntervalSeconds),
            Analyze8111CaptureDirectory = string.IsNullOrWhiteSpace(analyze8111CaptureDirectory)
                ? null
                : Path.GetFullPath(analyze8111CaptureDirectory),
        });
    }

    public static string UsageText => """
        WTDeck

        Usage:
          WTDeck.App.exe [--debug]
          WTDeck.App.exe --emulate-api <scenario.json>
          WTDeck.App.exe --capture-8111 [--capture-output <dir>] [--capture-duration <seconds>] [--capture-interval-ms <ms>] [--capture-dump-interval-sec <seconds>]
          WTDeck.App.exe --analyze-8111-capture <capture-dir>

        Options:
          --debug                         Run in debug mode and print current state/output to the console.
          --emulate-api <path>            Replay a scripted telemetry scenario instead of calling the live War Thunder API.
          --capture-8111                  Record compact localhost:8111 endpoint changes for telemetry discovery.
          --capture-output <dir>          Capture output directory. Defaults to tmp/8111-captures/<timestamp>.
          --capture-duration <seconds>    Capture duration. Defaults to 300.
          --capture-interval-ms <ms>      Poll interval. Defaults to 500.
          --capture-dump-interval-sec <s> Segment flush interval. Defaults to 10.
          --analyze-8111-capture <dir>    Analyze an existing 8111 capture directory.
          --help                          Show this help text.
        """;

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string optionName,
        out string? value,
        out string error)
    {
        value = null;
        if (index + 1 >= args.Length)
        {
            error = $"Missing path after {optionName}.";
            return false;
        }

        value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"Missing path after {optionName}.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryReadPositiveInt(
        string[] args,
        ref int index,
        string optionName,
        out int value,
        out string error)
    {
        value = 0;
        if (index + 1 >= args.Length)
        {
            error = $"Missing value after {optionName}.";
            return false;
        }

        return TryParsePositiveInt(args[++index], optionName, out value, out error);
    }

    private static bool TryParsePositiveInt(string text, string optionName, out int value, out string error)
    {
        if (!int.TryParse(text, out value) || value <= 0)
        {
            error = $"{optionName} must be a positive integer.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryReadInlineValue(string arg, string optionName, out string value)
    {
        var prefix = optionName + "=";
        if (!arg.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = "";
            return false;
        }

        value = arg[prefix.Length..];
        return !string.IsNullOrWhiteSpace(value);
    }
}

public sealed record CommandLineParseResult(
    bool IsSuccess,
    bool ShowHelp,
    RuntimeModeOptions? Options,
    string? Error)
{
    public static CommandLineParseResult Success(RuntimeModeOptions options) =>
        new(true, false, options, null);

    public static CommandLineParseResult Help() =>
        new(false, true, null, null);

    public static CommandLineParseResult Fail(string error) =>
        new(false, false, null, error);
}
