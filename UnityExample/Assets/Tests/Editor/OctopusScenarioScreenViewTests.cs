using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Proves the three parts of the SDK_STANDARDS §5.2 contract that are observable without a device:
/// the QA handles exist under the exact names the shared catalogue publishes, the screen is usable
/// the moment it opens, and opening it mutates nothing.
///
/// Everything here is asserted through <c>GameObject.name</c> rather than through uGUI types: the
/// test assembly sets `overrideReferences` with `nunit.framework.dll` as its only precompiled
/// reference, so it cannot name a `UnityEngine.UI` type — and the name is what the QA pipeline
/// actually addresses anyway.
///
/// These are EditMode tests, so `Start` never runs; <see cref="OctopusScenarioScreenView.Open"/>
/// builds the screen itself, which is why it can be tested at all.
/// </summary>
public class OctopusScenarioScreenViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [Test]
    public void ConnectionScreenExposesTheCatalogPresetIds()
    {
        AssertHandles("connection",
                      new[]
                      {
                          "qa-preset-connection-1",
                          "qa-preset-connection-2",
                          "qa-preset-connection-3",
                          "qa-preset-connection-4",
                          "qa-preset-connection-5",
                      },
                      "connection-result");
    }

    [Test]
    public void CustomEventsScreenExposesTheCatalogPresetIds()
    {
        AssertHandles("customEvents",
                      new[] { "qa-preset-customEvents-1", "qa-preset-customEvents-2" },
                      "customEvents-result");
    }

    [Test]
    public void LocaleScreenExposesTheCatalogPresetIds()
    {
        AssertHandles("locale",
                      new[] { "qa-preset-locale-1", "qa-preset-locale-2", "qa-preset-locale-3" },
                      "locale-result");
    }

    [Test]
    public void OpeningAScreenMakesNoSdkCall()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        foreach (var id in OctopusScenarioPilots.Ids)
        {
            Open(id);
            TearDownSpawned();
        }

        Assert.IsEmpty(probe.Methods,
            "Opening a scenario screen reached the SDK: " + string.Join(", ", probe.Methods) +
            " — §5.2 asks for no mutation without a user action.");
    }

    [Test]
    public void AScreenIsUsableOnEntryWithEveryFieldPreFilled()
    {
        foreach (var id in OctopusScenarioPilots.Ids)
        {
            var pilot = OctopusScenarioPilots.Create(id);
            OpenFor(pilot);

            foreach (var field in pilot.Fields.All)
            {
                Assert.IsNotEmpty(field.Value,
                    "Scenario '" + id + "' opens with field '" + field.Key + "' empty.");
            }
            TearDownSpawned();
        }
    }

    [Test]
    public void EveryPresetHasAnInputForEveryFieldItFills()
    {
        foreach (var id in OctopusScenarioPilots.Ids)
        {
            var pilot = OctopusScenarioPilots.Create(id);
            var view = OpenFor(pilot);

            foreach (var field in pilot.Fields.All)
            {
                var name = id + "-field-" + field.Key;
                Assert.IsNotNull(Find(view.gameObject.transform, name),
                    "Scenario '" + id + "' has no input named '" + name + "'.");
            }
            TearDownSpawned();
        }
    }

    [Test]
    public void ASecondOpenReturnsTheScreenAlreadyOpen()
    {
        var first = Open("locale");
        var second = OctopusScenarioScreenView.Open(OctopusScenarioPilots.Create("connection"));

        Assert.AreSame(first, second,
            "A second Open built a new screen on top of the open one.");
    }

    private void AssertHandles(string scenarioId, string[] presetTestIds, string resultTestId)
    {
        var view = Open(scenarioId);
        var root = view.gameObject.transform;

        foreach (var presetTestId in presetTestIds)
        {
            Assert.IsNotNull(Find(root, presetTestId),
                "No GameObject named '" + presetTestId + "' on the '" + scenarioId +
                "' screen — the QA pipeline addresses presets by that name.");
        }

        Assert.IsNotNull(Find(root, resultTestId),
            "No result panel named '" + resultTestId + "' on the '" + scenarioId + "' screen.");
    }

    private OctopusScenarioScreenView Open(string scenarioId)
    {
        return OpenFor(OctopusScenarioPilots.Create(scenarioId));
    }

    private OctopusScenarioScreenView OpenFor(OctopusScenarioPilot pilot)
    {
        var view = OctopusScenarioScreenView.Open(pilot);
        _spawned.Add(view.gameObject);
        return view;
    }

    /// <summary>Destroys what this test has opened so far, mid-test — Open is a no-op while one
    /// screen is still alive.</summary>
    private void TearDownSpawned()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.gameObject.name == name) return root;
        for (var i = 0; i < root.childCount; i++)
        {
            var found = Find(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> Methods = new List<string>();

        public void LogApiCall(string method, string detail = null)
        {
            Methods.Add(method);
        }
    }
}
