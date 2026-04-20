using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;

namespace WTDeck.Core.KeyBindings;

public sealed class BlkKeyBindingProvider : IKeyBindingProvider
{
    private static readonly KeyBinding DefaultGearBinding = new(
        ActionId.Gear,
        [new KeyChord([34])]); // G key

    private readonly Dictionary<ActionId, KeyBinding> _bindings;

    private BlkKeyBindingProvider(Dictionary<ActionId, KeyBinding> bindings)
    {
        _bindings = bindings;
    }

    public static BlkKeyBindingProvider FromFile(string path)
    {
        using var reader = new StreamReader(path);
        return FromReader(reader);
    }

    public static BlkKeyBindingProvider FromReader(TextReader reader)
    {
        var parsed = BlkParser.Parse(reader);
        var bindings = new Dictionary<ActionId, KeyBinding>();

        foreach (var (_, binding) in parsed)
        {
            if (binding.ActionId != ActionId.Unknown)
            {
                bindings[binding.ActionId] = binding;
            }
        }

        // Apply defaults for missing bindings
        bindings.TryAdd(ActionId.Gear, DefaultGearBinding);

        return new BlkKeyBindingProvider(bindings);
    }

    public KeyBinding? GetBinding(ActionId actionId) =>
        _bindings.GetValueOrDefault(actionId);

    public IReadOnlyList<KeyBinding> GetAllBindings() =>
        _bindings.Values.ToList().AsReadOnly();
}
