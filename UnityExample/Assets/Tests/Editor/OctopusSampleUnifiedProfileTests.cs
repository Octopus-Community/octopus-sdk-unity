using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Unified Profile override (`exposeClientUserId`): the Scenarios › Sign-in &amp; user
/// tri-state — Android's placement, labels and test ids — reaches the SDK
/// facade when it should and only then, the effective value is read back, the `communityData`
/// screen names the flag with that value, and a routed avatar tap opens the host profile page.
/// SDK calls use the recorder, never a network.
/// </summary>
public class OctopusSampleUnifiedProfileTests
{
    private const string Override = "DebugOverrideExposeClientUserId";
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private OctopusRecordingScenarioSdk _sdk;
    private string _key;

    [SetUp]
    public void SetUp()
    {
        _key = "OctopusSample.Tests.UnifiedProfile." + Guid.NewGuid().ToString("N");
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleFeatureToggles.Reset();
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile
            {
                apiKey = "test-key", authToken = "not-a-real-token", userId = "client-user-1"
            },
            AlternateProfile = new OctopusExampleConfig.ExampleProfile
            {
                apiKey = "test-alternate-key", authToken = "not-a-real-token", userId = "client-user-1"
            }
        };
        OctopusScenarioSdk.Use(_sdk, _key);
    }

    [TearDown]
    public void TearDown()
    {
        var page = UnityEngine.Object.FindAnyObjectByType<OctopusSampleClientProfileView>();
        if (page != null) UnityEngine.Object.DestroyImmediate(page.gameObject);
        var config = UnityEngine.Object.FindAnyObjectByType<OctopusSampleConfigView>();
        if (config != null) UnityEngine.Object.DestroyImmediate(config.gameObject);
        foreach (var host in _spawned) if (host != null) UnityEngine.Object.DestroyImmediate(host);
        _spawned.Clear();
        PlayerPrefs.DeleteKey(_key);
        PlayerPrefs.Save();
        OctopusScenarioSdk.Use(null);
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [Test]
    public void ADefaultStartMakesNoOverrideCallButInstallsTheProfileRoute()
    {
        Initialize();

        CollectionAssert.DoesNotContain(_sdk.Methods, Override);
        CollectionAssert.DoesNotContain(_sdk.Methods, "DebugGetCommunityConfig");
        Assert.IsNotNull(_sdk.NavigateToProfileHandler, "Avatar taps must be routable from the first community open.");
        Assert.IsNull(OctopusSampleUnifiedProfile.Override);
    }

    [Test]
    public void AChoiceMadeBeforeStartIsKeptAndAppliedRightAfterInitialize()
    {
        OctopusSampleUnifiedProfile.SetOverride(true);
        Assert.IsEmpty(_sdk.Methods, "No SDK call before the SDK exists.");
        StringAssert.Contains("Force active once the SDK starts", OctopusSampleUnifiedProfile.EffectiveText);

        Initialize();

        var methods = _sdk.Methods;
        Assert.Less(methods.IndexOf("Initialize"), methods.IndexOf(Override), "The override targets the running SDK.");
        Assert.AreEqual(true, LastOverride());
    }

    [TestCase(0, null)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    public void EachChoiceReachesTheRunningSdkAndReadsTheEffectiveValueBack(int choice, bool? expected)
    {
        Initialize();
        _sdk.DeferCompletions = true;
        var values = new bool?[] { null, true, false };

        OctopusSampleUnifiedProfile.SetOverride(values[choice]);

        Assert.AreEqual(1, Count(Override));
        Assert.AreEqual(expected, LastOverride());
        Assert.AreEqual(1, Count("DebugGetCommunityConfig"));
        Assert.AreEqual("reading…", OctopusSampleUnifiedProfile.EffectiveText);
        _sdk.ConfigResult(Config(expected ?? false));
        Assert.AreEqual(expected ?? false, OctopusSampleUnifiedProfile.Effective);
        Assert.AreEqual(((expected ?? false) ? "On" : "Off") + (expected.HasValue ? " (forced)" : " (backend)"),
            OctopusSampleUnifiedProfile.EffectiveText);
    }

    [Test]
    public void AStaleReadDoesNotOverwriteTheLatestOne()
    {
        Initialize();
        _sdk.DeferCompletions = true;
        OctopusSampleUnifiedProfile.SetOverride(true);
        var first = _sdk.ConfigResult;
        OctopusSampleUnifiedProfile.SetOverride(false);
        _sdk.ConfigResult(Config(false));

        first(Config(true));

        Assert.AreEqual(false, OctopusSampleUnifiedProfile.Effective);
    }

    [Test]
    public void AReadErrorIsShownInsteadOfAValue()
    {
        Initialize();
        _sdk.DeferCompletions = true;
        OctopusSampleUnifiedProfile.RefreshEffective();

        _sdk.ConfigError("not initialized");

        Assert.IsNull(OctopusSampleUnifiedProfile.Effective);
        StringAssert.Contains("unavailable (not initialized)", OctopusSampleUnifiedProfile.EffectiveText);
    }

    [Test]
    public void ACommunitySwitchReappliesTheForcedValue()
    {
        Initialize();
        OctopusSampleUnifiedProfile.SetOverride(false);
        Assert.AreEqual(1, Count(Override));

        OctopusScenarioSdk.CommunitySwitched(_sdk.AlternateProfile, 2);

        Assert.AreEqual(2, Count(Override));
        Assert.AreEqual(false, LastOverride());
    }

    [Test]
    public void ASwitchUnderAnOpenSectionReadsTheEffectiveValueAgain()
    {
        Initialize();
        var list = OpenScenarios();
        OpenUnifiedDetail();
        var reads = Count("DebugGetCommunityConfig");
        _sdk.DeferCompletions = true;

        OctopusScenarioSdk.CommunitySwitched(_sdk.AlternateProfile, 2);

        Assert.AreEqual(reads + 1, Count("DebugGetCommunityConfig"),
            "A switch left the Unified Profile section on \"reading…\" with no read in flight (#393).");
        _sdk.ConfigResult(null);
        StringAssert.DoesNotContain("reading…", OctopusSampleUnifiedProfile.EffectiveText);
    }

    [Test]
    public void ASwitchWithNobodyWatchingMakesNoRead()
    {
        Initialize();
        var reads = Count("DebugGetCommunityConfig");
        OctopusScenarioSdk.CommunitySwitched(_sdk.AlternateProfile, 2);
        Assert.AreEqual(reads, Count("DebugGetCommunityConfig"));
    }

    [Test]
    public void AStaleSynchronousFailureDoesNotOverwriteANewerRead()
    {
        Initialize();
        _sdk.DeferCompletions = true;
        _sdk.ThrowOnConfigRead = true;
        var first = true;
        _sdk.OnConfigRead = () =>
        {
            if (!first) return;
            first = false;
            // A newer read starts while the first one is still inside the SDK call.
            _sdk.ThrowOnConfigRead = false;
            OctopusSampleUnifiedProfile.RefreshEffective();
        };

        OctopusSampleUnifiedProfile.RefreshEffective();

        Assert.AreEqual("reading…", OctopusSampleUnifiedProfile.EffectiveText,
            "The first read's synchronous failure overwrote the newer read in flight (#393).");
    }

    [Test]
    public void TheRouteSwitchUnwiresAndRewiresTheHostProfileRoute()
    {
        Initialize();
        var view = OpenUnifiedDetail();
        Assert.IsNotNull(_sdk.NavigateToProfileHandler);
        // Effective flag starts off. Turning the scenario on wires and forces the flag.
        Tap(view.transform, "config-unified-profile");
        Assert.IsTrue(OctopusSampleUnifiedProfile.RouteWired);
        Assert.AreEqual(true, OctopusSampleUnifiedProfile.Override);
        _sdk.ConfigResult(Config(true));
        Tap(view.transform, "config-unified-profile");
        Assert.IsFalse(OctopusSampleUnifiedProfile.RouteWired);
        Assert.IsNull(_sdk.NavigateToProfileHandler);
        Assert.IsNull(OctopusSampleUnifiedProfile.Override);
    }

    [Test]
    public void AnUnwiredRouteStaysUnwiredAcrossTheNextStart()
    {
        OctopusSampleUnifiedProfile.SetRouteWired(false);
        Initialize();
        Assert.IsNull(_sdk.NavigateToProfileHandler);
    }

    [Test]
    public void ARoutedAvatarTapOpensTheHostProfilePageWithItsData()
    {
        Initialize();
        _sdk.CommunityDataResult = new OctopusCommunityData("profile-7");

        _sdk.NavigateToProfileHandler("member-7");

        var page = UnityEngine.Object.FindAnyObjectByType<OctopusSampleClientProfileView>();
        Assert.IsNotNull(page, "A routed tap must present the host profile page.");
        Assert.AreEqual("member-7", page.Profile.ClientUserId);
        Assert.IsNotNull(Find(page.transform, "clientProfile-data"));
        Assert.AreEqual(1, Count("FetchCommunityData"));
    }

    [Test]
    public void TheSectionSitsCollapsedAfterTheSignInCardsAndNoLongerInConfig()
    {
        var list = OpenScenarios();
        Assert.IsNull(Find(list.transform, "scenarios-unifiedProfile-card"));
        list.ToggleSection(ScenarioSection.SignIn);
        Tap(list.transform, "scenarios-unifiedProfile-card");
        var view = UnityEngine.Object.FindAnyObjectByType<OctopusScenarioScreenView>();
        _spawned.Add(view.gameObject);
        Assert.AreEqual("unifiedProfile", view.ScenarioId);
        Assert.IsNotNull(Find(view.transform, "scenario-title"));
        Assert.IsNotNull(Find(view.transform, "config-unified-profile"));
    }

    [Test]
    public void ExpandingOffersTheAndroidChoicesAndAChoiceAppliesLive()
    {
        var view = OpenUnifiedDetail();
        Assert.IsFalse(Find(view.transform, OctopusScenariosListView.UnifiedProfileForceOnId).gameObject.activeInHierarchy);
        Tap(view.transform, "unified-profile-backend-toggle");
        StringAssert.Contains("Use backend value", AllText(view.transform));
        StringAssert.Contains("Force active", AllText(view.transform));
        Initialize();
        _sdk.DeferCompletions = true;
        Tap(view.transform, OctopusScenariosListView.UnifiedProfileForceOnId);
        Assert.AreEqual(true, LastOverride());
        _sdk.ConfigResult(Config(true));
        StringAssert.Contains("Client profile", AllText(view.transform));
        Tap(view.transform, OctopusScenariosListView.UnifiedProfileForceOffId);
        _sdk.ConfigResult(Config(false));
        StringAssert.Contains("native profiles", AllText(view.transform));
    }

    [Test]
    public void ASearchHidesTheSectionAndATabSwitchKeepsItOpen()
    {
        var list = OpenScenarios();
        list.SetQuery("profile");
        Assert.IsNotNull(Find(list.transform, "scenarios-unifiedProfile-card"));
        Assert.IsNull(Find(list.transform, OctopusScenariosListView.UnifiedProfileForceOnId));
        var view = OpenUnifiedDetail();
        Assert.IsNotNull(Find(view.transform, "config-unified-profile"));
        Tap(view.transform, "Back");
        Assert.IsTrue(view == null);
        Assert.IsNotNull(Find(list.transform, "scenarios-unifiedProfile-card"));
    }

    [Test]
    public void TheCommunityDataScreenNamesTheFlagWithItsEffectiveValue()
    {
        var view = OctopusScenarioScreenView.Open(new CommunityDataScenario());
        _spawned.Add(view.gameObject);
        var hint = Find(view.transform, OctopusScenarioScreenView.UnifiedProfileHintId);
        Assert.IsNotNull(hint);
        var text = hint.GetComponentInChildren<TMP_Text>();
        StringAssert.Contains("exposeClientUserId", text.text);
        StringAssert.Contains("unknown until the SDK starts", text.text);

        Initialize();
        _sdk.DeferCompletions = true;
        OctopusSampleUnifiedProfile.SetOverride(true);
        _sdk.ConfigResult(Config(true));

        StringAssert.Contains("effective now: On (forced)", text.text);
    }

    [Test]
    public void OtherScenarioScreensCarryNoHint()
    {
        var view = OctopusScenarioScreenView.Open(new TermsAcceptanceScenario());
        _spawned.Add(view.gameObject);

        Assert.IsNull(Find(view.transform, OctopusScenarioScreenView.UnifiedProfileHintId));
    }

    private OctopusScenarioScreenView OpenUnifiedDetail()
    {
        var view = OctopusScenarioScreenView.Open(new UnifiedProfileScenario());
        _spawned.Add(view.gameObject);
        return view;
    }

    private OctopusScenariosListView OpenScenarios()
    {
        var shell = CreateShell();
        shell.Select(OctopusSampleTab.Scenarios);
        return shell.GetComponentInChildren<OctopusScenariosListView>();
    }

    private OctopusSampleShell CreateShell()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        return shell;
    }

    private static string LiveValue(Component list)
    {
        var line = Find(list.transform, OctopusScenariosListView.UnifiedProfileLiveValueId);
        Assert.IsNotNull(line, "Missing the Unified Profile read-out line.");
        return AllText(line);
    }

    private static void Initialize()
    {
        string reason;
        Assert.IsNotNull(OctopusScenarioSdk.EnsurePilotInitialized(out reason), reason);
    }

    private object LastOverride()
    {
        object last = "none";
        foreach (var call in _sdk.Calls) if (call.Method == Override) last = call.Args[0];
        return last;
    }

    private int Count(string method)
    {
        var count = 0;
        foreach (var name in _sdk.Methods) if (name == method) count++;
        return count;
    }

    private static OctopusCommunityConfig Config(bool exposeClientUserId)
    {
        var config = (OctopusCommunityConfig)Activator.CreateInstance(typeof(OctopusCommunityConfig), true);
        typeof(OctopusCommunityConfig).GetProperty("ExposeClientUserId").SetValue(config, exposeClientUserId);
        return config;
    }

    private static string Label(Component view, string id)
    {
        var node = Find(view.transform, id);
        Assert.IsNotNull(node, "Missing control " + id);
        return node.GetComponentInChildren<TMP_Text>().text;
    }

    private static void Tap(Transform root, string id)
    {
        var node = Find(root, id);
        Assert.IsNotNull(node, "Missing control " + id);
        node.GetComponent<Button>().onClick.Invoke();
    }

    private static string AllText(Transform root)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var label in root.GetComponentsInChildren<TMP_Text>()) builder.AppendLine(label.text);
        return builder.ToString();
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
}
