namespace WTDeck.Ipc.Http;

public sealed class HttpPluginBridgeOptions
{
    public int Port { get; set; } = 8730;
    public string BindAddress { get; set; } = "127.0.0.1";
}
