namespace WTDeck.StreamDock.Configuration;

public sealed class StreamDockOptions
{
    public bool SyncOnStartup { get; set; } = true;
    public bool AlwaysRestart { get; set; } = true;
    public string? UserDataRoot { get; set; }
    public string? InstallDir { get; set; }
    public string DeviceUUID { get; set; } = "CN001V3Device";
    public string DeviceSerialNumber { get; set; } = "8730DB78224F";
    public string DeviceModel { get; set; } = "20GBA9901";
    public string ProfileName { get; set; } = "WTDeck";
    public string PluginUuid { get; set; } = "com.wtdeck.streamdock";
    public string PluginActionUuid { get; set; } = "com.wtdeck.streamdock.gear";
    public bool ForceOverwriteProfile { get; set; } = true;
}
