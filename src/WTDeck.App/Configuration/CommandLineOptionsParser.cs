namespace WTDeck.App.Configuration;

public static class CommandLineOptionsParser
{
    public static CommandLineParseResult Parse(string[] args)
    {
        var debugEnabled = false;
        string? scenarioPath = null;

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

                default:
                    if (arg.StartsWith("--emulate-api=", StringComparison.Ordinal))
                    {
                        scenarioPath = arg["--emulate-api=".Length..];
                        if (string.IsNullOrWhiteSpace(scenarioPath))
                            return CommandLineParseResult.Fail("Missing path after --emulate-api=.");

                        debugEnabled = true;
                        break;
                    }

                    return CommandLineParseResult.Fail($"Unknown argument: {arg}");
            }
        }

        return CommandLineParseResult.Success(new RuntimeModeOptions
        {
            DebugEnabled = debugEnabled,
            EmulateApi = !string.IsNullOrWhiteSpace(scenarioPath),
            ScenarioPath = string.IsNullOrWhiteSpace(scenarioPath) ? null : Path.GetFullPath(scenarioPath),
        });
    }

    public static string UsageText => """
        WTDeck

        Usage:
          WTDeck.App.exe [--debug] [--emulate-api <scenario.json>]

        Options:
          --debug                    Run in debug mode and print current state/output to the console.
          --emulate-api <path>       Replay a scripted telemetry scenario instead of calling the live War Thunder API.
          --help                     Show this help text.
        """;
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
