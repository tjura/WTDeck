using WTDeck.Core.Models;

namespace WTDeck.Core.Interfaces;

public interface IKeyBindingProvider
{
    KeyBinding? GetBinding(ActionId actionId);
    IReadOnlyList<KeyBinding> GetAllBindings();
}
