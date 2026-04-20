using System.Text.Json.Serialization;

namespace WTDeck.StreamDock.Profiles;

public sealed class ProfileManifest
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("DeviceUUID")]
    public string DeviceUUID { get; set; } = "";

    [JsonPropertyName("DeviceSerialNumber")]
    public string DeviceSerialNumber { get; set; } = "";

    [JsonPropertyName("DeviceModel")]
    public string DeviceModel { get; set; } = "";

    [JsonPropertyName("AppIdentifier")]
    public string AppIdentifier { get; set; } = "*";

    [JsonPropertyName("Pages")]
    public ProfilePages Pages { get; set; } = new();

    [JsonPropertyName("Actions")]
    public Dictionary<string, ProfileAction> Actions { get; set; } = [];
}

public sealed class ProfilePages
{
    [JsonPropertyName("Current")]
    public string Current { get; set; } = "";

    [JsonPropertyName("Pages")]
    public List<string> Pages { get; set; } = [];
}

public sealed class ProfileAction
{
    [JsonPropertyName("ActionID")]
    public string ActionID { get; set; } = "";

    [JsonPropertyName("UUID")]
    public string UUID { get; set; } = "";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("State")]
    public int State { get; set; }

    [JsonPropertyName("Settings")]
    public Dictionary<string, object?> Settings { get; set; } = [];

    [JsonPropertyName("States")]
    public List<ProfileActionState> States { get; set; } = [];
}

public sealed class ProfileActionState
{
    [JsonPropertyName("Image")]
    public string Image { get; set; } = "";

    [JsonPropertyName("Title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("FontSize")]
    public string FontSize { get; set; } = "14";

    [JsonPropertyName("TitleAlignment")]
    public string TitleAlignment { get; set; } = "middle";

    [JsonPropertyName("ShowTitle")]
    public bool ShowTitle { get; set; }
}
