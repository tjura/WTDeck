using WTDeck.Core.Models;

namespace WTDeck.Core.Interfaces;

public interface IKeyboardSender
{
    void Send(KeyChord chord);
}
