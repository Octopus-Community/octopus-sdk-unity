using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Guards <see cref="OctopusScenarioCatalog"/> against silent drift from
/// `pm-tools/shared/config/scenarios-catalog.yaml`, the single source of truth for every
/// Octopus sample and for the QA pipeline (see the class doc comment on the catalogue itself).
/// </summary>
public class OctopusScenarioCatalogTests
{
    [TestCase("groups")]
    [TestCase("syncFollowGroups")]
    [TestCase("groupAccessDenied")]
    [TestCase("communityAccess")]
    [TestCase("contentOptions")]
    [TestCase("reactions")]
    [TestCase("bridge")]
    [TestCase("createPost")]
    public void CommunityScenarioIsListed(string id)
    {
        var row = OctopusScenarioCatalog.All.First(s => s.Id == id);
        Assert.IsTrue(OctopusScenarioSections.IsListed(row));
        var pilot = OctopusScenarioPilots.Create(id);
        CollectionAssert.AreEqual(row.PresetTestIds, pilot.Presets.Select(p => p.TestId).ToArray());
        Assert.AreEqual(row.ResultTestId, pilot.ResultTestId);
    }

    [TestCase("events")]
    [TestCase("pushNotifications")]
    [TestCase("notSeenNotifications")]
    [TestCase("lifecycle")]
    [TestCase("forceOctopusABTests")]
    [TestCase("trackABTests")]
    public void PortedPilotIsListed(string id)
    {
        var row = OctopusScenarioCatalog.All.Single(s => s.Id == id);
        Assert.IsTrue(OctopusScenarioSections.IsListed(row));
        var pilot = OctopusScenarioPilots.Create(id);
        Assert.AreEqual(row.ResultTestId, pilot.ResultTestId);
        CollectionAssert.AreEqual(row.PresetTestIds, pilot.Presets.Select(p => p.TestId));
    }

    [Test]
    public void CatalogueListingFollowsThePilotRegistryAndRetainsInapplicableReasons()
    {
        CollectionAssert.AreEquivalent(OctopusScenarioPilots.Ids,
            OctopusScenarioCatalog.All.Where(OctopusScenarioSections.IsListed).Select(row => row.Id));
        var inapplicable = OctopusScenarioCatalog.All.Where(row => row.NotApplicableReason != null).ToArray();
        CollectionAssert.AreEquivalent(new[] { "fullscreen", "modal", "sheet", "embeddedBack" },
            inapplicable.Select(row => row.Id));
        foreach (var row in inapplicable)
        {
            Assert.IsNotEmpty(row.NotApplicableReason, row.Id);
            Assert.IsFalse(OctopusScenarioSections.IsListed(row), row.Id);
        }
    }

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
    public void EveryDemoSceneNamesASceneThatExists()
    {
        // `DemoScene` is the absorption ledger for issue #100 — which legacy scene each pending
        // scenario has to take over — and since the Scenarios tab stopped listing unimplemented
        // scenarios, nothing on screen would notice it pointing at a scene that was renamed away.
        foreach (var scenario in OctopusScenarioCatalog.All)
        {
            if (scenario.DemoScene == null) continue;

            var path = Path.Combine(Application.dataPath,
                                    scenario.DemoScene + "/" + scenario.DemoScene + ".unity");
            Assert.IsTrue(File.Exists(path),
                "Scenario '" + scenario.Id + "' names the legacy scene '" + scenario.DemoScene +
                "', which is not at " + path + ".");
        }
    }

    [TestCase("termsAcceptance")]
    [TestCase("profileFieldsLock")]
    [TestCase("communityData")]
    [TestCase("theme")]
    [TestCase("refreshEntitlements")]
    public void PortedScenarioIsListedWithEveryCataloguePreset(string id)
    {
        var row = OctopusScenarioCatalog.All.First(s => s.Id == id);
        Assert.IsTrue(OctopusScenarioSections.IsListed(row));
        var pilot = OctopusScenarioPilots.Create(id);
        CollectionAssert.AreEqual(row.PresetTestIds, pilot.Presets.Select(p => p.TestId).ToArray());
        Assert.AreEqual(row.ResultTestId, pilot.ResultTestId);
    }

    [Test]
    public void EmbeddedBackScenarioIsPresent()
    {
        var embeddedBack = OctopusScenarioCatalog.All.FirstOrDefault(s => s.Id == "embeddedBack");
        Assert.IsNotNull(embeddedBack, "The 'embeddedBack' scenario is missing from the catalogue.");
        Assert.IsFalse(OctopusScenarioSections.IsListed(embeddedBack));
        Assert.IsNotEmpty(embeddedBack.NotApplicableReason);
        Assert.AreEqual(5, embeddedBack.PresetTestIds.Length);
        Assert.AreEqual("embeddedBack-result", embeddedBack.ResultTestId);
    }
}
