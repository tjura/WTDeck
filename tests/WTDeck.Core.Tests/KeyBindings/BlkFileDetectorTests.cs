using FluentAssertions;
using WTDeck.Core.KeyBindings;

namespace WTDeck.Core.Tests.KeyBindings;

public sealed class BlkFileDetectorTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"wtdeck-blk-test-{Guid.NewGuid():N}");

    public BlkFileDetectorTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void Finds_newest_key_binding_file_recursively()
    {
        var oldTopLevel = WriteBlk("LAST-CONTROLER.blk", """
            controls{
              hotkeys{
                ID_GEAR{
                  keyboardKey:i=34
                }
              }
            }
            """, DateTimeOffset.UtcNow.AddDays(-10));

        var activeMachine = WriteBlk(Path.Combine("last", "production", "machine.blk"), """
            controls{
              hotkeys{
                ID_COUNTERMEASURES_FLARES{
                  keyboardKey:i=27
                }
              }
            }
            """, DateTimeOffset.UtcNow);

        WriteBlk("common.blk", "not a controls file", DateTimeOffset.UtcNow.AddDays(1));

        var result = BlkFileDetector.FindBestBlkFile(_tempRoot);

        result.Should().Be(activeMachine);
        result.Should().NotBe(oldTopLevel);
    }

    [Fact]
    public void Falls_back_to_last_controller_when_no_key_binding_file_is_detected()
    {
        var fallback = WriteBlk("LAST-CONTROLER.blk", "legacy export", DateTimeOffset.UtcNow.AddDays(-1));
        WriteBlk(Path.Combine("last", "production", "storage.blk"), "not controls", DateTimeOffset.UtcNow);

        BlkFileDetector.FindBestBlkFile(_tempRoot).Should().Be(fallback);
    }

    private string WriteBlk(string relativePath, string content, DateTimeOffset lastWriteTime)
    {
        var path = Path.Combine(_tempRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteTime.UtcDateTime);
        return path;
    }
}
