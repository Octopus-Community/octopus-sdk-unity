using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class OctopusInitialScreenScenarioTests
{
    private OctopusRecordingScenarioSdk _sdk;
    private InitialScreenScenario _pilot;

    [SetUp]
    public void SetUp()
    {
        _sdk = new OctopusRecordingScenarioSdk
        { Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key", userId = "fixture-user" } };
        OctopusScenarioSdk.Use(_sdk);
        _pilot = new InitialScreenScenario();
    }

    [TearDown]
    public void TearDown()
    {
        _pilot.Dispose();
        OctopusScenarioSdk.Use(null);
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    private void Fill(int number) { _pilot.Presets[number - 1].Fill(_pilot.Fields); }
    private void Run() { _pilot.RunCustom(); }

    [Test]
    public void CatalogueContractIncludesAllElevenPresetsAndKeepsTheLegacyScene()
    {
        CollectionAssert.AreEqual(new[] {
            "qa-preset-initialScreen-1", "qa-preset-initialScreen-2", "qa-preset-initialScreen-3",
            "qa-preset-initialScreen-4", "qa-preset-initialScreen-5", "qa-preset-initialScreen-6",
            "qa-preset-initialScreen-7", "qa-preset-initialScreen-8", "qa-preset-initialScreen-9",
            "qa-preset-initialScreen-10", "qa-preset-initialScreen-11"
        }, _pilot.Presets.Select(p => p.TestId));
        CollectionAssert.AreEqual(new[] {
            "Preset 1 · Open main feed", "Preset 2 · Open post (bridge mode)",
            "Preset 3 · Open group (bridge mode)", "Preset 4 · Open createPost (prefilled)",
            "Preset 5 · Open standalone OctopusPostDetailsScreen", "Preset 6 · Standalone editor + signed image share",
            "Preset 7 · Open via showOctopusHomeScreen helper (auto inset)",
            "Preset 8 · Open member activity (by clientUserId)", "Preset 9 · Open member activity (by profileId)",
            "Preset 10 · Open member profile (OctopusProfileScreen)", "Preset 11 · Open my own profile (no id)"
        }, _pilot.Presets.Select(p => p.Label));
        Assert.AreEqual("initialScreen-result", _pilot.ResultTestId);
        Assert.AreEqual(ScenarioSection.Presentation, OctopusScenarioSections.SectionOf("initialScreen"));
        var row = OctopusScenarioCatalog.All.Single(s => s.Id == "initialScreen");
        Assert.AreEqual("OpenScreenExample", row.DemoScene);
        Assert.IsTrue(OctopusScenarioSections.IsListed(row));
        Assert.IsInstanceOf<InitialScreenScenario>(OctopusScenarioPilots.Create("initialScreen"));
        Assert.AreEqual(23, OctopusScenarioSections.Filter("").Sum(s => s.Scenarios.Count));
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void EveryPresetFillsOnlyTheRemainingCustomizeInputs()
    {
        CollectionAssert.AreEqual(new[]
        {
            "preset", "postId", "groupId", "text", "ctaLabel", "ctaUrl", "clientUserId"
        }, _pilot.Fields.All.Select(field => field.Key));
        foreach (var preset in _pilot.Presets)
        {
            foreach (var field in _pilot.Fields.All) field.Value = "";
            Assert.DoesNotThrow(() => preset.Fill(_pilot.Fields), preset.TestId);
            Assert.IsTrue(_pilot.Fields.All.All(field => !string.IsNullOrEmpty(field.Value)), preset.TestId);
        }
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void CustomizeLabelsIdentifyThePresetsThatReadTheirInputs()
    {
        StringAssert.Contains("presets 3, 4 & 6", _pilot.Fields.All.Single(f => f.Key == "groupId").Label);
        StringAssert.Contains("preset 10 only", _pilot.Fields.All.Single(f => f.Key == "clientUserId").Label);
    }

    [TestCase(5, 2, "OpenPost")]
    [TestCase(7, 1, "Open")]
    public void EquivalentPresetsReportTheirSharedUnityEntryPoint(int number, int equivalent, string method)
    {
        Fill(equivalent);
        _pilot.Fields.Set("postId", "fixture-post");
        Run();
        Assert.AreEqual(method, _sdk.Last.Method);
        var arguments = _sdk.Last.Args.ToArray();
        _sdk.Clear();

        Fill(number);
        _pilot.Fields.Set("postId", "fixture-post");
        Run();
        Assert.AreEqual(method, _sdk.Last.Method);
        CollectionAssert.AreEqual(arguments, _sdk.Last.Args);
        StringAssert.Contains("same entry point as preset " + equivalent + " in Unity", _pilot.Result);
        StringAssert.Contains("OctopusSDK." + method, _pilot.Result);
    }

    [TestCase(4)]
    [TestCase(6)]
    public void MissingGroupedEditorPrefillReportsWithoutCallingTheSdk(int number)
    {
        // Current presets always construct a prefill. Exercise the helper's defensive boundary
        // directly so a future caller cannot turn a missing prefill into an unhandled exception.
        var openGrouped = typeof(InitialScreenScenario).GetMethod("OpenGrouped",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(openGrouped);
        Assert.DoesNotThrow(() => openGrouped.Invoke(_pilot, new object[] { _sdk, number, "chosen", null }));
        Assert.IsEmpty(_sdk.Calls);
        Assert.AreEqual("No call made: post editor prefill is missing.", _pilot.Result);
        Assert.IsFalse(_pilot.IsRunning);
    }

    [TestCase(1)]
    [TestCase(7)]
    public void FeedPresetsUseTheNativeHomeEntryPoint(int number)
    {
        Fill(number);
        Assert.IsEmpty(_sdk.Calls);
        Run();
        CollectionAssert.AreEqual(new[] { "Initialize", "Open" }, _sdk.ScenarioMethods);
        StringAssert.StartsWith("Opening", _pilot.Result);
    }

    [TestCase(2)]
    [TestCase(5)]
    public void PostPresetsValidateAndForwardTheCustomId(int number)
    {
        Fill(number);
        Run();
        StringAssert.Contains("Post id is empty", _pilot.Result);
        Assert.IsFalse(_sdk.Methods.Contains("OpenPost"));
        _pilot.Fields.Set("postId", " fixture-post ");
        Run();
        Assert.AreEqual("OpenPost", _sdk.Last.Method);
        Assert.AreEqual("fixture-post", _sdk.Last.Args[0]);
    }

    [Test]
    public void GroupSelectionPrefersGeneralAndHonoursExplicitIds()
    {
        _sdk.Groups.Add(new OctopusGroup { Id = "first", Name = "First" });
        _sdk.Groups.Add(new OctopusGroup { Id = "general", Name = "General" });
        Fill(3);
        Run();
        Assert.AreEqual("OpenGroup", _sdk.Last.Method);
        Assert.AreEqual("general", _sdk.Last.Args[0]);
        _sdk.Clear();
        _pilot.Fields.Set("groupId", "custom");
        Run();
        Assert.AreEqual("custom", _sdk.Last.Args[0]);
        Assert.IsFalse(_sdk.Methods.Contains("FetchGroups"));
    }

    [Test]
    public void EmptyGroupsAndFetchFailuresNeverOpenTheFeedInstead()
    {
        Fill(3);
        Run();
        StringAssert.Contains("No group available", _pilot.Result);
        _sdk.GroupFetchError = "offline";
        Run();
        StringAssert.Contains("FetchGroups failed: offline", _pilot.Result);
        Assert.IsFalse(_sdk.Methods.Any(m => m.StartsWith("Open")));
        Assert.IsFalse(_pilot.IsRunning);
    }

    [TestCase(4, false)]
    [TestCase(6, true)]
    public void PrefilledEditorForwardsTextCtaGroupAndOptionalSignedImage(int number, bool image)
    {
        Fill(number);
        _pilot.Fields.Set("groupId", "chosen");
        _pilot.Fields.Set("ctaLabel", "Open");
        _pilot.Fields.Set("ctaUrl", "https://example.org/post");
        Run();
        Assert.AreEqual("OpenCreatePost", _sdk.Last.Method);
        var prefill = _sdk.LastPrefilledPost;
        Assert.AreEqual(InitialScreenScenario.DefaultText, prefill.Text);
        Assert.AreEqual("chosen", prefill.TopicId);
        Assert.AreEqual("Open", prefill.CtaLabel);
        Assert.AreEqual("https://example.org/post", prefill.CtaUrl);
        Assert.AreEqual(image ? "/sample-cache/scenario-share.png" : null, prefill.ImagePath);
        Assert.AreEqual(image, prefill.SignBridgeShare != null);
        Assert.IsFalse(_sdk.Methods.Contains("SignBridgeShare"), "Opening must never sign or publish.");
    }

    [Test]
    public void DefaultEditorAllowsNoGroupAndNoCta()
    {
        Fill(4);
        Run();
        Assert.IsNull(_sdk.LastPrefilledPost.TopicId);
        Assert.IsNull(_sdk.LastPrefilledPost.CtaLabel);
        Assert.IsNull(_sdk.LastPrefilledPost.CtaUrl);
    }

    [TestCase("text", "short")]
    [TestCase("ctaLabel", "Only label")]
    [TestCase("ctaUrl", "invalid")]
    public void InvalidPrefillDoesNotOpenTheEditor(string field, string value)
    {
        Fill(4);
        _pilot.Fields.Set(field, value);
        Run();
        StringAssert.Contains("rejected", _pilot.Result);
        Assert.IsFalse(_sdk.Methods.Contains("OpenCreatePost"));
    }

    [Test]
    public void MissingImageReportsFailureWithoutOpening()
    {
        _sdk.ThrowShareImage = true;
        Fill(6);
        Run();
        StringAssert.Contains("Bundled image unavailable", _pilot.Result);
        Assert.IsFalse(_sdk.Methods.Contains("OpenCreatePost"));
    }

    [TestCase(8, "clientUserId")]
    [TestCase(9, "profileId")]
    public void MissingMemberActivityApiReportsItsLimitWithoutInitialization(int number, string kind)
    {
        _sdk.Profile = null;
        Fill(number);
        Run();
        Assert.IsEmpty(_sdk.Calls);
        StringAssert.StartsWith("Unavailable:", _pilot.Result);
        StringAssert.Contains(kind, _pilot.Result);
        Assert.IsFalse(_pilot.IsRunning);
    }

    [Test]
    public void ProfilesUseCustomOrConfiguredClientIdsAndNullForOwnProfile()
    {
        Fill(10);
        Run();
        Assert.AreEqual("OpenProfile", _sdk.Last.Method);
        Assert.AreEqual("fixture-user", _sdk.Last.Args[0]);
        _pilot.Fields.Set("clientUserId", " other-member ");
        Run();
        Assert.AreEqual("other-member", _sdk.Last.Args[0]);
        Fill(11);
        Run();
        Assert.AreEqual("OpenProfile", _sdk.Last.Method);
        Assert.IsNull(_sdk.Last.Args[0]);
    }

    [Test]
    public void MissingMemberAndConfigurationReportWithoutOpening()
    {
        _sdk.Profile.userId = "";
        Fill(10);
        Run();
        StringAssert.Contains("No member clientUserId", _pilot.Result);
        Assert.IsFalse(_sdk.Methods.Contains("OpenProfile"));
        _sdk.Profile = null;
        _sdk.Clear();
        Fill(1);
        Run();
        Assert.IsEmpty(_sdk.Calls);
        Assert.IsFalse(_pilot.IsRunning);
    }

    [Test]
    public void DisposedPilotDoesNotNavigateAfterDeferredGroupFetch()
    {
        _sdk.DeferGroups = true;
        Fill(3);
        Run();
        Assert.IsTrue(_pilot.IsRunning);
        _pilot.Dispose();
        _sdk.PendingGroups(new[] { new OctopusGroup { Id = "late" } });
        Assert.IsFalse(_sdk.Methods.Contains("OpenGroup"));
        Assert.IsFalse(_pilot.IsRunning);
    }
}
