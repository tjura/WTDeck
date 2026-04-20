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
}
