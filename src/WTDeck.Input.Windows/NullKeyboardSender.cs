using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;

namespace WTDeck.Input.Windows;

public sealed class NullKeyboardSender : IKeyboardSender
{
    private readonly List<KeyChord> _sentChords = [];

    public IReadOnlyList<KeyChord> SentChords => _sentChords.AsReadOnly();

    public void Send(KeyChord chord)
    {
        _sentChords.Add(chord);
    }

    public void Clear() => _sentChords.Clear();
}
