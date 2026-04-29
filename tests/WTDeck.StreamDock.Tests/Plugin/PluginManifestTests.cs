using System.Text.Json;
using FluentAssertions;

namespace WTDeck.StreamDock.Tests.Plugin;

public class PluginManifestTests
{
    [Fact]
    public void Over_g_action_is_information_only()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "WTDeck.Plugin", "manifest.json")));
        var actions = document.RootElement.GetProperty("Actions").EnumerateArray();

        var action = actions.Single(a => a.GetProperty("UUID").GetString() == "com.wtdeck.streamdock.flight-alerts");
        var controllers = action.GetProperty("Controllers").EnumerateArray().Select(c => c.GetString()).ToList();

        action.GetProperty("Name").GetString().Should().Be("Over-G Alert");
        controllers.Should().Equal("Information");
        action.GetProperty("SupportedInMultiActions").GetBoolean().Should().BeFalse();
        action.GetProperty("UserTitleEnabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Plugin_has_panel_fallback_and_does_not_route_panel_keydown()
    {
        var js = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "WTDeck.Plugin", "plugin", "index.js"));

        js.Should().Contain("PANEL_POLL_INTERVAL_MS = 100");
        js.Should().Contain("PANEL_TILE_SIZE = 128");
        js.Should().Contain("\"com.wtdeck.streamdock.flight-alerts\"");
        js.Should().Contain("event === \"keyDown\" && actionDefinition && !actionDefinition.panel");
        js.Should().Contain("root.alerts || {}");
        js.Should().Contain("alerts[\"over-g\"] || {}");
        js.Should().NotContain("rows:");
    }

    private static string FindRepoRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WTDeck.sln")))
                    return current.FullName;

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate WTDeck.sln from the test output path.");
    }
}
