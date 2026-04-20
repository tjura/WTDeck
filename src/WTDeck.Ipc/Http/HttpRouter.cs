using System.Net;

namespace WTDeck.Ipc.Http;

public sealed class HttpRouter
{
    private readonly List<Route> _routes = [];

    public void Map(string method, string pathPattern, Func<HttpListenerContext, RouteMatch, CancellationToken, Task> handler)
    {
        _routes.Add(new Route(method, pathPattern, handler));
    }

    public async Task<bool> DispatchAsync(HttpListenerContext context, CancellationToken ct)
    {
        var method = context.Request.HttpMethod;
        var path = context.Request.Url?.AbsolutePath ?? "/";

        foreach (var route in _routes)
        {
            if (!route.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
                continue;

            var match = TryMatch(route.PathPattern, path);
            if (match is null)
                continue;

            await route.Handler(context, match, ct);
            return true;
        }

        return false;
    }

    public static RouteMatch? TryMatch(string pattern, string path)
    {
        var patternSegments = pattern.Trim('/').Split('/');
        var pathSegments = path.Trim('/').Split('/');

        if (patternSegments.Length != pathSegments.Length)
            return null;

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < patternSegments.Length; i++)
        {
            var p = patternSegments[i];
            var s = pathSegments[i];

            if (p.StartsWith('{') && p.EndsWith('}'))
            {
                var name = p[1..^1];
                parameters[name] = Uri.UnescapeDataString(s);
            }
            else if (!p.Equals(s, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return new RouteMatch(parameters);
    }

    private sealed record Route(string Method, string PathPattern, Func<HttpListenerContext, RouteMatch, CancellationToken, Task> Handler);
}

public sealed class RouteMatch
{
    public IReadOnlyDictionary<string, string> Parameters { get; }

    public RouteMatch(IReadOnlyDictionary<string, string> parameters)
    {
        Parameters = parameters;
    }

    public string? GetString(string name) => Parameters.TryGetValue(name, out var value) ? value : null;
}
