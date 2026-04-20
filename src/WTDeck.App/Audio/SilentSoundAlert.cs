using WTDeck.Core.Interfaces;

namespace WTDeck.App.Audio;

public sealed class SilentSoundAlert : ISoundAlert
{
    public void PlayDangerTone() { }
    public void Stop() { }
    public void Dispose() { }
}
