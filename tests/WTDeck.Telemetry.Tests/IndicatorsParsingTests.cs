using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WTDeck.Telemetry;

namespace WTDeck.Telemetry.Tests;

public class IndicatorsParsingTests
{
    private static WarThunderTelemetrySource CreateSource(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var options = new TelemetryOptions { BaseUrl = "http://localhost:8111" };
        return new WarThunderTelemetrySource(client, options, NullLogger<WarThunderTelemetrySource>.Instance);
    }

    private static FakeHandler Ok(string indicatorsJson, string stateJson) =>
        new(indicatorsJson, stateJson);

    [Fact]
    public async Task Valid_json_parses_correctly()
    {
        var indicators = """{"valid":true,"type":"bf-109g-6","gears":1.0,"gears_lamp":0.0}""";
        var state = """{"valid":true,"gear, %":100}""";
        var source = CreateSource(Ok(indicators, state));

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Valid.Should().BeTrue();
        result.AircraftType.Should().Be("bf-109g-6");
        result.Gear.Should().Be(1.0f); // 100 / 100
        result.GearsLamp.Should().Be(0.0f);
        source.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task State_provides_actual_gear_position_during_transit()
    {
        // /indicators reports the handle command (gears=1, handle down)
        // /state reports the actual position (gear% = 41, mid-transit)
        // The mapper must prefer /state for the gear field.
        var indicators = """{"valid":true,"type":"a_4n","gears":1.0,"gears_lamp":0.0}""";
        var state = """{"valid":true,"gear, %":41}""";
        var source = CreateSource(Ok(indicators, state));

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Gear.Should().BeApproximately(0.41f, 0.001f);
    }

    [Fact]
    public async Task Missing_gears_field_defaults_to_zero()
    {
        var indicators = """{"valid":true,"type":"bf-109g-6"}""";
        var state = """{"valid":true}""";
        var source = CreateSource(Ok(indicators, state));

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Gear.Should().Be(0.0f);
    }

    [Fact]
    public async Task Falls_back_to_indicators_when_state_invalid()
    {
        // If /state reports valid=false, the mapper should fall back to the
        // /indicators "gears" handle field instead of zeroing the position.
        var indicators = """{"valid":true,"type":"bf-109g-6","gears":0.75,"gears_lamp":0.0}""";
        var state = """{"valid":false}""";
        var source = CreateSource(Ok(indicators, state));

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Gear.Should().BeApproximately(0.75f, 0.001f);
    }

    [Fact]
    public async Task Invalid_json_returns_null()
    {
        var source = CreateSource(Ok("not json at all", "not json at all"));

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().BeNull();
        source.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_response_returns_null()
    {
        var source = CreateSource(Ok("", ""));

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Http_error_returns_null()
    {
        var source = CreateSource(new FakeHandler(
            indicatorsJson: "",
            stateJson: "",
            statusCode: HttpStatusCode.InternalServerError));

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().BeNull();
        source.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Connection_failure_returns_null()
    {
        var source = CreateSource(new ThrowingHandler());

        var result = await source.GetCurrentStateAsync(CancellationToken.None);

        result.Should().BeNull();
        source.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Recovery_after_failure_restores_availability()
    {
        var toggleHandler = new ToggleHandler();
        var source = CreateSource(toggleHandler);

        toggleHandler.ShouldFail = true;
        await source.GetCurrentStateAsync(CancellationToken.None);
        source.IsAvailable.Should().BeFalse();

        toggleHandler.ShouldFail = false;
        var result = await source.GetCurrentStateAsync(CancellationToken.None);
        source.IsAvailable.Should().BeTrue();
        result.Should().NotBeNull();
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly string _indicatorsJson;
        private readonly string _stateJson;
        private readonly HttpStatusCode _statusCode;

        public FakeHandler(string indicatorsJson, string stateJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _indicatorsJson = indicatorsJson;
            _stateJson = stateJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var body = path.EndsWith("/state", StringComparison.OrdinalIgnoreCase)
                ? _stateJson
                : _indicatorsJson;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            throw new HttpRequestException("Connection refused");
        }
    }

    private class ToggleHandler : HttpMessageHandler
    {
        public bool ShouldFail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (ShouldFail)
                throw new HttpRequestException("Connection refused");

            var path = request.RequestUri?.AbsolutePath ?? "";
            var body = path.EndsWith("/state", StringComparison.OrdinalIgnoreCase)
                ? """{"valid":true,"gear, %":50}"""
                : """{"valid":true,"type":"bf-109g-6","gears":0.5,"gears_lamp":0.0}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
