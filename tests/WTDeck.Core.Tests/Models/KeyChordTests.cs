using FluentAssertions;
using WTDeck.Core.Models;

namespace WTDeck.Core.Tests.Models;

public class KeyChordTests
{
    [Fact]
    public void Single_key_chord_equality()
    {
        var a = new KeyChord([34]);
        var b = new KeyChord([34]);
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Multi_key_chord_equality()
    {
        var a = new KeyChord([56, 57]);
        var b = new KeyChord([56, 57]);
        a.Should().Be(b);
    }

    [Fact]
    public void Different_key_chords_are_not_equal()
    {
        var a = new KeyChord([34]);
        var b = new KeyChord([57]);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Order_matters_for_chord_equality()
    {
        var a = new KeyChord([56, 57]);
        var b = new KeyChord([57, 56]);
        a.Should().NotBe(b);
    }
}
