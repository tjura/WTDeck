using FluentAssertions;
using WTDeck.Core.KeyBindings;
using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.KeyBindings;

public class BlkKeyBindingProviderTests
{
    [Fact]
    public void Provides_default_gear_binding_when_file_is_empty()
    {
        var provider = BlkKeyBindingProvider.FromReader(new StringReader(""));

        var binding = provider.GetBinding(ActionId.Gear);
        binding.Should().NotBeNull();
        binding!.Chords.Should().HaveCount(1);
        binding.Chords[0].ScanCodes.Should().Equal(34); // G key default
    }

    [Fact]
    public void Overrides_default_when_binding_found_in_file()
    {
        var blk = """
            controls{
              hotkeys{
                ID_GEAR{
                  keyboardKey:i=44
                }
              }
            }
            """;

        var provider = BlkKeyBindingProvider.FromReader(new StringReader(blk));

        var binding = provider.GetBinding(ActionId.Gear);
        binding.Should().NotBeNull();
        binding!.Chords[0].ScanCodes.Should().Equal(44); // Z key
    }

    [Fact]
    public void GetAllBindings_returns_all_parsed_bindings()
    {
        var blk = """
            controls{
              hotkeys{
                ID_GEAR{
                  keyboardKey:i=34
                }
                ID_BOMBS{
                  keyboardKey:i=57
                }
              }
            }
            """;

        var provider = BlkKeyBindingProvider.FromReader(new StringReader(blk));
        provider.GetAllBindings().Should().HaveCountGreaterOrEqualTo(2);
    }
}
