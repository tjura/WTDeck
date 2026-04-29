using FluentAssertions;
using WTDeck.App.Capture;

namespace WTDeck.App.IntegrationTests.Capture;

public sealed class WarThunder8111CaptureAnalyzerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"wtdeck-capture-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Analyze_reports_interesting_fields_near_marker()
    {
        Directory.CreateDirectory(_tempRoot);
        var segment = Path.Combine(_tempRoot, "segment-0001.jsonl");
        await File.WriteAllLinesAsync(segment,
        [
            """
            {"capturedAt":"2026-04-29T12:00:00Z","elapsedMs":1000,"kind":"sample","endpoint":"/indicators","path":"/indicators","status":200,"hash":"a","body":{"valid":true,"speed":100}}
            """,
            """
            {"capturedAt":"2026-04-29T12:00:05Z","elapsedMs":5000,"kind":"marker","marker":"missile_visible_or_warning_seen"}
            """,
            """
            {"capturedAt":"2026-04-29T12:00:06Z","elapsedMs":6000,"kind":"sample","endpoint":"/hudmsg","path":"/hudmsg?lastEvt=0&lastDmg=0","status":200,"hash":"b","body":{"events":[{"id":7,"text":"Missile launch"}]}}
            """,
            """
            {"capturedAt":"2026-04-29T12:00:07Z","elapsedMs":7000,"kind":"sample","endpoint":"/map_obj.json","path":"/map_obj.json","status":200,"hash":"c","body":[{"type":"aircraft","icon":"fighter","color":"red"},{"type":"weapon","icon":"missile","blink":true}]}
            """
        ]);

        var output = new StringWriter();
        var analyzer = new WarThunder8111CaptureAnalyzer(output);

        var exitCode = await analyzer.AnalyzeAsync(_tempRoot, CancellationToken.None);

        exitCode.Should().Be(0);
        var markdown = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "analysis.md"));
        markdown.Should().Contain("Missile launch");
        markdown.Should().Contain("$.events[0].text");
        markdown.Should().Contain("icon:missile");
        File.Exists(Path.Combine(_tempRoot, "analysis.json")).Should().BeTrue();
    }
}
