using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class OctopusCommunityScenarioTests
{
    private OctopusRecordingScenarioSdk _sdk;
    private readonly List<CommunityScenarioPilot> _pilots = new List<CommunityScenarioPilot>();

    [SetUp]
    public void SetUp()
    {
        _sdk = new OctopusRecordingScenarioSdk
        { Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key" } };
        OctopusScenarioSdk.Use(_sdk);
    }

    [TearDown]
    public void TearDown()
    {
        OctopusScenarioSdk.BridgeShareSigner = null;
        foreach (var pilot in _pilots) pilot.Dispose();
        _pilots.Clear();
        OctopusScenarioSdk.Use(null);
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    private T Pilot<T>() where T : CommunityScenarioPilot, new()
    {
        var pilot = new T();
        _pilots.Add(pilot);
        return pilot;
    }

    private static void Tap(OctopusScenarioPilot pilot, int index)
    {
        pilot.Presets[index].Fill(pilot.Fields);
        pilot.Presets[index].Run(pilot.Fields);
    }

    [Test]
    public void GroupsFetchFillIsPureAndRunReportsAllGroupFlags()
    {
        var pilot = Pilot<GroupsScenario>();
        pilot.Presets[0].Fill(pilot.Fields);
        Assert.AreEqual("fetch", pilot.Fields.Get("action"));
        Assert.AreEqual("none", pilot.Fields.Get("groupId"));
        Assert.IsEmpty(_sdk.Calls);
        _sdk.Groups.Add(new OctopusGroup { Id = "group-1", Name = "Sample group", CanAccess = true });
        pilot.Presets[0].Run(pilot.Fields);
        CollectionAssert.AreEqual(new[] { "Initialize", "FetchGroups" }, _sdk.ScenarioMethods);
        StringAssert.Contains("group-1", pilot.Result);
        StringAssert.Contains("canCreateChildren=False", pilot.Result);
        _sdk.EmitGroups(new List<OctopusGroup>());
        StringAssert.Contains("Groups: 0", pilot.Result);
        pilot.Dispose();
        _sdk.EmitGroups(_sdk.Groups);
        StringAssert.Contains("Groups: 0", pilot.Result);
    }

    [TestCase("follow", "FollowGroup")]
    [TestCase("unfollow", "UnfollowGroup")]
    public void GroupsCustomRunUsesTheSelectedIdAndReportsTypedErrors(string action, string method)
    {
        var pilot = Pilot<GroupsScenario>();
        pilot.Fields.Set("action", action);
        pilot.Fields.Set("groupId", "group-2");
        pilot.RunCustom();
        Assert.AreEqual(method, _sdk.Last.Method);
        Assert.AreEqual("group-2", _sdk.Last.Args[0]);
        _sdk.GroupFollowError = new OctopusGroupFollowUnfollowError(
            OctopusGroupFollowUnfollowErrorCode.MissingGroup, "No group");
        pilot.RunCustom();
        StringAssert.Contains("MissingGroup", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }

    [Test]
    public void GroupsFetchFailureFinishesTheRun()
    {
        _sdk.GroupFetchError = "offline";
        var pilot = Pilot<GroupsScenario>();
        Tap(pilot, 0);
        StringAssert.Contains("FetchGroups failed: offline", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }
    [TestCase(0, "invert", true, false)]
    [TestCase(1, "follow", true, true)]
    [TestCase(2, "unfollow", false, false)]
    public void SyncPresetsUseLiveGroupsAndSkipLockedOnes(int index, string action, bool first, bool second)
    {
        var pilot = Pilot<SyncFollowGroupsScenario>();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(action, pilot.Fields.Get("action"));
        Assert.AreEqual("0", pilot.Fields.Get("offsetMinutes"));
        Assert.IsEmpty(_sdk.Calls);
        _sdk.Groups = new List<OctopusGroup>
        {
            new OctopusGroup { Id = "a", Name = "First", CanChangeFollowStatus = true },
            new OctopusGroup { Id = "b", IsFollowed = true, CanChangeFollowStatus = true },
            new OctopusGroup { Id = "locked", IsFollowed = true }
        };
        _sdk.SyncResults.Add(new OctopusSyncFollowGroupResult { GroupId = "a", Status = OctopusSyncFollowGroupStatus.Skipped });
        var before = DateTime.UtcNow;
        pilot.Presets[index].Run(pilot.Fields);
        Assert.AreEqual("SyncFollowGroups", _sdk.Last.Method);
        var actions = (IList<OctopusSyncFollowGroupAction>)_sdk.Last.Args[0];
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual("a", actions[0].GroupId);
        Assert.AreEqual("b", actions[1].GroupId);
        Assert.AreEqual(first, actions[0].Followed);
        Assert.AreEqual(second, actions[1].Followed);
        Assert.That(actions[0].ActionDate, Is.InRange(before, DateTime.UtcNow));
        Assert.AreEqual(actions[0].ActionDate, actions[1].ActionDate);
        StringAssert.Contains("First: Skipped", pilot.Result);
    }

    [TestCase(-5)]
    [TestCase(5)]
    public void SyncSupportsPastAndFutureDates(int offset)
    {
        var pilot = Pilot<SyncFollowGroupsScenario>();
        pilot.Presets[0].Fill(pilot.Fields);
        pilot.Fields.Set("offsetMinutes", offset.ToString());
        _sdk.Groups.Add(new OctopusGroup { Id = "a", CanChangeFollowStatus = true });
        var before = DateTime.UtcNow.AddMinutes(offset);
        pilot.RunCustom();
        var action = ((IList<OctopusSyncFollowGroupAction>)_sdk.Last.Args[0])[0];
        Assert.That(action.ActionDate, Is.InRange(before, DateTime.UtcNow.AddMinutes(offset)));
    }

    [Test]
    public void SyncEmptyLockedAndFailedFetchNeverSubmitABatch()
    {
        var pilot = Pilot<SyncFollowGroupsScenario>();
        Tap(pilot, 0);
        StringAssert.Contains("No groups in this community", pilot.Result);
        _sdk.Groups.Add(new OctopusGroup { Id = "locked" });
        Tap(pilot, 1);
        StringAssert.Contains("force-followed / locked", pilot.Result);
        _sdk.GroupFetchError = "offline";
        Tap(pilot, 2);
        StringAssert.Contains("FetchGroups failed: offline", pilot.Result);
        Assert.IsFalse(_sdk.Methods.Contains("SyncFollowGroups"));
    }

    [TestCase(0, "register")]
    [TestCase(1, "unregister")]
    public void AccessDeniedFillIsPure(int index, string action)
    {
        var pilot = Pilot<GroupAccessDeniedScenario>();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(action, pilot.Fields.Get("action"));
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void AccessDeniedRegistrationIsIdempotentAndUnregisterStopsDelivery()
    {
        var pilot = Pilot<GroupAccessDeniedScenario>();
        Tap(pilot, 0);
        Tap(pilot, 0);
        Assert.AreEqual(1, _sdk.Methods.Count(m => m == "GroupAccessDenied.add"));
        _sdk.EmitGroupAccessDenied("locked-1");
        StringAssert.Contains("Last groupId: locked-1", pilot.Result);
        StringAssert.Contains("Fire count: 1", pilot.Result);
        Tap(pilot, 1);
        Assert.AreEqual("GroupAccessDenied.remove", _sdk.Last.Method);
        var result = pilot.Result;
        _sdk.EmitGroupAccessDenied("locked-2");
        Assert.AreEqual(result, pilot.Result);
        Tap(pilot, 1);
        StringAssert.Contains("already unregistered", pilot.Result);
        Tap(pilot, 0);
        _sdk.EmitGroupAccessDenied("locked-3");
        StringAssert.Contains("Fire count: 2", pilot.Result);
    }

    [TestCase(0, "override", "true", "OverrideCommunityAccess", true)]
    [TestCase(1, "override", "false", "OverrideCommunityAccess", false)]
    [TestCase(2, "track", "true", "TrackAccessToCommunity", true)]
    public void CommunityAccessPresetsFillPurelyAndCallTheirOwnApi(int index, string action, string value, string method, bool access)
    {
        var pilot = Pilot<CommunityAccessScenario>();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(action, pilot.Fields.Get("action"));
        Assert.AreEqual(value, pilot.Fields.Get("hasAccess"));
        Assert.IsEmpty(_sdk.Calls);
        pilot.Presets[index].Run(pilot.Fields);
        Assert.AreEqual(method, _sdk.Last.Method);
        Assert.AreEqual(access, _sdk.Last.Args[0]);
        if (action == "track") Assert.IsFalse(_sdk.HasAccessToCommunity);
        _sdk.EmitCommunityAccess(true);
        StringAssert.Contains("hasAccessToCommunity: True", pilot.Result);
        _sdk.EmitCommunityAccess(false);
        StringAssert.Contains("hasAccessToCommunity: False", pilot.Result);
    }

    [Test]
    public void CommunityAccessReportsFailureWithoutInventingAccess()
    {
        _sdk.AccessError = "offline";
        var pilot = Pilot<CommunityAccessScenario>();
        Tap(pilot, 0);
        StringAssert.Contains("failed: offline", pilot.Result);
        StringAssert.Contains("hasAccessToCommunity: False", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }

    [TestCase(0, true, true, true, true)]
    [TestCase(1, false, true, true, true)]
    [TestCase(2, true, false, true, true)]
    [TestCase(3, false, false, true, true)]
    [TestCase(4, true, true, false, true)]
    [TestCase(5, true, true, true, false)]
    public void ContentOptionsPresetsFillAndSendTheFlutterMatrix(int index, bool pictures, bool polls, bool comments, bool replies)
    {
        var pilot = Pilot<ContentOptionsScenario>();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual("apply", pilot.Fields.Get("action"));
        CollectionAssert.AreEqual(new[] { pictures, polls, comments, replies },
            pilot.Fields.All.Skip(1).Select(f => bool.Parse(f.Value)).ToArray());
        Assert.IsEmpty(_sdk.Calls);
        pilot.Presets[index].Run(pilot.Fields);
        Assert.AreEqual("DebugOverrideContentOptions", _sdk.Last.Method);
        var options = (OctopusContentOptions)_sdk.Last.Args[0];
        Assert.AreEqual(pictures, options.Post.EnablePictures);
        Assert.AreEqual(polls, options.Post.EnablePolls);
        Assert.AreEqual(comments, options.Comment.EnablePictures);
        Assert.AreEqual(replies, options.Reply.EnablePictures);
        StringAssert.StartsWith("Applied: ", pilot.Result);
    }

    [Test]
    public void ContentClearIsPureAndSendsNullEvenWhenRepeated()
    {
        var pilot = Pilot<ContentOptionsScenario>();
        pilot.Presets[6].Fill(pilot.Fields);
        Assert.AreEqual("clear", pilot.Fields.Get("action"));
        Assert.IsTrue(pilot.Fields.All.Skip(1).All(f => f.Value == "backend"));
        Assert.IsEmpty(_sdk.Calls);
        Tap(pilot, 6);
        Tap(pilot, 6);
        Assert.AreEqual("DebugOverrideContentOptions", _sdk.Last.Method);
        Assert.IsNull(_sdk.Last.Args[0]);
        StringAssert.Contains("none (backend default)", pilot.Result);
        _sdk.ThrowContentOverride = true;
        Tap(pilot, 0);
        StringAssert.Contains("override unavailable", pilot.Result);
    }

    [TestCase(0, "heart", OctopusReactionKind.Heart)]
    [TestCase(1, "joy", OctopusReactionKind.Joy)]
    [TestCase(2, "null", null)]
    [TestCase(3, "mouthOpen", OctopusReactionKind.MouthOpen)]
    [TestCase(4, "clap", OctopusReactionKind.Clap)]
    [TestCase(5, "cry", OctopusReactionKind.Cry)]
    [TestCase(6, "rage", OctopusReactionKind.Rage)]
    public void ReactionsFillPurelyAndSendEachKind(int index, string value, OctopusReactionKind? kind)
    {
        var pilot = Pilot<ReactionsScenario>();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual("unity-demo-fake-post-id", pilot.Fields.Get("postId"));
        Assert.AreEqual(value, pilot.Fields.Get("reaction"));
        Assert.IsEmpty(_sdk.Calls);
        pilot.Fields.Set("postId", "chosen-post");
        pilot.Presets[index].Run(pilot.Fields);
        Assert.AreEqual("SetReaction", _sdk.Last.Method);
        Assert.AreEqual("chosen-post", _sdk.Last.Args[0]);
        Assert.AreEqual(kind, _sdk.Last.Args[1]);
        StringAssert.Contains("success", pilot.Result);
    }

    [TestCase(OctopusSetReactionErrorCode.PostNotFound)]
    [TestCase(OctopusSetReactionErrorCode.UnknownReaction)]
    [TestCase(OctopusSetReactionErrorCode.ReactionError)]
    public void ReactionsReportTheTypedFailure(OctopusSetReactionErrorCode code)
    {
        _sdk.ReactionError = new OctopusSetReactionError(code, "sample failure");
        var pilot = Pilot<ReactionsScenario>();
        Tap(pilot, 0);
        StringAssert.Contains(code.ToString(), pilot.Result);
        StringAssert.Contains("sample failure", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }

    [TestCase(0, "fetch", "recipe-1", "default")]
    [TestCase(1, "fetch", "random", "Gourmands")]
    [TestCase(2, "heart", "latest", "unused")]
    [TestCase(3, "unreact", "latest", "unused")]
    public void BridgePresetFillIsPure(int index, string action, string id, string groupName)
    {
        var pilot = Pilot<BridgeScenario>();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(action, pilot.Fields.Get("action"));
        Assert.AreEqual(id, pilot.Fields.Get("objectId"));
        Assert.AreEqual(groupName, pilot.Fields.Get("groupName"));
        Assert.IsTrue(pilot.Fields.All.All(f => !string.IsNullOrEmpty(f.Value)));
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void BridgeStableAndRandomCarryContentAndUseTheLatestPostForReactions()
    {
        var pilot = Pilot<BridgeScenario>();
        Tap(pilot, 2);
        Assert.IsFalse(_sdk.Methods.Contains("SetReaction"));
        StringAssert.Contains("No post yet", pilot.Result);
        Tap(pilot, 0);
        Assert.AreEqual("recipe-1", _sdk.LastClientObject.ObjectId);
        Assert.AreEqual("View recipe", _sdk.LastClientObject.ViewObjectButtonText);
        Assert.AreEqual("A delicious recipe to test the bridge", _sdk.LastClientObject.CatchPhrase);
        Assert.IsNull(_sdk.LastClientObject.GroupId);
        Assert.IsNotNull(_sdk.LastClientObject.SignBridgeShare);
        StringAssert.Contains("macarons.jpeg", _sdk.LastClientObject.ImageUrl);
        Tap(pilot, 2);
        Assert.AreEqual("SetReaction", _sdk.Last.Method);
        Assert.AreEqual("bridge-post-1", _sdk.Last.Args[0]);
        Assert.AreEqual(OctopusReactionKind.Heart, _sdk.Last.Args[1]);
        Tap(pilot, 3);
        Assert.IsNull(_sdk.Last.Args[1]);
        _sdk.Groups.Add(new OctopusGroup { Id = "recipe-group", Name = "Gourmands" });
        Tap(pilot, 1);
        Assert.AreEqual("recipe-group", _sdk.LastClientObject.GroupId);
        var first = _sdk.LastClientObject.ObjectId;
        pilot.Presets[1].Run(pilot.Fields);
        Assert.AreNotEqual(first, _sdk.LastClientObject.ObjectId);
        Assert.IsTrue(_sdk.Methods.Contains("StopObservingClientObjectRelatedPost"));
        Assert.AreEqual("StartObservingClientObjectRelatedPost", _sdk.Last.Method);
    }

    [Test]
    public void BridgeSnapshotsFilterObjectIdsAndNavigationSurvivesPilotDisposal()
    {
        var pilot = Pilot<BridgeScenario>();
        Tap(pilot, 0);
        _sdk.EmitClientPost("different-object", new OctopusPost("wrong-post"));
        Assert.AreEqual("bridge-post-1", OctopusScenarioSdk.LatestBridgePostId);
        _sdk.EmitClientPost("recipe-1", new OctopusPost("bridge-post-1",
            new[] { new OctopusReactionCount(OctopusReactionKind.Heart, 3) }, 2, 0, OctopusReactionKind.Heart));
        StringAssert.Contains("comments=2, reactions=3, userReaction=Heart", pilot.Result);
        pilot.Dispose();
        _sdk.EmitNavigate("recipe-1");
        var next = Pilot<BridgeScenario>();
        StringAssert.Contains("fires: 1", next.ParameterNotice);
        Tap(next, 3);
        Assert.AreEqual("bridge-post-1", _sdk.Last.Args[0]);
        _sdk.EmitClientPost("recipe-1", null);
        Tap(next, 2);
        StringAssert.Contains("No post yet", next.Result);
    }

    [Test]
    public void BridgeErrorsRemainTypedAndDoNotCreateAnObservation()
    {
        _sdk.BridgeError = new OctopusClientPostError(OctopusClientPostErrorCode.MissingObjectId, "sample failure");
        var pilot = Pilot<BridgeScenario>();
        Tap(pilot, 0);
        StringAssert.Contains("MissingObjectId", pilot.Result);
        Assert.IsFalse(_sdk.Methods.Contains("StartObservingClientObjectRelatedPost"));
    }

    [TestCase(0, true, false, false)]
    [TestCase(1, true, true, false)]
    [TestCase(2, true, false, true)]
    [TestCase(3, true, true, true)]
    [TestCase(4, false, false, true)]
    public void CreatePostPresetsFillPurelyAndSendTheCompletePayload(int index, bool text, bool cta, bool image)
    {
        var pilot = Pilot<CreatePostScenario>();
        pilot.Presets[index].Fill(pilot.Fields);
        CollectionAssert.AreEqual(new[] { text, cta, image },
            pilot.Fields.All.Take(3).Select(f => bool.Parse(f.Value)).ToArray());
        Assert.AreEqual(CreatePostScenario.DefaultText, pilot.Fields.Get("text"));
        Assert.AreEqual("Open", pilot.Fields.Get("ctaLabel"));
        Assert.AreEqual("https://octopuscommunity.com/preset", pilot.Fields.Get("ctaUrl"));
        Assert.AreEqual("auto", pilot.Fields.Get("groupId"));
        Assert.IsEmpty(_sdk.Calls);
        _sdk.Groups.Add(new OctopusGroup { Id = "first", Name = "First" });
        _sdk.Groups.Add(new OctopusGroup { Id = "general", Name = "General" });
        pilot.Presets[index].Run(pilot.Fields);
        Assert.AreEqual("OpenCreatePost", _sdk.Last.Method);
        var prefill = _sdk.LastPrefilledPost;
        Assert.AreEqual(text ? CreatePostScenario.DefaultText : null, prefill.Text);
        Assert.AreEqual(cta ? "Open" : null, prefill.CtaLabel);
        Assert.AreEqual(cta ? "https://octopuscommunity.com/preset" : null, prefill.CtaUrl);
        Assert.AreEqual(image ? "/sample-cache/scenario-share.png" : null, prefill.ImagePath);
        Assert.AreEqual(image, prefill.SignBridgeShare != null);
        Assert.AreEqual("general", prefill.TopicId);
        StringAssert.Contains("Publishing requires a connected member", pilot.Result);
    }

    [TestCase("none", null)]
    [TestCase("chosen-group", "chosen-group")]
    public void CreatePostCanChooseAGroupOrLetTheMemberChoose(string field, string expected)
    {
        var pilot = Pilot<CreatePostScenario>();
        pilot.Presets[0].Fill(pilot.Fields);
        pilot.Fields.Set("groupId", field);
        pilot.RunCustom();
        Assert.AreEqual(expected, _sdk.LastPrefilledPost.TopicId);
        Assert.IsFalse(_sdk.Methods.Contains("FetchGroups"));
    }

    [Test]
    public void CreatePostValidatesBeforeOpeningAndReportsImageFailures()
    {
        var pilot = Pilot<CreatePostScenario>();
        pilot.Presets[1].Fill(pilot.Fields);
        pilot.Fields.Set("text", "short");
        pilot.RunCustom();
        StringAssert.Contains("text must contain", pilot.Result);
        pilot.Presets[1].Fill(pilot.Fields);
        pilot.Fields.Set("ctaLabel", "");
        pilot.RunCustom();
        StringAssert.Contains("CTA needs", pilot.Result);
        _sdk.ThrowShareImage = true;
        Tap(pilot, 2);
        StringAssert.Contains("Bundled image unavailable", pilot.Result);
        Assert.IsFalse(_sdk.Methods.Contains("OpenCreatePost"));
    }

    [Test]
    public void CreatePostBundledImageIsAPngPayload()
    {
        var path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources/ScenarioShareImage.bytes");
        var bytes = System.IO.File.ReadAllBytes(path);
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8));
    }

    [Test]
    public void AGroupStreamUpdateDoesNotCompleteAnInFlightFetch()
    {
        var pilot = Pilot<GroupsScenario>();
        _sdk.DeferGroups = true;
        Tap(pilot, 0);
        Assert.IsTrue(pilot.IsRunning);
        _sdk.EmitGroups(new List<OctopusGroup>());
        Assert.IsTrue(pilot.IsRunning);
        _sdk.PendingGroups(new List<OctopusGroup>());
        Assert.IsFalse(pilot.IsRunning);
    }

    [Test]
    public void ContentOverrideSummarySurvivesANewPilotAndClearRestoresBackend()
    {
        var pilot = Pilot<ContentOptionsScenario>();
        Tap(pilot, 1);
        var next = Pilot<ContentOptionsScenario>();
        StringAssert.Contains("post.enablePictures=False", next.ParameterNotice);
        Tap(next, 6);
        StringAssert.Contains("none (backend default)", next.ParameterNotice);
    }

    [Test]
    public void EveryRecordedSdkActionWasAnnounced()
    {
        var log = new ApiLog();
        OctopusSampleLog.Current = log;
        foreach (var id in new[] { "groups", "syncFollowGroups", "groupAccessDenied", "communityAccess", "contentOptions", "reactions", "bridge", "createPost" })
        {
            var pilot = (CommunityScenarioPilot)OctopusScenarioPilots.Create(id);
            _pilots.Add(pilot);
            _sdk.Groups = new List<OctopusGroup> { new OctopusGroup { Id = "a", CanChangeFollowStatus = true } };
            foreach (var preset in pilot.Presets)
            {
                _sdk.Clear();
                log.Methods.Clear();
                preset.Fill(pilot.Fields);
                preset.Run(pilot.Fields);
                foreach (var method in _sdk.ScenarioMethods)
                {
                    if (method == "PrepareBundledShareImage") continue; // Asset I/O, not SDK API.
                    var symbol = method == "GroupAccessDenied.add" ? "OnGroupAccessDenied +=" :
                        method == "GroupAccessDenied.remove" ? "OnGroupAccessDenied -=" : method;
                    CollectionAssert.Contains(log.Methods, "OctopusSDK." + symbol, preset.TestId + ": " + method);
                }
            }
        }
    }

    [TestCase("groups")]
    [TestCase("communityAccess")]
    [TestCase("groupAccessDenied")]
    public void NativeEventsDoNotRetainAnAbandonedPilot(string id)
    {
        var weak = AbandonObservedPilot(id);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.IsFalse(weak.IsAlive, "The SDK event retained the pilot and its view.");
        _sdk.EmitGroups(new List<OctopusGroup>());
        _sdk.EmitCommunityAccess(true);
        _sdk.EmitGroupAccessDenied("locked");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference AbandonObservedPilot(string id)
    {
        var pilot = OctopusScenarioPilots.Create(id);
        Tap(pilot, 0);
        return new WeakReference(pilot);
    }

    [Test]
    public void LiveSignerFailsWithoutConfigurationAndForwardsTheFingerprintToTheHost()
    {
        var log = new ApiLog();
        OctopusSampleLog.Current = log;
        var live = new OctopusLiveScenarioSdk();
        var task = live.SignBridgeShare("sample-fingerprint");
        Assert.IsTrue(task.IsFaulted, "Logging the refusal must not turn signing into a success.");
        var error = Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
        CollectionAssert.AreEqual(new[] { "Bridge signing refused" }, log.StateHeadlines);
        CollectionAssert.AreEqual(new[] { error.Message }, log.StateDetails);
        StringAssert.Contains("fill ssoTokenSecret", log.StateDetails[0]);
        StringAssert.Contains("OctopusScenarioSdk.BridgeShareSigner", log.StateDetails[0]);
        string received = null;
        OctopusScenarioSdk.BridgeShareSigner = fingerprint =>
        {
            received = fingerprint;
            return System.Threading.Tasks.Task.FromResult<string>(null);
        };
        try
        {
            Assert.IsNull(live.SignBridgeShare("sample-fingerprint").GetAwaiter().GetResult());
            Assert.AreEqual("sample-fingerprint", received);
        }
        finally { OctopusScenarioSdk.BridgeShareSigner = null; }
    }

    [Test]
    public void LiveSignerUsesTheInitializedDemoSecretOffThePlayerLoop()
    {
        _sdk.Profile = ProfileWithSigningSecret("unit-test-only-secret");
        Tap(Pilot<BridgeScenario>(), 0);
        var live = new OctopusLiveScenarioSdk();
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = System.Threading.Tasks.Task.Run(() => live.SignBridgeShare("sample-fingerprint"))
            .GetAwaiter().GetResult();
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        AssertBridgeToken(token, "unit-test-only-secret", before, after);

        // A host signer wins even after demo initialisation, and survives an SDK swap.
        OctopusScenarioSdk.BridgeShareSigner = fingerprint =>
            System.Threading.Tasks.Task.FromResult("host-result:" + fingerprint);
        try
        {
            Assert.AreEqual("host-result:sample-fingerprint", live.SignBridgeShare("sample-fingerprint").Result);
            OctopusScenarioSdk.Use(_sdk);
            Assert.AreEqual("host-result:sample-fingerprint", live.SignBridgeShare("sample-fingerprint").Result);
        }
        finally { OctopusScenarioSdk.BridgeShareSigner = null; }
        Assert.Throws<InvalidOperationException>(() => live.SignBridgeShare("sample-fingerprint").GetAwaiter().GetResult());
    }

    [Test]
    public void LiveSignerFollowsCommunitySwitchAndForgetsTheSecretOnStop()
    {
        _sdk.Profile = ProfileWithSigningSecret("unit-test-only-secret");
        Tap(Pilot<BridgeScenario>(), 0);
        // These lifecycle hooks are internal to the sample assembly; invoke them without widening its API.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        typeof(OctopusScenarioSdk).GetMethod("CommunitySwitched", flags).Invoke(null,
            new object[] { ProfileWithSigningSecret("another-unit-test-secret") });
        var live = new OctopusLiveScenarioSdk();
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = live.SignBridgeShare("sample-fingerprint").GetAwaiter().GetResult();
        AssertBridgeToken(token, "another-unit-test-secret", before, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        typeof(OctopusScenarioSdk).GetMethod("LifecycleStopped", flags).Invoke(null, null);
        Assert.Throws<InvalidOperationException>(() => live.SignBridgeShare("sample-fingerprint").GetAwaiter().GetResult());
    }

    private static OctopusExampleConfig.ExampleProfile ProfileWithSigningSecret(string secret)
    {
        var config = UnityEngine.ScriptableObject.CreateInstance<OctopusExampleConfig>();
        try
        {
            config.ssoTokenSecret = secret;
            var profile = config.Default;
            profile.apiKey = "not-a-real-key";
            return profile;
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    private static void AssertBridgeToken(string token, string secret, long before, long after)
    {
        var json = OctopusSampleTokenProviderTests.Decode(token.Split('.')[1]);
        var payload = UnityEngine.JsonUtility.FromJson<BridgeClaims>(json);
        Assert.AreEqual("sample-fingerprint", payload.bridge_fingerprint);
        Assert.That(payload.exp, Is.InRange(before + 3600, after + 3600));
        var expected = new OctopusSampleTokenProvider(null,
            () => DateTimeOffset.FromUnixTimeSeconds(payload.exp - 3600), secret)
            .GetBridgeSignature("sample-fingerprint");
        Assert.AreEqual(expected, token);
    }

    [Serializable]
    private class BridgeClaims
    {
        public string bridge_fingerprint = null;
        public long exp = 0;
    }

    private sealed class ApiLog : IOctopusSampleLog
    {
        public readonly List<string> Methods = new List<string>();
        public readonly List<string> StateHeadlines = new List<string>();
        public readonly List<string> StateDetails = new List<string>();
        public void LogApiCall(string method, string detail = null) { Methods.Add(method); }
        public void LogStateChange(string headline, string detail = null)
        {
            StateHeadlines.Add(headline);
            StateDetails.Add(detail);
        }
    }

}
