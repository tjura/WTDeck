namespace WTDeck.Core.Models;

public sealed record KeyBinding(ActionId ActionId, IReadOnlyList<KeyChord> Chords);
