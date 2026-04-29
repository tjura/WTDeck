using FluentAssertions;
using WTDeck.Core.Contracts;
using WTDeck.Core.FlightAlerts;
using WTDeck.Core.Profiles.Aircraft;
using WTDeck.Core.Tests.TestDoubles;

namespace WTDeck.Core.Tests.FlightAlerts;

public class FlightAlertPanelEvaluatorTests
{
    private readonly FlightAlertPanelEvaluator _evaluator = new();

    [Fact]
    public void No_telemetry_is_unavailable_and_dim()
    {
        var result = _evaluator.Evaluate(null, A4NSkyhawkProfile.Instance);

        result.Panel.IsAvailable.Should().BeFalse();
        result.Panel.StatusKey.Should().Be(StreamDockPanelState.StatusUnavailable);
        result.Alerts[StreamDockAlertKeys.OverG].StatusKey.Should().Be(StreamDockAlertState.StatusUnavailable);
        result.Alerts[StreamDockAlertKeys.OverG].IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Invalid_telemetry_is_unavailable_and_dim()
    {
        var snapshot = new FlightSnapshotBuilder().Invalid().WithNy(12f).Build();

        var result = _evaluator.Evaluate(snapshot, A4NSkyhawkProfile.Instance);

        result.Panel.IsAvailable.Should().BeFalse();
        result.Alerts[StreamDockAlertKeys.OverG].Value.Should().Be("--");
        result.Alerts[StreamDockAlertKeys.OverG].AlertLevel.Should().Be("None");
    }

    [Fact]
    public void Negative_g_is_clamped_to_zero_and_normal()
    {
        var snapshot = new FlightSnapshotBuilder().WithNy(-12f).Build();

        var result = _evaluator.Evaluate(snapshot, A4NSkyhawkProfile.Instance);

        var alert = result.Alerts[StreamDockAlertKeys.OverG];
        result.Panel.StatusKey.Should().Be(StreamDockPanelState.StatusNormal);
        alert.Value.Should().Be("0.0");
        alert.NumericValue.Should().Be(0f);
        alert.StatusKey.Should().Be(StreamDockAlertState.StatusNormal);
        alert.AlertLevel.Should().Be("None");
    }

    [Fact]
    public void Nine_point_nine_g_is_normal()
    {
        var snapshot = new FlightSnapshotBuilder().WithNy(9.9f).Build();

        var result = _evaluator.Evaluate(snapshot, A4NSkyhawkProfile.Instance);

        result.Panel.StatusKey.Should().Be(StreamDockPanelState.StatusNormal);
        result.Alerts[StreamDockAlertKeys.OverG].Value.Should().Be("9.9");
        result.Alerts[StreamDockAlertKeys.OverG].AlertLevel.Should().Be("None");
    }

    [Fact]
    public void Ten_g_is_warning()
    {
        var snapshot = new FlightSnapshotBuilder().WithNy(10.0f).Build();

        var result = _evaluator.Evaluate(snapshot, A4NSkyhawkProfile.Instance);

        result.Panel.StatusKey.Should().Be(StreamDockPanelState.StatusWarning);
        result.Alerts[StreamDockAlertKeys.OverG].StatusKey.Should().Be(StreamDockAlertState.StatusWarning);
        result.Alerts[StreamDockAlertKeys.OverG].AlertLevel.Should().Be("Warning");
    }

    [Fact]
    public void Eleven_g_is_danger()
    {
        var snapshot = new FlightSnapshotBuilder().WithNy(11.0f).Build();

        var result = _evaluator.Evaluate(snapshot, A4NSkyhawkProfile.Instance);

        result.Panel.StatusKey.Should().Be(StreamDockPanelState.StatusDanger);
        result.Alerts[StreamDockAlertKeys.OverG].StatusKey.Should().Be(StreamDockAlertState.StatusDanger);
        result.Alerts[StreamDockAlertKeys.OverG].AlertLevel.Should().Be("Danger");
    }
}
