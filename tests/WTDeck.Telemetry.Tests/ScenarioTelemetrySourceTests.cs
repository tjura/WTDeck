using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace WTDeck.Telemetry.Tests;

public sealed class ScenarioTelemetrySourceTests
{
    [Fact]
    public async Task GetCurrentStateAsync_replays_steps_and_records_executions()
    {
        var scenario = new TelemetryScenarioFile
        {
            Name = "gear-cycle",
            StepIntervalMs = 10,
            Steps =
            [
                new TelemetryScenarioStep
                {
                    Name = "gear-up",
                    IndicatorsJson = """{"valid":true,"type":"a_4n","army":"air","gears":0,"gears_lamp":0}""",
                    StateJson = """{"valid":true,"IAS, km/h":300,"gear, %":0}"""
                },
                new TelemetryScenarioStep
                {
                    Name = "gear-down",
                    IndicatorsJson = """{"valid":true,"type":"a_4n","army":"air","gears":1,"gears_lamp":0}""",
                    StateJson = """{"valid":true,"IAS, km/h":250,"gear, %":100}"""
                }
            ]
        };

        var source = new ScenarioTelemetrySource(scenario, NullLogger<ScenarioTelemetrySource>.Instance);

        var first = await source.GetCurrentStateAsync(CancellationToken.None);
        var second = await source.GetCurrentStateAsync(CancellationToken.None);
        var third = await source.GetCurrentStateAsync(CancellationToken.None);
        var executions = await ReadAllAsync(source);

        first.Should().NotBeNull();
        first!.AircraftType.Should().Be("a_4n");
        first.GearPercent.Should().Be(0f);
        second.Should().NotBeNull();
        second!.GearPercent.Should().Be(100f);
        third.Should().BeEquivalentTo(second);

        executions.Should().HaveCount(2);
        executions[0].StepNumber.Should().Be(1);
        executions[0].Step.Name.Should().Be("gear-up");
        executions[0].Snapshot!.Gear.Should().Be(0f);
        executions[1].StepNumber.Should().Be(2);
        executions[1].Step.Name.Should().Be("gear-down");
        executions[1].Snapshot!.Gear.Should().Be(1f);
    }

    private static async Task<List<TelemetryScenarioExecution>> ReadAllAsync(ScenarioTelemetrySource source)
    {
        var executions = new List<TelemetryScenarioExecution>();
        await foreach (var execution in source.ReadExecutionsAsync(CancellationToken.None))
            executions.Add(execution);

        return executions;
    }
}
