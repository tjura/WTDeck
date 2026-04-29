using WTDeck.Core.Contracts;

namespace WTDeck.Ipc.Http;

public sealed class HttpPluginBridgeOptions
{
    public int Port { get; set; } = IpcProtocol.HttpPort;
    public string BindAddress { get; set; } = IpcProtocol.HttpBindAddress;
}
