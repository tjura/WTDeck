using FluentAssertions;
using WTDeck.Core.KeyBindings;
using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.KeyBindings;

public class BlkParserTests
{
    [Fact]
    public void Parses_single_action_single_key()
    {
        var blk = """
            controls{
              version:i=5
              hotkeys{
                ID_GEAR{
                  keyboardKey:i=34
                }
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));

        result.Should().ContainKey("ID_GEAR");
        result["ID_GEAR"].ActionId.Should().Be(ActionId.Gear);
        result["ID_GEAR"].Chords.Should().HaveCount(1);
        result["ID_GEAR"].Chords[0].ScanCodes.Should().Equal(34);
    }

    [Fact]
    public void Parses_chord_with_multiple_keys_in_one_block()
    {
        var blk = """
            controls{
              hotkeys{
                ID_BOMBS{
                  keyboardKey:i=56
                  keyboardKey:i=57
                }
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));

        result.Should().ContainKey("ID_BOMBS");
        result["ID_BOMBS"].Chords.Should().HaveCount(1);
        result["ID_BOMBS"].Chords[0].ScanCodes.Should().Equal(56, 57);
    }

    [Fact]
    public void Parses_flare_only_binding()
    {
        var blk = """
            controls{
              hotkeys{
                ID_COUNTERMEASURES_FLARES{
                  keyboardKey:i=45
                }
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));

        result.Should().ContainKey("ID_COUNTERMEASURES_FLARES");
        result["ID_COUNTERMEASURES_FLARES"].ActionId.Should().Be(ActionId.LaunchFlares);
        result["ID_COUNTERMEASURES_FLARES"].Chords[0].ScanCodes.Should().Equal(45);
    }

    [Fact]
    public void Does_not_treat_selected_countermeasure_binding_as_flare_only()
    {
        var blk = """
            controls{
              hotkeys{
                ID_FLARES{
                  keyboardKey:i=45
                }
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));

        result["ID_FLARES"].ActionId.Should().Be(ActionId.Unknown);
    }

    [Fact]
    public void Duplicate_action_blocks_produce_alternative_chords()
    {
        var blk = """
            controls{
              hotkeys{
                ID_GEAR{
                  keyboardKey:i=34
                }
                ID_GEAR{
                  keyboardKey:i=57
                }
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));

        result["ID_GEAR"].Chords.Should().HaveCount(2);
        result["ID_GEAR"].Chords[0].ScanCodes.Should().Equal(34);
        result["ID_GEAR"].Chords[1].ScanCodes.Should().Equal(57);
    }

    [Fact]
    public void Joystick_only_binding_produces_no_keyboard_chord()
    {
        var blk = """
            controls{
              hotkeys{
                ID_GEAR{
                  joyButton:i=30
                }
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));

        // The block exists but has no keyboard keys - no chord collected
        result.Should().NotContainKey("ID_GEAR");
    }

    [Fact]
    public void Empty_hotkeys_block_returns_empty()
    {
        var blk = """
            controls{
              hotkeys{
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Malformed_content_does_not_throw()
    {
        var blk = "this is not valid blk content { } { {{{}}}";
        var act = () => BlkParser.Parse(new StringReader(blk));
        act.Should().NotThrow();
    }

    [Fact]
    public void Parses_real_world_excerpt()
    {
        var blk = """
            controls{
              version:i=5
              basePresetPaths{
                preset:t="wt/config/hotkeys/hotkey.keyboard_ver1.blk"
              }
              hotkeys{
                ID_GEAR{
                  keyboardKey:i=34
                }
                ID_GEAR{
                  joyButton:i=30
                }
                ID_BOMBS{
                  keyboardKey:i=57
                }
              }
            }
            """;

        var result = BlkParser.Parse(new StringReader(blk));

        result.Should().ContainKey("ID_GEAR");
        result.Should().ContainKey("ID_BOMBS");
        // ID_GEAR has 1 keyboard chord + 1 joystick-only (not counted)
        result["ID_GEAR"].Chords.Should().HaveCount(1);
        result["ID_GEAR"].Chords[0].ScanCodes.Should().Equal(34);
    }
}
