using WTDeck.Core.Contracts;

namespace WTDeck.Core.Interfaces;

public interface IPluginBridge
{
    Task SendButtonStateAsync(ButtonStateUpdate update, CancellationToken ct);
    Task SendPanelStateAsync(StreamDockPanelUpdate update, CancellationToken ct);
    event EventHandler<ButtonPressCommand>? ButtonPressed;
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
