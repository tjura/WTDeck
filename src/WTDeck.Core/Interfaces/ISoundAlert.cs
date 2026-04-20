namespace WTDeck.Core.Interfaces;

public interface ISoundAlert : IDisposable
{
    void PlayDangerTone();
    void Stop();
}
