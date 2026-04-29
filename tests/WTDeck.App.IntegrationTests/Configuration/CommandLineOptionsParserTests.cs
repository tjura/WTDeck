using FluentAssertions;
using WTDeck.App.Configuration;

namespace WTDeck.App.IntegrationTests.Configuration;

public sealed class CommandLineOptionsParserTests
{
    [Fact]
    public void Parse_without_args_uses_normal_runtime_mode()
    {
        var result = CommandLineOptionsParser.Parse([]);

        result.IsSuccess.Should().BeTrue();
        result.Options.Should().NotBeNull();
        result.Options!.DebugEnabled.Should().BeFalse();
        result.Options.EmulateApi.Should().BeFalse();
        result.Options.DisableSideEffects.Should().BeFalse();
        result.Options.UseTray.Should().BeTrue();
    }

    [Fact]
    public void Parse_debug_flag_enables_debug_mode()
    {
        var result = CommandLineOptionsParser.Parse(["--debug"]);

        result.IsSuccess.Should().BeTrue();
        result.Options.Should().NotBeNull();
        result.Options!.DebugEnabled.Should().BeTrue();
        result.Options.EmulateApi.Should().BeFalse();
        result.Options.DisableSideEffects.Should().BeTrue();
        result.Options.UseTray.Should().BeFalse();
    }

    [Fact]
    public void Parse_emulate_api_flag_enables_debug_mode_and_resolves_path()
    {
        var result = CommandLineOptionsParser.Parse(["--emulate-api", "scenarios/sample.json"]);

        result.IsSuccess.Should().BeTrue();
        result.Options.Should().NotBeNull();
        result.Options!.DebugEnabled.Should().BeTrue();
        result.Options.EmulateApi.Should().BeTrue();
        result.Options.ScenarioPath.Should().Be(Path.GetFullPath("scenarios/sample.json"));
    }

    [Fact]
    public void Parse_help_flag_requests_help()
    {
        var result = CommandLineOptionsParser.Parse(["--help"]);

        result.ShowHelp.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Parse_unknown_flag_fails()
    {
        var result = CommandLineOptionsParser.Parse(["--wat"]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unknown argument");
    }

    [Fact]
    public void Parse_capture_8111_uses_defaults_and_disables_side_effects()
    {
        var result = CommandLineOptionsParser.Parse(["--capture-8111"]);

        result.IsSuccess.Should().BeTrue();
        result.Options.Should().NotBeNull();
        result.Options!.Capture8111.Should().BeTrue();
        result.Options.DebugEnabled.Should().BeTrue();
        result.Options.DisableSideEffects.Should().BeTrue();
        result.Options.UseTray.Should().BeFalse();
        result.Options.CaptureOptions.OutputDirectory.Should().BeNull();
        result.Options.CaptureOptions.DurationSeconds.Should().Be(300);
        result.Options.CaptureOptions.IntervalMs.Should().Be(500);
        result.Options.CaptureOptions.DumpIntervalSeconds.Should().Be(10);
    }

    [Fact]
    public void Parse_capture_8111_accepts_custom_values()
    {
        var result = CommandLineOptionsParser.Parse([
            "--capture-8111",
            "--capture-output",
            "tmp/capture",
            "--capture-duration",
            "60",
            "--capture-interval-ms",
            "250",
            "--capture-dump-interval-sec",
            "5"
        ]);

        result.IsSuccess.Should().BeTrue();
        result.Options.Should().NotBeNull();
        result.Options!.CaptureOptions.OutputDirectory.Should().Be(Path.GetFullPath("tmp/capture"));
        result.Options.CaptureOptions.DurationSeconds.Should().Be(60);
        result.Options.CaptureOptions.IntervalMs.Should().Be(250);
        result.Options.CaptureOptions.DumpIntervalSeconds.Should().Be(5);
    }

    [Fact]
    public void Parse_analyze_8111_capture_resolves_directory()
    {
        var result = CommandLineOptionsParser.Parse(["--analyze-8111-capture", "tmp/capture"]);

        result.IsSuccess.Should().BeTrue();
        result.Options.Should().NotBeNull();
        result.Options!.Analyze8111Capture.Should().BeTrue();
        result.Options.Analyze8111CaptureDirectory.Should().Be(Path.GetFullPath("tmp/capture"));
        result.Options.DisableSideEffects.Should().BeTrue();
    }

    [Fact]
    public void Parse_rejects_capture_options_without_capture_mode()
    {
        var result = CommandLineOptionsParser.Parse(["--capture-duration", "60"]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("--capture-8111");
    }

    [Fact]
    public void Parse_rejects_multiple_exclusive_modes()
    {
        var result = CommandLineOptionsParser.Parse([
            "--capture-8111",
            "--emulate-api",
            "scenarios/sample.json"
        ]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Choose only one");
    }
}
