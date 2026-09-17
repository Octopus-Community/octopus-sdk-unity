using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class OctopusHostNotificationsScenarioTests
{
    private OctopusRecordingScenarioSdk _sdk;
    [SetUp] public void SetUp()
    {
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "qa-example-api-key" }
        };
        OctopusScenarioSdk.Use(_sdk);
    }
    [TearDown] public void TearDown()
    {
        OctopusScenarioSdk.Use(null);
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }
    private static void Tap(OctopusScenarioPilot pilot, int index)
    {
        pilot.Presets[index].Fill(pilot.Fields);
        pilot.Execute(() => pilot.Presets[index].Run(pilot.Fields));
    }
    private void Warm()
    {
        Tap(new LocaleScenario(), 0);
        _sdk.Clear();
    }
    [Test] public void EventsClearIsPureAndDoesNotCallSdk()
    {
        var pilot = new EventsScenario();
        pilot.Presets[0].Fill(pilot.Fields);
        Assert.AreEqual("clear", pilot.Fields.Get("action"));
        Assert.IsEmpty(_sdk.Calls);
        Tap(pilot, 0);
        Assert.IsEmpty(_sdk.Calls);
        Assert.AreEqual("Log was already empty.", pilot.Result);
    }
    [Test] public void EventsArePersistentTypedNewestFirstAndBounded()
    {
        Warm();
        _sdk.EmitEvent(new PostCreatedEvent { PostId = "qa-post", TextLength = 7 });
        var pilot = new EventsScenario();
        StringAssert.Contains("Post Created", pilot.Result);
        StringAssert.Contains("Text length: 7", pilot.Result);
        _sdk.EmitEvent(new CommentCreatedEvent { CommentId = "qa-comment" });
        Assert.IsTrue(pilot.Result.StartsWith("Comment Created"));
        for (int i = 0; i < 205; i++) _sdk.EmitEvent(new SessionStartedEvent());
        Assert.AreEqual(50, OctopusScenarioSdk.EventLines.Count);
        Tap(pilot, 0);
        Assert.IsEmpty(OctopusScenarioSdk.EventLines);
        Assert.AreEqual("Cleared 50 events from the local log.", pilot.Result);
        _sdk.EmitEvent(new PostClickedEvent { PostId = "qa-next" });
        StringAssert.Contains("qa-next", pilot.Result);
    }
    [Test] public void EventsRaisedOffTheMainThreadArePostedBackBeforeTouchingPilots()
    {
        Warm();
        var posted = new List<Action>();
        OctopusScenarioSdk.MainThreadPoster = posted.Add;
        var pilot = new EventsScenario();
        var before = pilot.Result;
        var worker = new System.Threading.Thread(() =>
            _sdk.EmitEvent(new PostCreatedEvent { PostId = "qa-background", TextLength = 3 }));
        worker.Start();
        worker.Join();
        Assert.IsEmpty(OctopusScenarioSdk.EventLines, "a background SDK thread must not mutate the log");
        Assert.AreEqual(before, pilot.Result, "nor notify pilots and their renderers");
        Assert.AreEqual(1, posted.Count, "the work is posted to the main thread once");
        posted[0]();
        Assert.AreEqual(1, OctopusScenarioSdk.EventLines.Count);
        StringAssert.Contains("qa-background", pilot.Result);
    }
    [Test] public void SwappingSdkDetachesOldEventSource()
    {
        Warm();
        OctopusScenarioSdk.Use(new OctopusRecordingScenarioSdk());
        _sdk.EmitEvent(new PostClickedEvent());
        Assert.IsEmpty(OctopusScenarioSdk.EventLines);
    }
    [TestCase(true)] [TestCase(false)]
    public void TrackCohortFillPreservesDecisionAndRunOpensOnlyWhenGranted(bool access)
    {
        var pilot = new TrackABTestsScenario();
        Assert.AreEqual("true", pilot.Fields.Get("canAccessCommunity"));
        pilot.Fields.Set("canAccessCommunity", access.ToString());
        pilot.Presets[0].Fill(pilot.Fields);
        Assert.AreEqual(access.ToString(), pilot.Fields.Get("canAccessCommunity"));
        Assert.IsEmpty(_sdk.Calls);
        Warm();
        Tap(pilot, 0);
        CollectionAssert.AreEqual(access ? new[] { "TrackAccessToCommunity", "Open" } : new[] { "TrackAccessToCommunity" }, _sdk.ScenarioMethods);
        Assert.AreEqual(access, _sdk.Calls[0].Args[0]);
    }
    [Test] public void InvalidHostCohortMakesNoCall()
    {
        var pilot = new TrackABTestsScenario();
        pilot.Fields.Set("canAccessCommunity", "invalid");
        pilot.RunCustom();
        Assert.IsEmpty(_sdk.Calls);
        StringAssert.Contains("must be true or false", pilot.Result);
    }

    [TestCase(0, "true")] [TestCase(1, "false")] [TestCase(2, "current")]
    public void OverrideFillIsPure(int index, string value)
    {
        var pilot = new ForceOctopusABTestsScenario();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(value, pilot.Fields.Get("hasAccess"));
        Assert.IsEmpty(_sdk.Calls);
    }
    [TestCase(0, true)] [TestCase(1, false)] [TestCase(2, true)] [TestCase(2, false)]
    public void OverrideUsesPresetOrCurrentSdkValue(int index, bool access)
    {
        Warm();
        _sdk.EmitAccess(access);
        var pilot = new ForceOctopusABTestsScenario();
        Tap(pilot, index);
        CollectionAssert.AreEqual(new[] { "OverrideCommunityAccess" }, _sdk.ScenarioMethods);
        Assert.AreEqual(access, _sdk.Last.Args[0]);
        StringAssert.Contains("success", pilot.Result);
    }
    [Test] public void UnknownAccessDoesNotOverrideWithDefaultFalse()
    {
        Warm();
        var pilot = new ForceOctopusABTestsScenario();
        Tap(pilot, 2);
        Assert.IsEmpty(_sdk.Calls);
        StringAssert.Contains("not yet known", pilot.Result);
    }
    [Test] public void OverrideFailureReleasesSlotAndDoesNotInventTypedError()
    {
        Warm();
        _sdk.CallbackError = "offline";
        var pilot = new ForceOctopusABTestsScenario();
        Tap(pilot, 0);
        StringAssert.Contains("string error): offline", pilot.Result);
        _sdk.CallbackError = null;
        Tap(pilot, 1);
        StringAssert.Contains("success", pilot.Result);
    }
    [Test] public void DeferredOverrideKeepsRunningUntilCallback()
    {
        Warm();
        _sdk.DeferCompletions = true;
        var pilot = new ForceOctopusABTestsScenario();
        Tap(pilot, 0);
        _sdk.EmitAccess(true);
        Assert.IsTrue(pilot.IsRunning);
        Tap(pilot, 1);
        Assert.AreEqual(1, _sdk.Calls.Count);
        _sdk.PendingCompleted();
        Assert.IsFalse(pilot.IsRunning);
        StringAssert.Contains("hasAccessToCommunity: true", pilot.Result);
    }

    [TestCase(0, "switchCommunity", "Alternate (OctopusExampleConfig)")]
    [TestCase(1, "reset", "Current community")]
    [TestCase(2, "stop", "Current community")]
    public void LifecycleFillIsPure(int index, string action, string profile)
    {
        var pilot = new LifecycleScenario();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(action, pilot.Fields.Get("action"));
        Assert.AreEqual(profile, pilot.Fields.Get("profile"));
        Assert.IsEmpty(_sdk.Calls);
    }
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void LifecycleBeforeInitializationReportsRefusalWithoutCallingSdk(int index)
    {
        var pilot = new LifecycleScenario();
        var operationToken = OctopusScenarioSdk.OperationToken;
        Tap(pilot, index);
        Assert.IsEmpty(_sdk.Calls);
        StringAssert.Contains("SDK is not initialised", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
        Assert.AreEqual(operationToken, OctopusScenarioSdk.OperationToken);
    }
    [Test] public void SwitchUsesAlternateProfileAndSubsequentConnectionUsesIt()
    {
        Warm();
        _sdk.AlternateProfile = new OctopusExampleConfig.ExampleProfile { apiKey = "qa-alternate-key", userId = "qa-alternate-user", authToken = "not-a-real-token" };
        var pilot = new LifecycleScenario();
        Tap(pilot, 0);
        CollectionAssert.AreEqual(new[] { "SwitchCommunity" }, _sdk.ScenarioMethods);
        Assert.AreEqual("qa-alternate-key", _sdk.Calls[0].Args[0]);
        Assert.AreEqual("sso", ((ConnectionMode)_sdk.Calls[0].Args[1]).Mode);
        StringAssert.DoesNotContain("qa-alternate-key", pilot.Result);
        _sdk.Clear();
        Tap(new ConnectionScenario(), 0);
        Assert.AreEqual("ConnectUser", _sdk.Last.Method);
        Assert.AreEqual("qa-alternate-user", _sdk.Last.Args[0]);
    }
    [TestCase(1, "Reset", true)] [TestCase(2, "Stop", false)]
    public void LifecycleCompletionUpdatesOnlyHostRecordedState(int index, string method, bool initialized)
    {
        Warm();
        _sdk.DeferCompletions = true;
        var pilot = new LifecycleScenario();
        Tap(pilot, index);
        Assert.IsTrue(pilot.IsRunning);
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        CollectionAssert.AreEqual(new[] { method }, _sdk.ScenarioMethods);
        Tap(new LocaleScenario(), 0);
        Assert.AreEqual(1, _sdk.Calls.Count, "No other SDK call during lifecycle.");
        _sdk.PendingCompleted();
        Assert.AreEqual(initialized, OctopusSampleState.IsInitialized);
        Assert.IsFalse(pilot.IsRunning);
        StringAssert.Contains("No isInitialised stream", pilot.Result);
    }
    [Test] public void MissingAlternateMakesNoCallAndLifecycleFailureReleasesSlot()
    {
        Warm();
        var pilot = new LifecycleScenario();
        Tap(pilot, 0);
        Assert.IsEmpty(_sdk.Calls);
        StringAssert.Contains("OctopusExampleConfig", pilot.Result);
        _sdk.CallbackError = "qa-alternate-key";
        Tap(pilot, 2);
        StringAssert.DoesNotContain("qa-alternate-key", pilot.Result);
        StringAssert.Contains("failed", pilot.Result);
        _sdk.CallbackError = null;
        Tap(pilot, 1);
        StringAssert.Contains("SDK is not initialised", pilot.Result);
        Warm();
        Tap(pilot, 1);
        StringAssert.Contains("completed", pilot.Result);
    }

    [TestCase(0, "open", "Open")] [TestCase(1, "refresh", "UpdateNotSeenNotificationsCount")]
    public void NotSeenPresetsFillPurelyAndCallSdk(int index, string action, string method)
    {
        var pilot = new NotSeenNotificationsScenario();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(action, pilot.Fields.Get("action"));
        Assert.IsEmpty(_sdk.Calls);
        Warm();
        Tap(pilot, index);
        CollectionAssert.AreEqual(new[] { method }, _sdk.ScenarioMethods);
        StringAssert.Contains("notSeenNotificationsCount: —", pilot.Result);
        _sdk.EmitCount(5);
        StringAssert.Contains("notSeenNotificationsCount: 5", pilot.Result);
        _sdk.EmitCount(0);
        StringAssert.Contains("notSeenNotificationsCount: 0", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }
    [Test] public void RefreshFailureIsShownAndNextRunStillWorks()
    {
        Warm();
        var pilot = new NotSeenNotificationsScenario();
        _sdk.ThrowOnRefresh = true;
        Tap(pilot, 1);
        StringAssert.Contains("refresh unavailable", pilot.Result);
        _sdk.ThrowOnRefresh = false;
        Tap(pilot, 1);
        StringAssert.Contains("Refresh requested", pilot.Result);
    }

    [Test] public void PushFillIsPureAndRunReplaysTheFullNotificationPath()
    {
        var pilot = new PushNotificationsScenario();
        pilot.Presets[0].Fill(pilot.Fields);
        Assert.AreEqual("octopus-demo-post-id-unset", pilot.Fields.Get("postId"));
        Assert.IsEmpty(_sdk.Calls);
        Warm();
        Tap(pilot, 0);
        CollectionAssert.AreEqual(new[] { "IsOctopusNotification", "GetOctopusNotification", "OpenNotification" }, _sdk.ScenarioMethods);
        var payload = (IDictionary<string, string>)_sdk.Calls[0].Args[0];
        Assert.AreEqual("true", payload["is_octopus_notification"]);
        Assert.AreEqual("post/octopus-demo-post-id-unset", payload["link_path"]);
        Assert.AreEqual("octopus-demo-post-id-unset", payload["post_id"]);
        var notification = (OctopusNotification)_sdk.Last.Args[0];
        Assert.AreEqual("octopus-sdk://post/octopus-demo-post-id-unset", notification.DeepLink);
        StringAssert.Contains("not-found placeholder", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }
    [TestCase(false, false, 1)] [TestCase(true, true, 2)]
    public void UnrecognizedOrUnparseablePushDoesNotOpen(bool recognized, bool nullNotification, int calls)
    {
        Warm();
        _sdk.RecognizesNotification = recognized;
        _sdk.NullNotification = nullNotification;
        var pilot = new PushNotificationsScenario();
        Tap(pilot, 0);
        Assert.AreEqual(calls, _sdk.Calls.Count);
        CollectionAssert.DoesNotContain(_sdk.ScenarioMethods, "OpenNotification");
        Assert.IsFalse(pilot.IsRunning);
    }
    [Test] public void NotificationOpenFailureDoesNotLeaveThePilotRunning()
    {
        Warm();
        _sdk.ThrowOnNotificationOpen = true;
        var pilot = new PushNotificationsScenario();
        Tap(pilot, 0);
        StringAssert.Contains("notification unavailable", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
        _sdk.ThrowOnNotificationOpen = false;
        Tap(pilot, 0);
        StringAssert.Contains("Opening Octopus", pilot.Result);
    }

    [Test] public void EveryNewPresetUsesCatalogueWordingAndFillsEveryField()
    {
        var labels = new Dictionary<string, string[]>
        {
            { "events", new[] { "Clear log" } },
            { "trackABTests", new[] { "Open Octopus Home Screen" } },
            { "forceOctopusABTests", new[] { "Force cohort · grant community access", "Force cohort · deny community access", "Re-apply current SDK value" } },
            { "lifecycle", new[] { "switchCommunity (alt key)", "reset", "stop" } },
            { "notSeenNotifications", new[] { "Open Octopus (full-page route)", "Refresh not-seen count" } },
            { "pushNotifications", new[] { "Open sample notification (deep link)" } }
        };
        foreach (var item in labels)
        {
            var pilot = OctopusScenarioPilots.Create(item.Key);
            for (int i = 0; i < item.Value.Length; i++)
            {
                foreach (var field in pilot.Fields.All) field.Value = "";
                pilot.Presets[i].Fill(pilot.Fields);
                Assert.AreEqual("Preset " + (i + 1) + " · " + item.Value[i], pilot.Presets[i].Label);
                foreach (var field in pilot.Fields.All) Assert.IsNotEmpty(field.Value);
            }
        }
        Assert.IsEmpty(_sdk.Calls);
    }
    [Test] public void SwitchingCommunityForgetsPriorAccessAndCount()
    {
        Warm();
        _sdk.EmitAccess(true);
        _sdk.EmitCount(8);
        _sdk.AlternateProfile = new OctopusExampleConfig.ExampleProfile { apiKey = "qa-alternate-key" };
        Tap(new LifecycleScenario(), 0);
        Assert.IsNull(OctopusScenarioSdk.ObservedAccess);
        Assert.IsNull(OctopusScenarioSdk.ObservedNotSeenCount);
    }
    [Test] public void ThrowingOverrideReleasesOperationSlot()
    {
        Warm();
        _sdk.ThrowOnCallbackCall = true;
        var pilot = new ForceOctopusABTestsScenario();
        Tap(pilot, 0);
        StringAssert.Contains("bridge unavailable", pilot.Result);
        _sdk.ThrowOnCallbackCall = false;
        Tap(pilot, 1);
        StringAssert.Contains("success", pilot.Result);
    }

    [Test] public void EveryActionIsAnnouncedBeforeItReachesTheSdk()
    {
        Warm();
        _sdk.AlternateProfile = new OctopusExampleConfig.ExampleProfile { apiKey = "qa-alternate-key" };
        var log = new CallLog();
        OctopusSampleLog.Current = log;
        var unannounced = new List<string>();
        _sdk.BeforeCall = method =>
        {
            if (method == "ApplyTheme" || method == "SetColorSchemeType") return;
            var symbol = method == "OpenNotification" ? "Open(OctopusNotification)" : method;
            if (log.Last != "OctopusSDK." + symbol) unannounced.Add(method);
        };
        foreach (var id in new[] { "events", "trackABTests", "forceOctopusABTests", "lifecycle", "notSeenNotifications", "pushNotifications" })
        {
            var pilot = OctopusScenarioPilots.Create(id);
            for (int i = 0; i < pilot.Presets.Count; i++)
            {
                _sdk.EmitAccess(true);
                Tap(pilot, i);
            }
        }
        Assert.IsEmpty(unannounced, "Every SDK call must be announced before it runs.");
    }
    private sealed class CallLog : IOctopusSampleLog
    {
        public string Last;
        public void LogApiCall(string method, string detail = null) { Last = method; }
        public void LogStateChange(string headline, string detail = null) { }
    }

    // Next scenario tests.
}
