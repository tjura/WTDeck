using FluentAssertions;

namespace WTDeck.Telemetry.Tests;

public sealed class TelemetryScenarioFileTests
{
    [Fact]
    public void LoadFromFile_reads_steps_expectations_and_commands()
    {
        var path = WriteScenarioFile(
            """
            {
              "name": "gear-cycle",
              "stepIntervalMs": 25,
              "steps": [
                {
                  "name": "gear-up",
                  "indicators": { "valid": true, "type": "a_4n", "army": "air", "gears": 0, "gears_lamp": 0 },
                  "state": { "valid": true, "IAS, km/h": 320, "gear, %": 0 },
                  "expectTelemetry": { "available": true, "valid": true, "aircraftType": "a_4n", "gearPercent": 0, "gear": 0, "indicatedAirspeedKmh": 320 },
                  "expectUi": { "actionKey": "landing-gear", "title": "GEAR UP", "statusKey": "up", "isBlinking": false, "isEnabled": true, "alertLevel": "None" },
                  "commands": [
                    {
                      "actionKey": "landing-gear",
                      "expectedScanCodes": [34],
                      "expectedUi": { "actionKey": "landing-gear", "title": "GEAR UP" }
                    }
                  ]
                }
              ]
            }
            """);

        var scenario = TelemetryScenarioFile.LoadFromFile(path);

        scenario.Name.Should().Be("gear-cycle");
        scenario.StepIntervalMs.Should().Be(25);
        scenario.Steps.Should().HaveCount(1);
        scenario.Steps[0].Name.Should().Be("gear-up");
        scenario.Steps[0].ExpectTelemetry!.AircraftType.Should().Be("a_4n");
        scenario.Steps[0].ExpectUi!.StatusKey.Should().Be("up");
        scenario.Steps[0].Commands.Should().ContainSingle();
        scenario.Steps[0].Commands[0].ExpectedScanCodes.Should().Equal(34);
    }

    [Fact]
    public void LoadFromFile_rejects_empty_step_list()
    {
        var path = WriteScenarioFile(
            """
            {
              "name": "empty",
              "steps": []
            }
            """);

        var act = () => TelemetryScenarioFile.LoadFromFile(path);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one step*");
    }

    private static string WriteScenarioFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
