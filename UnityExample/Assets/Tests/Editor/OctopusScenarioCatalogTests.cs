using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Guards <see cref="OctopusScenarioCatalog"/> against silent drift from
/// `pm-tools/shared/config/scenarios-catalog.yaml`, the single source of truth for every
/// Octopus sample and for the QA pipeline (see the class doc comment on the catalogue itself).
/// </summary>
public class OctopusScenarioCatalogTests
{
    [Test]
    public void CatalogHasTwentySevenScenarios()
    {
        Assert.AreEqual(27, OctopusScenarioCatalog.All.Count);
    }

    [Test]
    public void ScenarioIdsAreUnique()
    {
        var ids = OctopusScenarioCatalog.All.Select(s => s.Id).ToList();
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Duplicate scenario id found.");
    }

    [Test]
    public void ResultTestIdFollowsTheIdDashResultConvention()
    {
        foreach (var scenario in OctopusScenarioCatalog.All)
        {
            Assert.AreEqual(scenario.Id + "-result", scenario.ResultTestId,
                $"Scenario '{scenario.Id}' has a ResultTestId that does not follow the " +
                "'<id>-result' convention used by the catalogue.");
        }
    }

    [Test]
    public void EveryScenarioHasAtLeastOnePreset()
    {
        foreach (var scenario in OctopusScenarioCatalog.All)
        {
            Assert.IsNotNull(scenario.PresetTestIds, $"Scenario '{scenario.Id}' has null PresetTestIds.");
            Assert.IsNotEmpty(scenario.PresetTestIds, $"Scenario '{scenario.Id}' has no preset test ids.");
        }
    }

    [Test]
    public void EveryPresetTestIdIsPrefixedQaPreset()
    {
        foreach (var scenario in OctopusScenarioCatalog.All)
        {
            foreach (var presetTestId in scenario.PresetTestIds)
            {
                Assert.IsTrue(presetTestId.StartsWith("qa-preset-"),
                    $"Preset test id '{presetTestId}' on scenario '{scenario.Id}' does not start " +
                    "with the 'qa-preset-' prefix.");
            }
        }
    }

    [Test]
    public void NoPresetTestIdIsDuplicatedAcrossScenarios()
    {
        var allPresetIds = OctopusScenarioCatalog.All.SelectMany(s => s.PresetTestIds).ToList();
        var duplicates = allPresetIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.IsEmpty(duplicates, "Duplicate preset test id(s) found: " + string.Join(", ", duplicates));
    }

    [Test]
    public void EmbeddedBackScenarioIsPresent()
    {
        var embeddedBack = OctopusScenarioCatalog.All.FirstOrDefault(s => s.Id == "embeddedBack");
        Assert.IsNotNull(embeddedBack, "The 'embeddedBack' scenario is missing from the catalogue.");
        Assert.AreEqual(ScenarioStatus.NotApplicable, embeddedBack.Status);
        Assert.AreEqual(5, embeddedBack.PresetTestIds.Length);
        Assert.AreEqual("embeddedBack-result", embeddedBack.ResultTestId);
    }
}
