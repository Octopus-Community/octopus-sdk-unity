using TMPro;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Community tab's contract: four doors into the SDK's own screen, each one initialising the
/// SDK first, none of them fired by arriving on the tab.
///
/// Assertions go through the recording seam and through <c>GameObject.name</c>, because the
/// EditMode assembly overrides its references down to `nunit.framework.dll` and can name no
/// `UnityEngine.UI` type.
/// </summary>
public class OctopusSampleCommunityViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private OctopusRecordingScenarioSdk _sdk;

    [SetUp]
    public void SetUp()
    {
        OctopusSampleState.Reset();
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key" }
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
        OctopusSampleState.Reset();
        OctopusScenarioSdk.Use(null);
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void ReadOnlyBandUsesNeutralSurfaceAndUnbuiltDisclosuresAreSafe()
    {
        var view = View();
        var band = Find(view.transform, OctopusSampleCommunityView.ReadOnlyBandId);
        Assert.AreEqual(OctopusSampleBranding.Palette.Surface, band.GetComponent<Image>().color);
        var host = new GameObject("Unbuilt Community");
        try
        {
            var unbuilt = host.AddComponent<OctopusSampleCommunityView>();
            Assert.DoesNotThrow(unbuilt.ToggleDestinations);
            Assert.DoesNotThrow(unbuilt.ToggleComposer);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void TheTabRendersItsFourDoorsAndTheirFields()
    {
        var shell = Shell();

        foreach (var id in new[]
                 {
                     OctopusSampleCommunityView.OpenId,
                     OctopusSampleCommunityView.OpenGroupId,
                     OctopusSampleCommunityView.OpenPostId,
                     OctopusSampleCommunityView.CreatePostId,
                     OctopusSampleCommunityView.GroupFieldId,
                     OctopusSampleCommunityView.PostFieldId,
                     OctopusSampleCommunityView.PrefillFieldId,
                     OctopusSampleCommunityView.StateCardId,
                     OctopusSampleCommunityView.ResultId,
                 })
        {
            Assert.IsNotNull(Find(shell.transform, id),
                "Nothing in the Community tab is named '" + id + "' — the QA pipeline addresses " +
                "it by that name.");
        }
    }

    [Test]
    public void ArrivingOnTheTabCallsNothing()
    {
        // The same rule as Home and Scenarios: a tab that initialises or opens on arrival makes
        // every later QA step ambiguous about what caused what.
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        Shell();

        Assert.IsEmpty(_sdk.ScenarioMethods, "Opening the Community tab reached the SDK: " +
                                     string.Join(", ", _sdk.ScenarioMethods));
        Assert.IsEmpty(probe.Methods, "Opening the Community tab logged an SDK call: " +
                                      string.Join(", ", probe.Methods));
    }

    [Test]
    public void TheMainDoorInitialisesTheSdkThenOpensIt()
    {
        var view = View();

        view.OpenCommunity();

        Assert.AreEqual(new[] { "Initialize", "Open" }, _sdk.ScenarioMethods,
            "The main door did not initialise before opening, or opened something else.");
        Assert.IsNotEmpty(view.Result, "The tab opened the community and said nothing about it.");
    }

    [Test]
    public void ASecondDoorReusesTheInitialisationRatherThanRepeatingIt()
    {
        // OctopusSDK.Initialize is not idempotent — it creates a fresh OctopusChannel every call —
        // which is why every door goes through OctopusScenarioSdk.EnsureInitialized.
        var view = View();

        view.OpenCommunity();
        view.SetPostId("post-1");
        view.OpenPost();

        Assert.AreEqual(new[] { "Initialize", "Open", "OpenPost" }, _sdk.ScenarioMethods);
    }

    [Test]
    public void TheGroupDoorPassesTheIdInItsField()
    {
        var view = View();
        view.SetGroupId("  group-42  ");

        view.OpenGroup();

        Assert.AreEqual("OpenGroup", _sdk.Last.Method);
        Assert.AreEqual("group-42", _sdk.Last.Args[0],
            "The group id reached the SDK untrimmed, so a pasted id with a trailing space opens " +
            "the main feed instead.");
    }

    [Test]
    public void TheGroupDoorWithoutAnIdCallsNothingAndSaysWhy()
    {
        var view = View();

        view.OpenGroup();

        Assert.IsEmpty(_sdk.ScenarioMethods,
            "An empty group id reached the SDK, which lands on the main feed — indistinguishable " +
            "from a wrong id.");
        StringAssert.Contains("group id", view.Result.ToLowerInvariant());
    }

    [Test]
    public void ThePostDoorPassesTheIdInItsFieldAndRefusesAnEmptyOne()
    {
        var view = View();

        view.OpenPost();
        Assert.IsEmpty(_sdk.ScenarioMethods, "An empty post id reached the SDK.");
        StringAssert.Contains("post id", view.Result.ToLowerInvariant());

        view.SetPostId("post-7");
        view.OpenPost();
        Assert.AreEqual("OpenPost", _sdk.Last.Method);
        Assert.AreEqual("post-7", _sdk.Last.Args[0]);
    }

    [Test]
    public void TheEditorOpensBlankWithoutTextAndPrefilledWithIt()
    {
        var view = View();

        view.OpenCreatePost();
        Assert.AreEqual("OpenCreatePost", _sdk.Last.Method);
        Assert.IsNull(_sdk.Last.Args[0],
            "An empty prefill field built a prefilled post with an empty body, which is not the " +
            "same thing as a blank editor.");

        view.SetPrefillText("Hello from the Unity sample");
        view.OpenCreatePost();
        Assert.AreEqual("Hello from the Unity sample", _sdk.Last.Args[0]);
    }

    [Test]
    public void NoDoorPublishesAnything()
    {
        // The editor door opens an editor. Anything that reached the community without the user
        // tapping the editor's own send control would post to a shared community from a test pass.
        var view = View();
        view.SetGroupId("g");
        view.SetPostId("p");
        view.SetPrefillText("t");

        view.OpenCommunity();
        view.OpenGroup();
        view.OpenPost();
        view.OpenCreatePost();

        foreach (var method in _sdk.ScenarioMethods)
        {
            Assert.AreNotEqual("ConnectUser", method);
            Assert.AreNotEqual("Track", method);
        }
        Assert.AreEqual(new[] { "Initialize", "Open", "OpenGroup", "OpenPost", "OpenCreatePost" },
                        _sdk.ScenarioMethods);
    }

    [Test]
    public void AMissingConfigAssetStopsEveryDoorAndSaysSo()
    {
        _sdk.Profile = null;
        var view = View();

        view.SetGroupId("g");
        view.OpenCommunity();
        view.OpenGroup();

        Assert.IsEmpty(_sdk.ScenarioMethods,
            "A door called the SDK with no API key to initialise it with.");
        StringAssert.Contains("OctopusExampleConfig", view.Result);
    }

    [TestCase((string)null)]
    [TestCase("")]
    [TestCase("   ")]
    public void AConfigAssetWithNoUsableKeyStopsEveryDoorJustLikeAMissingOne(string apiKey)
    {
        // The half-finished setup, and the one worth a test: the asset exists, so a `profile != null`
        // guard lets all four doors through and `Initialize` accepts the blank key — after which
        // every screen reports "initialised" and the first real call fails far from the cause.
        _sdk.Profile = new OctopusExampleConfig.ExampleProfile { apiKey = apiKey };
        var view = View();

        view.SetGroupId("g");
        view.SetPostId("p");
        view.SetPrefillText("hello");
        view.OpenCommunity();
        view.OpenGroup();
        view.OpenPost();
        view.OpenCreatePost();

        Assert.IsEmpty(_sdk.ScenarioMethods,
            "A door initialised the SDK with a blank API key: " + string.Join(", ", _sdk.ScenarioMethods));
        StringAssert.Contains("API key", view.Result);
        Assert.AreEqual(OctopusSampleCommunityView.BlockedBandId, view.VisibleBand,
            "The doors are all inert and the band did not say so.");
    }

    [Test]
    public void TheStateCardFollowsWhatTheSdkPublishes()
    {
        var view = View();
        StringAssert.Contains("not granted", view.StateSummary);

        // The SDK has published no count yet, and the sample never asks for one. Home's dash, not
        // the raw sentinel: "-1 unseen notifications" reads as a counter that broke.
        StringAssert.Contains("—", view.StateSummary);
        Assert.IsFalse(view.StateSummary.Contains("-1"),
            "The card is showing the not-yet-published sentinel as a count: " + view.StateSummary);

        OctopusSampleState.ReportCommunityAccess(true);
        OctopusSampleState.ReportUnseenNotifications(4);

        var summary = view.StateSummary;
        Assert.IsFalse(summary.Contains("not granted"),
            "The state card still says access is not granted: " + summary);
        StringAssert.Contains("granted", summary);
        StringAssert.Contains("4", summary);
    }

    [Test]
    public void TheStateCardSaysWhetherAnythingHasInitialisedTheSdk()
    {
        var view = View();
        StringAssert.Contains("not initialised", view.StateSummary);

        view.OpenCommunity();

        Assert.IsFalse(view.StateSummary.Contains("not initialised"),
            "A door initialised the SDK and the card kept saying nothing had: " +
            view.StateSummary);
        StringAssert.Contains(OctopusScenarioSdk.PilotModeLabel, view.StateSummary);
    }

    [Test]
    public void TheStateCardKeepsSayingWhatTheLastConnectionCallDidAfterTheBandGoesDown()
    {
        // The band can only speak about calls, not about sessions: a connect that completes without
        // authenticating takes it down. So the card has to carry the fact the band drops, in Home's
        // vocabulary rather than a second one of its own.
        var view = View();
        StringAssert.Contains("NO CALL", view.StateSummary);

        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        Assert.AreEqual(string.Empty, view.VisibleBand);
        StringAssert.Contains("CONNECT OK", view.StateSummary);

        OctopusSampleState.ReportSession(OctopusSampleState.Session.Failed, "refused");
        StringAssert.Contains("CALL FAILED", view.StateSummary);
    }

    [Test]
    public void WithoutAConfigAssetTheBlockingBandIsTheOneShowing()
    {
        _sdk.Profile = null;

        var view = View();

        Assert.AreEqual(OctopusSampleCommunityView.BlockedBandId, view.VisibleBand,
            "A tab whose doors are all inert did not say so at the top.");
    }

    [Test]
    public void WithAConfigAssetAndNoConnectedUserTheBandIsTheReadOnlyOne()
    {
        // Blocking beats degraded and only one shows at a time — the rule the three other samples
        // draw this band by.
        var view = View();

        Assert.AreEqual(OctopusSampleCommunityView.ReadOnlyBandId, view.VisibleBand);
    }

    [Test]
    public void ConnectingAUserTakesTheReadOnlyBandDown()
    {
        var view = View();

        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");

        Assert.AreEqual(string.Empty, view.VisibleBand,
            "The tab still warns about a read-only community after a user connected.");
    }

    [Test]
    public void ADisconnectPutsTheReadOnlyBandBack()
    {
        var view = View();
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");

        OctopusSampleState.ReportSession(OctopusSampleState.Session.Disconnected, "disconnected");

        Assert.AreEqual(OctopusSampleCommunityView.ReadOnlyBandId, view.VisibleBand);
    }

    [Test]
    public void TheReadOnlyBandSendsTheReaderToTheTabThatCanConnectOne()
    {
        var shell = Shell();
        var view = shell.GetComponentInChildren<OctopusSampleCommunityView>();
        Assert.IsNotNull(view, "The Community tab built no view.");
        Assert.IsNotNull(Find(shell.transform, OctopusSampleCommunityView.ReadOnlyBandId + "-action"),
            "The read-only band has no action to take.");

        // Driven through the method the button is wired to, not through the button: this assembly
        // overrides its references down to nunit and cannot name `Button` to invoke its click.
        view.GoToScenarios();

        Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected,
            "The band's action did not land on the tab that connects a user.");
    }

    [Test]
    public void TheTwoBandsAreNeverBothOnScreen()
    {
        // VisibleBand answers with the first band it finds up, so on its own it cannot catch two
        // bands showing at once. This reads both flags.
        _sdk.Profile = null;
        var shell = Shell();
        var blocked = Find(shell.transform, OctopusSampleCommunityView.BlockedBandId);
        var readOnly = Find(shell.transform, OctopusSampleCommunityView.ReadOnlyBandId);
        Assert.IsNotNull(blocked, "The blocking band was never built.");
        Assert.IsNotNull(readOnly, "The read-only band was never built.");

        // Both conditions hold — no config asset AND no connect call — and blocking wins alone.
        Assert.IsTrue(blocked.gameObject.activeSelf);
        Assert.IsFalse(readOnly.gameObject.activeSelf,
            "A reader who cannot open anything is also being told they will be read-only.");

        // A key appears (as it does when the tab is rebuilt after a fixed setup): the degraded band
        // takes over, and the blocking one goes down rather than stacking.
        _sdk.Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key" };
        OctopusSampleState.ReportUnseenNotifications(1);

        Assert.IsFalse(blocked.gameObject.activeSelf);
        Assert.IsTrue(readOnly.gameObject.activeSelf);
    }

    [Test]
    public void LeavingTheTabStopsItListening()
    {
        // A handler left on a static event outlives the view it was built for. It is invisible from
        // the outside — OnStateChanged returns quietly on a destroyed view — so the count is what
        // separates "unsubscribed" from "still subscribed and silent".
        var shell = Shell();
        var view = shell.GetComponentInChildren<OctopusSampleCommunityView>();
        OctopusSampleState.ReportUnseenNotifications(1);
        var whileOnScreen = view.StateEventCount;
        Assert.Greater(whileOnScreen, 0,
            "The view never heard the state event at all, so this test proves nothing.");

        // Edit mode delivers neither OnDisable nor OnDestroy to a plain MonoBehaviour, so the view
        // is still subscribed the moment it is destroyed; the guarantee is that it drops itself on
        // the first event that reaches it afterwards, which is the path a scene unload takes too.
        shell.Select(OctopusSampleTab.Home);
        OctopusSampleState.ReportUnseenNotifications(9);
        var afterUnhooking = view.StateEventCount;

        OctopusSampleState.ReportUnseenNotifications(10);

        Assert.AreEqual(afterUnhooking, view.StateEventCount,
            "The destroyed Community view is still on OctopusSampleState.Changed.");
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void DestinationSectionsExpandWithoutCallingAndRetainTheirFields(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var view = View();
        var group = Find(view.transform, OctopusSampleCommunityView.GroupFieldId);
        var composer = Find(view.transform, OctopusSampleCommunityView.PrefillFieldId);
        Assert.IsFalse(group.gameObject.activeInHierarchy);
        Assert.IsFalse(composer.gameObject.activeInHierarchy);
        Find(view.transform, OctopusSampleCommunityView.DestinationToggleId).GetComponent<Button>().onClick.Invoke();
        Find(view.transform, OctopusSampleCommunityView.ComposerToggleId).GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(group.gameObject.activeInHierarchy);
        Assert.IsTrue(composer.gameObject.activeInHierarchy);
        view.SetGroupId("group-42");
        view.ToggleDestinations();
        view.ToggleDestinations();
        Assert.AreEqual("group-42", group.GetComponent<TMP_InputField>().text);
        Assert.IsEmpty(_sdk.ScenarioMethods);
    }

    [Test]
    public void ReportedAccessShowsOneBandAndConfigurationStillTakesPriority()
    {
        var view = View();
        OctopusSampleState.ReportCommunityAccess(true);
        Assert.AreEqual(OctopusSampleCommunityView.GrantedBandId, view.VisibleBand);
        Assert.IsFalse(Find(view.transform, OctopusSampleCommunityView.ReadOnlyBandId).gameObject.activeSelf);
        _sdk.Profile = null;
        OctopusSampleState.ReportUnseenNotifications(3);
        Assert.AreEqual(OctopusSampleCommunityView.BlockedBandId, view.VisibleBand);
        Assert.IsFalse(Find(view.transform, OctopusSampleCommunityView.GrantedBandId).gameObject.activeSelf);
    }

    [Test]
    public void ReturningFromNativePresentationKeepsCommunityAndItsDestination()
    {
        var shell = Shell();
        var view = shell.GetComponentInChildren<OctopusSampleCommunityView>();
        view.SetPostId("post-7");
        view.OpenPost();
        StringAssert.Contains("requested", view.Result);
        view.OnApplicationPause(true);
        view.OnApplicationPause(false);
        Assert.AreEqual(OctopusSampleTab.Community, shell.Selected);
        Assert.AreSame(view, shell.GetComponentInChildren<OctopusSampleCommunityView>());
        Assert.AreEqual("post-7", Find(view.transform, OctopusSampleCommunityView.PostFieldId).GetComponent<TMP_InputField>().text);
        StringAssert.Contains("Returned", view.Result);
        Assert.AreEqual(new[] { "Initialize", "OpenPost" }, _sdk.ScenarioMethods);
    }

    [Test]
    public void ASynchronousNativeFailureIsReportedWithoutPublishingOrClaimingSuccess()
    {
        OctopusScenarioSdk.Use(new ThrowingOpenSdk(_sdk));
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        var view = View();
        Assert.DoesNotThrow(() => view.OpenCommunity());
        StringAssert.Contains("Could not request", view.Result);
        Assert.IsTrue(probe.StateChanges.Exists(change => change.detail == "Simulated presentation failure"));
        CollectionAssert.AreEqual(new[] { "Initialize" }, _sdk.ScenarioMethods);
        view.OnApplicationPause(true);
        view.OnApplicationPause(false);
        StringAssert.Contains("Could not request", view.Result);
    }

    private sealed class ThrowingOpenSdk : IOctopusScenarioSdk
    {
        private readonly OctopusRecordingScenarioSdk _recording;
        public ThrowingOpenSdk(OctopusRecordingScenarioSdk recording) { _recording = recording; }
        public OctopusExampleConfig.ExampleProfile Profile { get { return _recording.Profile; } }
        public OctopusExampleConfig.ExampleProfile AlternateProfile { get { return _recording.AlternateProfile; } }
        public bool HasAccessToCommunity { get { return _recording.HasAccessToCommunity; } }
        public event System.Action<OctopusEvent> OnOctopusEvent
        { add { _recording.OnOctopusEvent += value; } remove { _recording.OnOctopusEvent -= value; } }
        public event System.Action<bool> OnHasAccessToCommunityChanged
        { add { _recording.OnHasAccessToCommunityChanged += value; } remove { _recording.OnHasAccessToCommunityChanged -= value; } }
        public event System.Action<int> OnNotSeenNotificationsCount
        { add { _recording.OnNotSeenNotificationsCount += value; } remove { _recording.OnNotSeenNotificationsCount -= value; } }
        public void TrackAccessToCommunity(bool value) { _recording.TrackAccessToCommunity(value); }
        public void OverrideCommunityAccess(bool value, System.Action completed, System.Action<string> error)
        { _recording.OverrideCommunityAccess(value, completed, error); }
        public void SwitchCommunity(string key, ConnectionMode mode, System.Action completed, System.Action<string> error)
        { _recording.SwitchCommunity(key, mode, completed, error); }
        public void Reset(System.Action completed, System.Action<string> error) { _recording.Reset(completed, error); }
        public void Stop(System.Action completed, System.Action<string> error) { _recording.Stop(completed, error); }
        public void UpdateNotSeenNotificationsCount() { _recording.UpdateNotSeenNotificationsCount(); }
        public bool IsOctopusNotification(IDictionary<string, string> payload) { return _recording.IsOctopusNotification(payload); }
        public OctopusNotification GetOctopusNotification(IDictionary<string, string> payload) { return _recording.GetOctopusNotification(payload); }
        public void Open(OctopusNotification notification) { Open(); }
        public void Initialize(string key, ConnectionMode mode) { _recording.Initialize(key, mode); }
        public void ApplyTheme(OctopusColorScheme light, OctopusColorScheme dark) { _recording.ApplyTheme(light, dark); }
        public void SetColorSchemeType(int type) { _recording.SetColorSchemeType(type); }
        public void SetLogo(OctopusLogo logo) { _recording.SetLogo(logo); }
        public void SetFonts(OctopusFonts fonts) { _recording.SetFonts(fonts); }
        public OctopusProfile CurrentProfile { get { return _recording.CurrentProfile; } }
        public event System.Action<OctopusProfile> ProfileChanged
        {
            add { _recording.ProfileChanged += value; }
            remove { _recording.ProfileChanged -= value; }
        }
        public void RefreshEntitlements(System.Action success, System.Action<OctopusRefreshEntitlementsError> error)
        { _recording.RefreshEntitlements(success, error); }
        public void DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode? mode)
        { _recording.DebugOverrideTermsAcceptanceMode(mode); }
        public void DebugGetCommunityConfig(System.Action<OctopusCommunityConfig> result, System.Action<string> error)
        { _recording.DebugGetCommunityConfig(result, error); }
        public void DebugOverrideProfileFieldsLock(OctopusProfileFieldsLock fieldsLock)
        { _recording.DebugOverrideProfileFieldsLock(fieldsLock); }
        public void FetchCommunityData(OctopusCommunityMemberId memberId,
            System.Action<OctopusCommunityData> result, System.Action<string> error)
        { _recording.FetchCommunityData(memberId, result, error); }
        public event System.Action<OctopusCommunityData> CommunityDataChanged
        {
            add { _recording.CommunityDataChanged += value; }
            remove { _recording.CommunityDataChanged -= value; }
        }
        public void StartObservingCommunityData(OctopusCommunityMemberId memberId)
        { _recording.StartObservingCommunityData(memberId); }
        public void StopObservingCommunityData() { _recording.StopObservingCommunityData(); }
        public void Open() { throw new System.InvalidOperationException("Simulated presentation failure"); }
        public void OpenGroup(string id) { Open(); }
        public void OpenPost(string id) { Open(); }
        public void OpenProfile(string id) { Open(); }
        public void OpenCreatePost(OctopusPrefilledPost post) { Open(); }
        public System.Threading.Tasks.Task ConnectUser(string id, string nickname, string bio, string picture,
            System.Func<System.Threading.Tasks.Task<string>> token) { throw new System.NotSupportedException(); }
        public System.Threading.Tasks.Task DisconnectUser() { throw new System.NotSupportedException(); }
        public void Track(string name, IDictionary<string, string> properties) { throw new System.NotSupportedException(); }
        public void OverrideDefaultLocale(string language) { throw new System.NotSupportedException(); }

        // Community & groups seam members: forwarded to the recording fake so the
        // presentation-failure scenario above stays focused on Open().
        public event System.Action<IList<OctopusGroup>> GroupsChanged
        {
            add { _recording.GroupsChanged += value; }
            remove { _recording.GroupsChanged -= value; }
        }
        public void FetchGroups(System.Action<IList<OctopusGroup>> completed, System.Action<string> error)
        { _recording.FetchGroups(completed, error); }
        public void FollowGroup(string groupId, System.Action completed, System.Action<OctopusGroupFollowUnfollowError> error)
        { _recording.FollowGroup(groupId, completed, error); }
        public void UnfollowGroup(string groupId, System.Action completed, System.Action<OctopusGroupFollowUnfollowError> error)
        { _recording.UnfollowGroup(groupId, completed, error); }
        public void SyncFollowGroups(IList<OctopusSyncFollowGroupAction> actions,
            System.Action<IList<OctopusSyncFollowGroupResult>> completed, System.Action<string> error)
        { _recording.SyncFollowGroups(actions, completed, error); }
        public event System.Action<string> GroupAccessDenied
        {
            add { _recording.GroupAccessDenied += value; }
            remove { _recording.GroupAccessDenied -= value; }
        }
        public void DebugOverrideContentOptions(OctopusContentOptions options)
        { _recording.DebugOverrideContentOptions(options); }
        public void SetReaction(string postId, OctopusReactionKind? kind, System.Action completed,
            System.Action<OctopusSetReactionError> error)
        { _recording.SetReaction(postId, kind, completed, error); }
        public event System.Action<string, OctopusPost> ClientObjectRelatedPostChanged
        {
            add { _recording.ClientObjectRelatedPostChanged += value; }
            remove { _recording.ClientObjectRelatedPostChanged -= value; }
        }
        public event System.Action<string> NavigateToClientObject
        {
            add { _recording.NavigateToClientObject += value; }
            remove { _recording.NavigateToClientObject -= value; }
        }
        public void FetchOrCreateClientObjectRelatedPost(OctopusClientObject clientObject,
            System.Action<string> completed, System.Action<OctopusClientPostError> error)
        { _recording.FetchOrCreateClientObjectRelatedPost(clientObject, completed, error); }
        public void StartObservingClientObjectRelatedPost(string objectId)
        { _recording.StartObservingClientObjectRelatedPost(objectId); }
        public void StopObservingClientObjectRelatedPost(string objectId)
        { _recording.StopObservingClientObjectRelatedPost(objectId); }
        public System.Threading.Tasks.Task<string> SignBridgeShare(string fingerprint)
        { return _recording.SignBridgeShare(fingerprint); }
        public string PrepareBundledShareImage() { return _recording.PrepareBundledShareImage(); }
    }

    private OctopusSampleShell Shell()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        shell.Select(OctopusSampleTab.Community);
        return shell;
    }

    private OctopusSampleCommunityView View()
    {
        var view = Shell().GetComponentInChildren<OctopusSampleCommunityView>();
        Assert.IsNotNull(view, "The Community tab built no view.");
        return view;
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
