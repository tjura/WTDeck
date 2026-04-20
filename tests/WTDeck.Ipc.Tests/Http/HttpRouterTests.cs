using FluentAssertions;
using WTDeck.Ipc.Http;

namespace WTDeck.Ipc.Tests.Http;

public class HttpRouterTests
{
    [Fact]
    public void Matches_exact_static_path()
    {
        var match = HttpRouter.TryMatch("/api/health", "/api/health");
        match.Should().NotBeNull();
    }

    [Fact]
    public void Matches_path_with_parameter()
    {
        var match = HttpRouter.TryMatch("/api/actions/{actionKey}", "/api/actions/landing-gear");
        match.Should().NotBeNull();
        match!.GetString("actionKey").Should().Be("landing-gear");
    }

    [Fact]
    public void Fails_to_match_different_segment_count()
    {
        var match = HttpRouter.TryMatch("/api/actions/{actionKey}", "/api/actions/landing-gear/extra");
        match.Should().BeNull();
    }

    [Fact]
    public void Fails_to_match_different_literal()
    {
        var match = HttpRouter.TryMatch("/api/health", "/api/status");
        match.Should().BeNull();
    }

    [Fact]
    public void Parameter_value_is_url_decoded()
    {
        var match = HttpRouter.TryMatch("/api/actions/{actionKey}", "/api/actions/landing%2Dgear");
        match.Should().NotBeNull();
        match!.GetString("actionKey").Should().Be("landing-gear");
    }
}
