using System.Reflection;

namespace WTDeck.StreamDock.Plugin;

/// <summary>
/// Enumerates embedded plugin resources that must be written to disk
/// during plugin installation.
/// </summary>
public static class PluginAssetManifest
{
    // Maps embedded resource names to relative paths inside the .sdPlugin directory
    public static IReadOnlyDictionary<string, string> ResourceMap { get; } = new Dictionary<string, string>
    {
        ["WTDeck.Plugin.manifest.json"] = "manifest.json",
        ["WTDeck.Plugin.plugin.index.html"] = "plugin/index.html",
        ["WTDeck.Plugin.plugin.index.js"] = "plugin/index.js",
        ["WTDeck.Plugin.plugin.property-inspector.html"] = "plugin/property-inspector.html",
        ["WTDeck.Plugin.assets.gear-retracted.svg"] = "assets/gear-retracted.svg",
        ["WTDeck.Plugin.assets.gear-deployed.svg"] = "assets/gear-deployed.svg",
        ["WTDeck.Plugin.assets.gear-deploying.svg"] = "assets/gear-deploying.svg",
        ["WTDeck.Plugin.assets.gear-retracting.svg"] = "assets/gear-retracting.svg",
        ["WTDeck.Plugin.assets.gear-damaged.svg"] = "assets/gear-damaged.svg",
        ["WTDeck.Plugin.assets.gear-disabled.svg"] = "assets/gear-disabled.svg",
        ["WTDeck.Plugin.assets.gear-unknown.svg"] = "assets/gear-unknown.svg",
        ["WTDeck.Plugin.assets.gear-blink-off.svg"] = "assets/gear-blink-off.svg",
        ["WTDeck.Plugin.assets.plugin-icon.svg"] = "assets/plugin-icon.svg",
        ["WTDeck.Plugin.assets.category-icon.svg"] = "assets/category-icon.svg",
    };

    public static Stream? OpenResource(string resourceName, Assembly? assembly = null)
    {
        assembly ??= typeof(PluginAssetManifest).Assembly;
        return assembly.GetManifestResourceStream(resourceName);
    }
}
