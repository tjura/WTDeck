using System.Collections.Concurrent;
using WTDeck.Core.Contracts;
using WTDeck.Core.Interfaces;

namespace WTDeck.App.Debug;

public sealed class RecordingPluginBridge : IPluginBridge
{
    private readonly ConcurrentDictionary<string, ButtonStateUpdate> _stateByActionKey = new(StringComparer.Ordinal);
    private StreamDockPanelUpdate _panelUpdate = StreamDockPanelUpdate.Unavailable();

    public event EventHandler<ButtonPressCommand>? ButtonPressed;
    public event EventHandler<ButtonStateUpdate>? ButtonStateSent;
    public event EventHandler<StreamDockPanelUpdate>? PanelStateSent;

    public Task SendButtonStateAsync(ButtonStateUpdate update, CancellationToken ct)
    {
        _stateByActionKey[update.ActionKey] = update;
        ButtonStateSent?.Invoke(this, update);
        return Task.CompletedTask;
    }

    public Task SendPanelStateAsync(StreamDockPanelUpdate update, CancellationToken ct)
    {
        _panelUpdate = update;
        PanelStateSent?.Invoke(this, update);
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public bool TryGetLatestState(string actionKey, out ButtonStateUpdate? update)
    {
        var found = _stateByActionKey.TryGetValue(actionKey, out var latest);
        update = latest;
        return found;
    }

    public StreamDockPanelUpdate LatestPanelState => _panelUpdate;

    public void TriggerButtonPress(string actionKey)
        => ButtonPressed?.Invoke(this, new ButtonPressCommand(IpcProtocol.Version, actionKey));
}
