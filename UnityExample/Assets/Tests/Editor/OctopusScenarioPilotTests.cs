using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Guards the scenario pilots against the two ways they could quietly stop being QA-usable:
/// a preset id or label that drifts from `pm-tools/shared/config/scenarios-catalog.yaml`, and a
/// preset that leaves a field for the Tester to fill in by hand (SDK_STANDARDS §5.2).
///
/// The expected ids are spelled out as literals on purpose. Deriving them from
/// <see cref="OctopusScenarioCatalog"/> would make this file agree with the pilots by
/// construction — including when both are wrong. `OctopusScenarioCatalogTests` guards the
/// catalogue against the YAML; this file guards the pilots against the catalogue's values as a
/// human transcribed them here.
/// </summary>
public class OctopusScenarioPilotTests
{
    private static readonly string[] ConnectionPresetIds =
    {
        "qa-preset-connection-1",
        "qa-preset-connection-2",
        "qa-preset-connection-3",
        "qa-preset-connection-4",
        "qa-preset-connection-5",
    };

    private static readonly string[] CustomEventsPresetIds =
    {
        "qa-preset-customEvents-1",
        "qa-preset-customEvents-2",
    };

    private static readonly string[] LocalePresetIds =
    {
        "qa-preset-locale-1",
        "qa-preset-locale-2",
        "qa-preset-locale-3",
    };

    [TearDown]
    public void TearDown()
    {
        // A pilot constructed by a test must never leave a probe installed for the next one.
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [Test]
    public void PilotsCoverExactlyTheScenariosTheSampleDrives()
    {
        CollectionAssert.AreEqual(new[] { "connection", "customEvents", "locale", "theme", "refreshEntitlements", "termsAcceptance", "profileFieldsLock", "communityData", "groups", "syncFollowGroups", "groupAccessDenied", "communityAccess", "contentOptions", "reactions", "bridge", "createPost", "events", "trackABTests", "forceOctopusABTests", "lifecycle", "notSeenNotifications", "pushNotifications", "initialScreen" },
                                  OctopusScenarioPilots.Ids);
    }

    [Test]
    public void HasIsTrueForDrivenScenariosAndFalseForTheRest()
    {
        Assert.IsTrue(OctopusScenarioPilots.Has("connection"));
        Assert.IsTrue(OctopusScenarioPilots.Has("customEvents"));
        Assert.IsTrue(OctopusScenarioPilots.Has("locale"));

        // A catalogue scenario this sample has NOT built, and a plain typo.
        Assert.IsFalse(OctopusScenarioPilots.Has("fullscreen"));
        Assert.IsFalse(OctopusScenarioPilots.Has("Connection"));
    }

    [Test]
    public void CreateReturnsNullForAScenarioThisSampleDoesNotDrive()
    {
        Assert.IsNull(OctopusScenarioPilots.Create("fullscreen"));
    }

    [Test]
    public void ConnectionPresetIdsMatchTheCatalog()
    {
        AssertPresetIds(new ConnectionScenario(), ConnectionPresetIds);
    }

    [Test]
    public void CustomEventsPresetIdsMatchTheCatalog()
    {
        AssertPresetIds(new CustomEventsScenario(), CustomEventsPresetIds);
    }

    [Test]
    public void LocalePresetIdsMatchTheCatalog()
    {
        AssertPresetIds(new LocaleScenario(), LocalePresetIds);
    }

    [Test]
    public void ResultTestIdsMatchTheCatalog()
    {
        Assert.AreEqual("connection-result", new ConnectionScenario().ResultTestId);
        Assert.AreEqual("customEvents-result", new CustomEventsScenario().ResultTestId);
        Assert.AreEqual("locale-result", new LocaleScenario().ResultTestId);
    }

    [Test]
    public void EveryPresetLabelFollowsThePresetNShape()
    {
        foreach (var pilot in AllPilots())
        {
            for (var i = 0; i < pilot.Presets.Count; i++)
            {
                if (pilot.Presets[i].TestId.EndsWith("-clear"))
                {
                    Assert.AreEqual("Clear override (backend default)", pilot.Presets[i].Label);
                    continue;
                }
                // "Preset N · ", with U+00B7 MIDDLE DOT — the QA Tester matches on visible text.
                if (pilot.Presets[i].TestId.EndsWith("-clear"))
                {
                    Assert.AreEqual("Clear override (backend default)", pilot.Presets[i].Label);
                    continue;
                }
                var expectedPrefix = "Preset " + (i + 1) + " · ";
                Assert.IsTrue(pilot.Presets[i].Label.StartsWith(expectedPrefix),
                    "Scenario '" + pilot.Id + "' preset #" + (i + 1) + " is labelled '" +
                    pilot.Presets[i].Label + "', which does not start with '" + expectedPrefix + "'.");
                Assert.Greater(pilot.Presets[i].Label.Length, expectedPrefix.Length,
                    "Scenario '" + pilot.Id + "' preset #" + (i + 1) + " has an empty description.");
            }
        }
    }

    [Test]
    public void EveryPresetFillsEveryFieldWithANonEmptyValue()
    {
        foreach (var pilot in AllPilots())
        {
            foreach (var preset in pilot.Presets)
            {
                // Blank every field first: a preset that only writes some of them would otherwise
                // pass on the values a previous preset left behind.
                foreach (var field in pilot.Fields.All) field.Value = string.Empty;

                preset.Fill(pilot.Fields);

                foreach (var field in pilot.Fields.All)
                {
                    Assert.IsNotEmpty(field.Value,
                        "Preset '" + preset.TestId + "' left field '" + field.Key +
                        "' empty — §5.2 asks a preset to pre-fill everything, with nothing left " +
                        "for the Tester to type.");
                }
            }
        }
    }

    [Test]
    public void FillMakesNoSdkCall()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        foreach (var pilot in AllPilots())
        {
            foreach (var preset in pilot.Presets) preset.Fill(pilot.Fields);
        }

        Assert.IsEmpty(probe.Methods,
            "Filling fields reached the SDK: " + string.Join(", ", probe.Methods));
    }

    [Test]
    public void ConstructingAPilotMakesNoSdkCall()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        AllPilots();

        Assert.IsEmpty(probe.Methods,
            "Constructing a pilot reached the SDK: " + string.Join(", ", probe.Methods));
    }

    [Test]
    public void APilotStartsOnAResultLineThatSaysNothingHasRunYet()
    {
        foreach (var pilot in AllPilots())
        {
            Assert.IsNotEmpty(pilot.Result);
            Assert.IsTrue(pilot.Result.StartsWith("Ready"),
                "Scenario '" + pilot.Id + "' opens on '" + pilot.Result + "'.");
        }
    }

    [Test]
    public void FieldsRejectAnUnknownKey()
    {
        var pilot = new LocaleScenario();
        Assert.Throws<System.ArgumentException>(() => pilot.Fields.Set("nope", "value"));
        Assert.Throws<System.ArgumentException>(() => pilot.Fields.Get("nope"));
    }

    [Test]
    public void EveryPilotInitialisesWithTheSharedSsoMode()
    {
        // `connection` needs SSO and the other two do not care: a common mode is what makes the
        // screens safe to visit in any order (ConnectUser on an Octopus-auth SDK is a native
        // precondition failure on iOS).
        Assert.AreEqual("SSO", OctopusScenarioSdk.PilotModeLabel);
        Assert.IsFalse(OctopusScenarioSdk.IsInPilotMode,
            "No pilot may initialise the SDK before a preset is tapped.");
    }

    [Test]
    public void SdkOperationsAreSerialisedProcessWide()
    {
        OctopusScenarioSdk.ResetOperationSlot();
        string busy;
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("ConnectUser", out busy));
        Assert.IsNull(busy);
        Assert.IsFalse(OctopusScenarioSdk.TryBeginOperation("DisconnectUser", out busy));
        StringAssert.Contains("ConnectUser", busy);
        StringAssert.Contains("DisconnectUser", busy);
        OctopusScenarioSdk.EndOperation(OctopusScenarioSdk.OperationToken);
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("DisconnectUser", out busy));
        OctopusScenarioSdk.ResetOperationSlot();
    }

    [Test]
    public void SystemLanguageMapsToAnIsoCodeWithHonestFallbacks()
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        Assert.AreEqual("fr", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.French, invariant));
        Assert.AreEqual("de", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.German, invariant));
        Assert.AreEqual("zh", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.ChineseSimplified, invariant));
        // Unity's enum spells a `Hugarian` alias with the same value: a name-based lookup misses it.
        Assert.AreEqual("hu", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.Hungarian, invariant));
        Assert.AreEqual("hu", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.Hungarian,
            new System.Globalization.CultureInfo("fr-FR")));
        Assert.AreEqual("sr", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.SerboCroatian, invariant));
        // Unknown OS language: the thread culture is the fallback, the invariant culture is not a language.
        Assert.AreEqual("es", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.Unknown,
            new System.Globalization.CultureInfo("es-ES")));
        Assert.AreEqual("en", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.Unknown, invariant));
        Assert.AreEqual("en", LocaleScenario.LanguageCodeFor(UnityEngine.SystemLanguage.Unknown, null));
    }

    private static void AssertPresetIds(OctopusScenarioPilot pilot, string[] expected)
    {
        var actual = new List<string>();
        foreach (var preset in pilot.Presets) actual.Add(preset.TestId);
        CollectionAssert.AreEqual(expected, actual,
            "Scenario '" + pilot.Id + "' preset test ids drifted from the catalogue.");
    }

    private static List<OctopusScenarioPilot> AllPilots()
    {
        var pilots = new List<OctopusScenarioPilot>();
        foreach (var id in OctopusScenarioPilots.Ids) pilots.Add(OctopusScenarioPilots.Create(id));
        return pilots;
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> Methods = new List<string>();

        public void LogApiCall(string method, string detail = null)
        {
            Methods.Add(method);
        }

        public readonly List<(string headline, string detail)> StateChanges =
            new List<(string, string)>();

        public void LogStateChange(string headline, string detail = null)
        {
            StateChanges.Add((headline, detail));
        }
    }
}
