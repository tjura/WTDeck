using WTDeck.Core.Alerts;
using WTDeck.Core.Models;
using WTDeck.Core.Profiles;

namespace WTDeck.Core.Rules;

/// <summary>
/// Input to a rule evaluation. Carries the current and previous telemetry
/// snapshots plus resolved profile and the alert center so rules can both read
/// state and raise/clear alerts side-effectfully.
/// </summary>
public sealed record RuleContext(
    FlightSnapshot? Current,
    FlightSnapshot? Previous,
    AircraftProfile Profile,
    IAlertCenter AlertCenter,
    DateTimeOffset Now);
