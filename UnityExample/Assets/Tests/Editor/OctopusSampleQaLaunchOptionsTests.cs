using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class OctopusSampleQaLaunchOptionsTests
{
    [Test]
    public void EmptyOptionsDoNothing()
    {
        var options = Parse();
        Assert.IsNull(options.Error);
        Assert.IsNull(options.Tab);
        Assert.IsNull(options.Scenario);
        Assert.IsNull(options.Preset);
        Assert.IsFalse(options.AutoStart);
        Assert.AreEqual("tab=none scenario=none preset=none autoStart=false", options.Summary);
        var navigation = new Navigation();
        Assert.IsFalse(new OctopusSampleQaRequest(options).Apply(navigation).MoveNext());
        Assert.IsNull(navigation.Tab);
    }

    [TestCase("home")]
    [TestCase("scenarios")]
    [TestCase("community")]
    [TestCase("settings")]
    public void EveryTabIsParsedAndSelected(string tab)
    {
        var options = Parse("qaTab", tab);
        Assert.IsNull(options.Error);
        Assert.AreEqual(tab, options.Tab);
        var navigation = new Navigation { Ready = true };
        Assert.IsFalse(new OctopusSampleQaRequest(options).Apply(navigation).MoveNext());
        Assert.AreEqual(tab, navigation.Tab);
        Assert.AreEqual(0, navigation.OpenCount);
    }

    [Test]
    public void EveryCatalogIdIsAcceptedWithoutRequiringAPilot()
    {
        foreach (var scenario in OctopusScenarioCatalog.All)
        {
            var options = Parse("qaScenario", scenario.Id);
            Assert.IsNull(options.Error, scenario.Id);
            Assert.AreEqual(scenario.Id, options.Scenario);
        }
    }

    [TestCase("qaTab", "unknown", "unknown tab")]
    [TestCase("qaTab", "HOME", "unknown tab")]
    [TestCase("qaScenario", "unknown", "unknown scenario")]
    [TestCase("qaPreset", "one", "unknown preset")]
    [TestCase("qaPreset", "0", "unknown preset")]
    [TestCase("qaPreset", "-1", "unknown preset")]
    [TestCase("qaPreset", "999999999999", "unknown preset")]
    [TestCase("qaPreset", "1", "preset requires scenario")]
    [TestCase("qaAutoStart", "yes", "invalid autoStart")]
    public void InvalidOptionsAreRejectedWithoutNavigation(string key, string value, string error)
    {
        var options = Parse(key, value);
        Assert.AreEqual(error, options.Error);
        var navigation = new Navigation();
        Assert.IsFalse(new OctopusSampleQaRequest(options).Apply(navigation).MoveNext());
        Assert.IsNull(navigation.Tab);
    }

    [TestCase("true", true)]
    [TestCase("TRUE", true)]
    [TestCase("false", false)]
    public void AutoStartInvokesTheConfigActionOnceEvenOnReentry(string value, bool expected)
    {
        var options = Parse("qaAutoStart", value);
        Assert.IsNull(options.Error);
        Assert.AreEqual(expected, options.AutoStart);
        var request = new OctopusSampleQaRequest(options);
        var calls = 0;
        System.Action start = null;
        start = () => { calls++; request.ConfigShown(start); };
        request.ConfigShown(start);
        request.ConfigShown(start);
        Assert.AreEqual(expected ? 1 : 0, calls);
    }

    [Test]
    public void ScenarioTakesPrecedenceAndWaitsForTheTabBeforeRunningOnce()
    {
        var options = Parse("qaTab", "home", "qaScenario", "customEvents", "qaPreset", "1");
        Assert.AreEqual(1, options.Preset);
        var request = new OctopusSampleQaRequest(options);
        var navigation = new Navigation();
        var apply = request.Apply(navigation);
        Assert.IsTrue(apply.MoveNext());
        Assert.AreEqual("scenarios", navigation.Tab);
        Assert.AreEqual(0, navigation.OpenCount);
        Assert.IsFalse(request.Apply(navigation).MoveNext(), "A second shell cannot replay the request.");
        navigation.Ready = true;
        Assert.IsFalse(apply.MoveNext());
        Assert.AreEqual("customEvents", navigation.Scenario);
        Assert.AreEqual(1, navigation.OpenCount);
        Assert.AreEqual(1, navigation.RunCount);
        Assert.AreEqual(1, navigation.Preset);
    }

    [Test]
    public void ScenarioWithoutPresetOnlyOpens()
    {
        var navigation = new Navigation { Ready = true };
        var request = new OctopusSampleQaRequest(Parse("qaScenario", "customEvents"));
        Assert.IsFalse(request.Apply(navigation).MoveNext());
        Assert.AreEqual(1, navigation.OpenCount);
        Assert.AreEqual(0, navigation.RunCount);
    }

    [Test]
    public void CatalogEntryWithoutPilotLogsAnErrorWithoutNavigating()
    {
        string missing = null;
        foreach (var scenario in OctopusScenarioCatalog.All)
            if (!OctopusScenarioPilots.Has(scenario.Id)) { missing = scenario.Id; break; }
        Assert.IsNotNull(missing);
        var request = new OctopusSampleQaRequest(Parse("qaScenario", missing));
        var navigation = new Navigation { Ready = true };
        LogAssert.Expect(LogType.Log, "[OctopusQA] launch-options=error unknown scenario");
        Assert.IsFalse(request.Apply(navigation).MoveNext());
        Assert.IsNull(navigation.Tab);
        Assert.AreEqual(0, navigation.OpenCount);
    }

    [Test]
    public void UnavailablePresetLogsAnErrorAndIsNotRetried()
    {
        var request = new OctopusSampleQaRequest(Parse("qaScenario", "customEvents", "qaPreset", "999"));
        var navigation = new Navigation { Ready = true, PresetExists = false };
        LogAssert.Expect(LogType.Log, "[OctopusQA] launch-options=error unknown preset");
        Assert.IsFalse(request.Apply(navigation).MoveNext());
        Assert.IsFalse(request.Apply(navigation).MoveNext());
        Assert.AreEqual(1, navigation.RunCount);
    }

    [Test]
    public void EverySampleDestinationIsAcceptedOnTheScenarioExtra()
    {
        // QA addresses the sample's own screens with the same flag as a catalogue scenario:
        // `--es qaScenario arcade --ez qaAutoStart true`.
        foreach (var id in OctopusSampleQaDestinations.Ids)
        {
            var options = Parse("qaScenario", id);
            Assert.IsNull(options.Error, id);
            Assert.IsNull(options.Scenario, id);
            Assert.AreEqual(id, options.Destination, id);
            Assert.AreEqual("tab=none scenario=" + id + " preset=none autoStart=false", options.Summary);
        }
    }

    [Test]
    public void NoDestinationSharesAnIdWithTheSharedCatalog()
    {
        // The catalogue is the cross-platform contract; a destination that shadowed one of its ids
        // would silently send QA somewhere else than the other SDKs.
        foreach (var scenario in OctopusScenarioCatalog.All)
            Assert.IsFalse(OctopusSampleQaDestinations.Has(scenario.Id), scenario.Id);
    }

    [Test]
    public void ArcadeOpensFromItsOwnTab()
    {
        var options = Parse("qaScenario", OctopusSampleQaDestinations.Arcade);
        var navigation = new Navigation { Ready = true };
        Assert.IsFalse(new OctopusSampleQaRequest(options).Apply(navigation).MoveNext());
        Assert.AreEqual("home", navigation.Tab);
        Assert.AreEqual(OctopusSampleQaDestinations.Arcade, navigation.Destination);
        Assert.AreEqual(1, navigation.DestinationCount);
        Assert.AreEqual(0, navigation.OpenCount);
    }

    [Test]
    public void ADestinationTakesNoPreset()
    {
        var options = Parse("qaScenario", OctopusSampleQaDestinations.Arcade, "qaPreset", "1");
        Assert.AreEqual("preset requires scenario", options.Error);
        var navigation = new Navigation { Ready = true };
        Assert.IsFalse(new OctopusSampleQaRequest(options).Apply(navigation).MoveNext());
        Assert.AreEqual(0, navigation.DestinationCount);
    }

    [Test]
    public void ARefusedDestinationLogsAnError()
    {
        var options = Parse("qaScenario", OctopusSampleQaDestinations.Arcade);
        var navigation = new Navigation { Ready = true, DestinationExists = false };
        LogAssert.Expect(LogType.Log, "[OctopusQA] launch-options=error unknown destination");
        Assert.IsFalse(new OctopusSampleQaRequest(options).Apply(navigation).MoveNext());
        Assert.AreEqual(1, navigation.DestinationCount);
    }

    [Test]
    public void MarkersCollapseEveryLineSeparator()
    {
        Assert.AreEqual("one two three four five six",
            OctopusSampleQaLaunch.SingleLine("one\r\ntwo\nthree\rfour\u2028five\u2029six"));
        LogAssert.Expect(LogType.Log, "[OctopusQA] scenario=customEvents result=one two");
        OctopusSampleQaLaunch.Log("scenario=customEvents result=one\ntwo");
    }

    private static OctopusSampleQaLaunchOptions Parse(params string[] pairs)
    {
        var extras = new Dictionary<string, string>();
        for (var i = 0; i < pairs.Length; i += 2) extras.Add(pairs[i], pairs[i + 1]);
        return OctopusSampleQaLaunchOptions.Parse(extras);
    }

    private sealed class Navigation : IOctopusSampleQaNavigation, IOctopusSampleQaScenario
    {
        public string Tab, Scenario, Destination;
        public bool Ready, PresetExists = true, DestinationExists = true;
        public int OpenCount, RunCount, Preset, DestinationCount;
        public void SelectTab(string id) { Tab = id; }
        public bool IsTabReady(string id) { return Ready && Tab == id; }
        public IOctopusSampleQaScenario OpenScenario(string id)
        {
            Scenario = id;
            OpenCount++;
            return this;
        }
        public bool OpenDestination(string id)
        {
            Destination = id;
            DestinationCount++;
            return DestinationExists;
        }
        public bool RunPreset(int number)
        {
            Preset = number;
            RunCount++;
            return PresetExists;
        }
    }
}
