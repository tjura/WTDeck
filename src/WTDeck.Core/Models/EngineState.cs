namespace WTDeck.Core.Models;

public sealed record EngineState(
    int Index,
    float ThrottlePercent,
    float Rpm,
    float OilTemperatureC,
    float ThrustKgf,
    float PowerHp,
    float EfficiencyPercent,
    float ManifoldPressureAtm);
