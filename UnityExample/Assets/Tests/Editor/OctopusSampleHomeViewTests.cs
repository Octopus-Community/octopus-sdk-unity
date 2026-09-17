using TMPro;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Home dashboard's three load-bearing claims, each of which is a bug the moment it stops
/// holding: it is addressable, it is read-only, and it never states a session it cannot observe.
///
/// Same constraint as <see cref="OctopusSampleShellTests"/> — the test assembly overrides its
/// precompiled references down to `nunit.framework.dll`, so no `UnityEngine.UI` type can be named
/// here. Text on screen is therefore asserted through the static label functions
/// <see cref="OctopusSampleHomeView.SdkStatusLabel"/> and
/// <see cref="OctopusSampleHomeView.ConnectionStatusLabel"/>, which are the exact strings
/// <c>Refresh</c> paints, and everything else through <c>GameObject.name</c> and object identity.
/// </summary>
public class OctopusSampleHomeViewTests
{
    private const string SecretApiKey = "sentinel-api-key-9d41f0";
    private const string SecretAuthToken = "sentinel-auth-token-6b27ac";

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private OctopusRecordingScenarioSdk _sdk;

    [SetUp]
    public void SetUp()
    {
        // Static sample state, so a test that reports a session would otherwise decide what the
        // next one sees.
        OctopusSampleState.Reset();

        // The recording seam stands in for the gitignored config asset, which is absent here and on
        // a fresh clone. Its profile carries deliberately recognisable secrets so a test can assert
        // they are nowhere on a screen that is mirrored publicly and screenshotted by QA.
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile
            {
                apiKey = SecretApiKey,
                authToken = SecretAuthToken,
                userId = "sample-user",
                nickname = "Sample User",
            },
        };
        OctopusScenarioSdk.Use(_sdk);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusScenarioSdk.Use(null);
        OctopusSampleState.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void TheHomeTabCarriesEveryDashboardId()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Home);

        foreach (var id in new[]
                 {
                     OctopusSampleHomeView.PlatformChipId,
                     OctopusSampleHomeView.SdkStatusCardId,
                     OctopusSampleHomeView.SdkStatusId,
                     OctopusSampleHomeView.ConfigurationBlockId,
                     OctopusSampleHomeView.ConnectionCardId,
                     OctopusSampleHomeView.ConnectionStatusId,
                     OctopusSampleHomeView.CommunityAccessCardId,
                     OctopusSampleHomeView.OpenCommunityId,
                 })
        {
            Assert.IsNotNull(Find(shell.transform, id),
                "The Home tab has no GameObject named '" + id + "'. Unity renders into one opaque " +
                "surface with no accessibility tree, so a name that moves takes the QA script with " +
                "it.");
        }
    }

    [Test]
    public void OpeningHomeAsksTheSdkForNothing()
    {
        // The whole reason the dashboard reads OctopusSDK's cached values and its own sample state
        // instead of calling UpdateNotSeenNotificationsCount(): §5.1 makes Home read-only, and a
        // tab switch that mutates anything makes every later QA step ambiguous.
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        shell.Select(OctopusSampleTab.Home);

        // Both halves matter. The seam is the load-bearing one — it is the object the pilots call
        // into, so an empty recording means Home reached no SDK entry point at all. The log probe
        // catches the other direction: a call the sample announced but routed around the seam.
        Assert.IsEmpty(_sdk.ScenarioMethods,
            "Opening Home reached the SDK: " + string.Join(", ", _sdk.ScenarioMethods));
        Assert.IsEmpty(probe.Methods,
            "Opening Home logged an SDK call: " + string.Join(", ", probe.Methods));
    }

    [Test]
    public void AnUntouchedSessionReadsNotInitializedAndNoCall()
    {
        Assert.AreEqual("Not initialized", OctopusSampleHomeView.SdkStatusLabel());
        Assert.AreEqual("NO CALL", OctopusSampleHomeView.ConnectionStatusLabel(),
            "Nothing was asked of the SDK, which is a different answer from the SDK refusing.");
    }

    [Test]
    public void EveryConnectionLabelNamesTheCallRatherThanTheSession()
    {
        // The rule this pins: no label here may assert a session state. This package publishes no
        // connection state, and the two ways an assertion would be wrong are both live — the iOS
        // bridge reports a connect complete even when authentication failed, and a disconnect that
        // throws leaves the earlier session running.
        OctopusSampleState.ReportInitialized("SSO");
        Assert.AreEqual("READY", OctopusSampleHomeView.SdkStatusLabel());
        Assert.AreEqual("NO CALL", OctopusSampleHomeView.ConnectionStatusLabel(),
            "Initializing is not connecting.");

        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "done");
        Assert.AreEqual("CONNECT OK", OctopusSampleHomeView.ConnectionStatusLabel());

        OctopusSampleState.ReportSession(OctopusSampleState.Session.Disconnected, "done");
        Assert.AreEqual("DISCONNECT OK", OctopusSampleHomeView.ConnectionStatusLabel());

        foreach (var session in new[]
                 {
                     OctopusSampleState.Session.None,
                     OctopusSampleState.Session.ConnectCompleted,
                     OctopusSampleState.Session.Disconnected,
                     OctopusSampleState.Session.Failed,
                 })
        {
            OctopusSampleState.ReportSession(session, "detail");
            var label = OctopusSampleHomeView.ConnectionStatusLabel();
            Assert.AreNotEqual("CONNECTED", label,
                "The dashboard cannot observe a session, so no state of it may read CONNECTED.");
            Assert.AreNotEqual("OFF", label,
                "Nor may any state read OFF: a disconnect that threw left the session up.");
        }
    }

    [Test]
    public void AFailedCallReadsAsFailedWhicheverCallItWas()
    {
        OctopusSampleState.ReportSession(OctopusSampleState.Session.Failed, "boom");

        Assert.AreEqual("CALL FAILED", OctopusSampleHomeView.ConnectionStatusLabel(),
            "A connection call that threw says nothing about the session — only that the call did " +
            "not complete.");
    }

    [Test]
    public void APublishedNotificationCountSurvivesTheDashboardBeingRebuilt()
    {
        // The shell destroys and rebuilds its content on every tab switch and every theme change.
        // The SDK publishes this count when it changes and the sample never asks for it (Home is
        // read-only), so a count held by the view would be gone for the rest of the session — the
        // dashboard would read "not yet told" while the SDK considers the reader informed.
        OctopusSampleState.EnsureObserving();
        OctopusSampleState.ReportUnseenNotifications(7);

        var shell = Create();
        shell.Select(OctopusSampleTab.Home);
        shell.Select(OctopusSampleTab.Scenarios);
        shell.Select(OctopusSampleTab.Home);

        Assert.AreEqual(7, OctopusSampleState.UnseenNotifications,
            "The count was forgotten across a rebuild, and nothing in the sample can ask for it " +
            "again.");
    }

    [Test]
    public void TheDashboardRepaintsWhenTheSampleStateChanges()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Home);
        // Identity, not presence: the id below survives a dashboard that never heard the event,
        // which is precisely the failure worth catching. The marker is rebuilt on every refresh, so
        // a new object is proof the handler ran.
        var before = Find(shell.transform, OctopusSampleHomeView.SdkStatusId);
        Assert.IsNotNull(before);

        OctopusSampleState.ReportInitialized("SSO");

        var after = Find(shell.transform, OctopusSampleHomeView.SdkStatusId);
        Assert.IsNotNull(after, "The refresh left no status marker.");
        Assert.AreNotSame(before, after,
            "The marker is the same object as before the state changed — the dashboard never heard " +
            "OctopusSampleState.Changed, and is still showing Not initialized on an initialized SDK. In edit " +
            "mode Unity calls no OnEnable, so the subscription has to be made from the build path.");
    }

    [Test]
    public void ADestroyedDashboardStopsListening()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Home);
        Object.DestroyImmediate(shell.gameObject);
        _spawned.Clear();

        // Would throw a MissingReferenceException from the dead view's handler if the events were
        // still hooked — a static event outliving its subscriber is how one test poisons the next.
        Assert.DoesNotThrow(() => OctopusSampleState.ReportInitialized("SSO"));
    }

    [Test]
    public void TheConfigurationBlockRendersNoSecretValue()
    {
        // The assertion that actually protects something: SetUp configured a profile whose API key
        // and auth token are recognisable strings, and no rendered value may contain either. This
        // screen is mirrored publicly and screenshotted by QA, so a leak here leaves the repo.
        foreach (var line in OctopusSampleHomeView.ConfigurationLines())
        {
            var rendered = line.Key + " = " + line.Value;
            Assert.That(line.Value, Does.Not.Contain(SecretApiKey),
                "The API key itself reached the screen, on the line '" + rendered + "'. The block " +
                "names the key's *source*, never the key.");
            Assert.That(line.Value, Does.Not.Contain(SecretAuthToken),
                "The auth token reached the screen, on the line '" + rendered + "'. It has no " +
                "business on any screen at all.");
        }
    }

    [Test]
    public void MissingSsoCredentialsReachTheHomeErrorTextAndSampleLog()
    {
        _sdk.Profile.authToken = "";
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        var shell = Create();
        var pilot = new ConnectionScenario();
        var preset = pilot.Presets[0];
        preset.Fill(pilot.Fields);
        pilot.Execute(() => preset.Run(pilot.Fields));

        Assert.IsFalse(pilot.IsRunning);
        StringAssert.Contains("ssoTokenSecret or authToken", pilot.Result);
        Assert.AreEqual(OctopusSampleState.Session.Failed, OctopusSampleState.ConnectionSession);
        Assert.AreEqual(pilot.Result, OctopusSampleState.SessionDetail);
        Assert.AreEqual("CALL FAILED", OctopusSampleHomeView.ConnectionStatusLabel());
        Assert.IsTrue(System.Array.Exists(shell.GetComponentsInChildren<TMP_Text>(true),
            label => label.text.Contains(pilot.Result)), "Home must render the actionable refusal.");
        Assert.IsTrue(probe.StateChanges.Exists(entry =>
            entry.headline == "[OctopusQA] scenario=connection state=refused" && entry.detail == pilot.Result));
        CollectionAssert.DoesNotContain(_sdk.ScenarioMethods, "ConnectUser");
        StringAssert.DoesNotContain(SecretApiKey, pilot.Result);
    }

    [Test]
    public void TheConfigurationBlockLabelsAreExactlyTheExpectedList()
    {
        var labels = new List<string>();
        foreach (var line in OctopusSampleHomeView.ConfigurationLines()) labels.Add(line.Key);
        Assert.AreEqual("Carried in the SSO token; presets set claims when demo signing is configured",
            OctopusSampleHomeView.ConfigurationLines().Find(line => line.Key == "Entitlements").Value);

        Assert.AreEqual(
            new[]
            {
                "Server environment", "Community", "API key source", "SSO user", "Entitlements",
                "Theme", "Language",
            },
            labels,
            "The configuration block's lines changed. The list is asserted whole because the " +
            "dangerous edit is an addition: this screen is mirrored publicly and screenshotted by " +
            "QA, so it names the *source* of the API key and never the key, and never the auth " +
            "token at all.");
    }

    [Test]
    public void TheConfigurationBlockSurvivesAMissingConfigAsset()
    {
        // OctopusExampleConfig is git-ignored, so it is absent here and on a fresh clone. Reading it
        // through Instance would log an error and NUnit fails a test that logs one — which is why
        // the seam reads LoadedOrNull.
        _sdk.Profile = null;

        foreach (var line in OctopusSampleHomeView.ConfigurationLines())
        {
            Assert.IsNotEmpty(line.Value, "Line '" + line.Key + "' rendered empty.");
        }
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void ColdStatusIsNeutralAndCardsKeepTheAcceptedOrder(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var shell = Create();
        var marker = Find(shell.transform, OctopusSampleHomeView.SdkStatusId).Find("Marker");
        Assert.AreEqual(SampleUi.Muted, marker.GetComponent<Image>().color);
        var ids = new[] { OctopusSampleHomeView.SdkStatusCardId,
            OctopusSampleHomeView.ConfigurationBlockId, OctopusSampleHomeView.ConnectionCardId,
            OctopusSampleHomeView.CommunityAccessCardId };
        var previous = -1;
        foreach (var id in ids)
        {
            var card = Find(shell.transform, id);
            Assert.Greater(card.GetSiblingIndex(), previous);
            previous = card.GetSiblingIndex();
        }
    }

    [Test]
    public void ConfigurationDetailsRemainReadableAndCollapseAfterNavigation()
    {
        var shell = Create();
        var card = Find(shell.transform, OctopusSampleHomeView.ConfigurationBlockId);
        Assert.IsFalse(card.Find("Details").gameObject.activeSelf);
        Find(shell.transform, "home-configuration-details").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(card.Find("Details").gameObject.activeSelf);
        foreach (var value in card.Find("Details").GetComponentsInChildren<TMP_Text>())
        {
            Assert.IsFalse(value.enableAutoSizing);
            Assert.AreEqual(TextWrappingModes.Normal, value.textWrappingMode);
        }
        shell.Select(OctopusSampleTab.Scenarios);
        shell.Select(OctopusSampleTab.Home);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        card = Find(shell.transform, OctopusSampleHomeView.ConfigurationBlockId);
        Assert.IsFalse(card.Find("Details").gameObject.activeSelf);
        Assert.IsEmpty(_sdk.ScenarioMethods);
    }

    [TestCase(OctopusSampleHomeView.SdkStatusCardId, "home-sdk-status-details")]
    [TestCase(OctopusSampleHomeView.ConfigurationBlockId, "home-configuration-details")]
    [TestCase(OctopusSampleHomeView.ConnectionCardId, "home-connection-details")]
    [TestCase(OctopusSampleHomeView.CommunityAccessCardId, "home-community-access-details")]
    public void EveryCardStartsCollapsedAndItsDisclosureIsLocalToTheVisit(string cardId, string toggleId)
    {
        var shell = Create();
        var details = Find(shell.transform, cardId).Find("Details");
        Assert.IsFalse(details.gameObject.activeSelf);
        var toggle = Find(shell.transform, toggleId).GetComponent<Button>();
        Assert.AreEqual("Show details", toggle.GetComponentInChildren<TMP_Text>().text);
        toggle.onClick.Invoke();
        Assert.IsTrue(details.gameObject.activeSelf);
        Assert.AreEqual("Hide details", toggle.GetComponentInChildren<TMP_Text>().text);
        OctopusSampleState.ReportUnseenNotifications(12);
        Assert.IsTrue(details.gameObject.activeSelf, "A state update must not collapse a card being read.");
        toggle.onClick.Invoke();
        Assert.IsFalse(details.gameObject.activeSelf);
        Assert.AreEqual("Show details", toggle.GetComponentInChildren<TMP_Text>().text);
        toggle.onClick.Invoke();
        shell.Select(OctopusSampleTab.Settings);
        shell.Select(OctopusSampleTab.Home);
        Assert.IsFalse(Find(shell.transform, cardId).Find("Details").gameObject.activeSelf);
        Assert.IsEmpty(_sdk.ScenarioMethods);
    }

    [Test]
    public void SdkCardShowsThePackageVersionAndCapturedNativeBuildPins()
    {
        var shell = Create();
        var card = Find(shell.transform, OctopusSampleHomeView.SdkStatusCardId);
        Assert.AreEqual("SDK " + OctopusSDK.Version, Find(card, "home-sdk-version").GetComponent<TMP_Text>().text);
        Assert.AreEqual("Native build pins · " + OctopusSampleNativePins.ReadSources(),
            Find(card, "home-sdk-native-pins").GetComponent<TMP_Text>().text);
        Assert.IsTrue(Find(card, "home-sdk-version").gameObject.activeInHierarchy);
        Assert.IsFalse(Find(card, "home-sdk-native-pins").gameObject.activeInHierarchy);
    }

    [Test]
    public void NativeBuildPinsAreWrittenToTheGeneratedResourceBeforeABuild()
    {
        // The pre-build callback writes a Resources text asset, so an incremental build that
        // reuses every scene still ships the current pins. A second write leaves the asset alone.
        var summary = OctopusSampleNativePins.Write();
        var path = System.IO.Path.Combine(Application.dataPath, "..", OctopusSampleNativePins.AssetPath);
        var written = System.IO.File.GetLastWriteTimeUtc(path);
        Assert.AreEqual(OctopusSampleNativePins.ReadSources(), summary);
        Assert.AreEqual(summary, System.IO.File.ReadAllText(path));
        Assert.AreEqual(summary, OctopusSampleNativePins.Write());
        Assert.AreEqual(written, System.IO.File.GetLastWriteTimeUtc(path), "an unchanged asset is not rewritten");
        var asset = Resources.Load<TextAsset>(OctopusSampleNativePins.ResourceName);
        Assert.IsNotNull(asset, "the generated asset must be loadable from Resources");
        Assert.AreEqual(summary, asset.text);
    }

    private OctopusSampleShell Create()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        return shell;
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

        public readonly List<(string headline, string detail)> StateChanges =
            new List<(string, string)>();

        public void LogStateChange(string headline, string detail = null)
        {
            StateChanges.Add((headline, detail));
        }
    }
}
