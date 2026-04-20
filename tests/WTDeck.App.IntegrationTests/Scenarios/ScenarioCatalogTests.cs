using FluentAssertions;

namespace WTDeck.App.IntegrationTests.Scenarios;

public sealed class ScenarioCatalogTests
{
    [Fact]
    public async Task All_checked_in_scenarios_pass_validation_gates()
    {
        var scenariosDirectory = FindScenariosDirectory();
        var scenarioFiles = Directory
            .GetFiles(scenariosDirectory.FullName, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName)
            .ToArray();

        scenarioFiles.Should().NotBeEmpty();

        foreach (var scenarioFile in scenarioFiles)
            await ScenarioValidationRunner.ValidateAsync(scenarioFile);
    }

    private static DirectoryInfo FindScenariosDirectory()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "scenarios");
                if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*.json", SearchOption.TopDirectoryOnly).Any())
                    return new DirectoryInfo(candidate);

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the scenarios directory from the test output path.");
    }
}
