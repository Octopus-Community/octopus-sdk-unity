using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class OctopusGroupFollowingTests
{
    private GameObject _object;
    private OctopusMainThread _drainer;
    private OctopusSDK.OctopusChannel _channel;

    [SetUp]
    public void SetUp()
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        _object = new GameObject("GroupFollowingTest");
        _drainer = _object.AddComponent<OctopusMainThread>();
        _channel = _object.AddComponent<OctopusSDK.OctopusChannel>();
        Drain();
    }

    [TearDown]
    public void TearDown()
    {
        Drain();
        UnityEngine.Object.DestroyImmediate(_object);
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
    }

    private void Drain()
    {
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_drainer, null);
    }

    [TestCase("missingGroup", OctopusGroupFollowUnfollowErrorCode.MissingGroup)]
    [TestCase("unfollowableGroup", OctopusGroupFollowUnfollowErrorCode.UnfollowableGroup)]
    [TestCase("groupAlreadyFollowed", OctopusGroupFollowUnfollowErrorCode.GroupAlreadyFollowed)]
    [TestCase("groupAlreadyUnfollowed", OctopusGroupFollowUnfollowErrorCode.GroupAlreadyUnfollowed)]
    [TestCase("lastFollowedGroup", OctopusGroupFollowUnfollowErrorCode.LastFollowedGroup)]
    [TestCase("unknown", OctopusGroupFollowUnfollowErrorCode.Unknown)]
    [TestCase("futureError", OctopusGroupFollowUnfollowErrorCode.Unknown)]
    public void ErrorParsing_PreservesMessageAndMapsCode(string wire, OctopusGroupFollowUnfollowErrorCode expected)
    {
        var error = OctopusGroupFollowingParsing.ErrorFromJson("{\"type\":\"" + wire + "\",\"message\":\"Line 1\\n\\\"Line 2\\\"\"}");
        Assert.AreEqual(expected, error.Code);
        Assert.AreEqual("Line 1\n\"Line 2\"", error.Message);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("{}")]
    [TestCase("invalid")]
    [TestCase("{\"type\":\"\\uZZZZ\"}")]
    public void ErrorParsing_InvalidPayloadFallsBack(string json)
    {
        var error = OctopusGroupFollowingParsing.ErrorFromJson(json);
        Assert.AreEqual(OctopusGroupFollowUnfollowErrorCode.Unknown, error.Code);
        Assert.IsNotEmpty(error.Message);
    }

    [Test]
    public void GroupParsing_KeepsLegacyFieldsAndDefaultsNewPermissions()
    {
        var groups = OctopusSyncFollowGroupParsing.GroupsFromJson(
            "[{\"id\":\"g\",\"name\":\"Group\",\"isFollowed\":true,\"canChangeFollowStatus\":false}," +
            "{\"canAccess\":false,\"canCreateChildren\":false},{\"canAccess\":true,\"canCreateChildren\":true}," +
            "{\"canAccess\":null,\"canCreateChildren\":17}]");
        Assert.AreEqual("g", groups[0].Id);
        Assert.AreEqual("Group", groups[0].Name);
        Assert.IsTrue(groups[0].IsFollowed);
        Assert.IsFalse(groups[0].CanChangeFollowStatus);
        Assert.IsTrue(groups[0].CanAccess);
        Assert.IsTrue(groups[0].CanCreateChildren);
        Assert.IsFalse(groups[1].CanAccess);
        Assert.IsFalse(groups[1].CanCreateChildren);
        Assert.IsTrue(groups[2].CanAccess);
        Assert.IsTrue(groups[2].CanCreateChildren);
        Assert.IsTrue(groups[3].CanAccess);
        Assert.IsTrue(groups[3].CanCreateChildren);
    }

    [Test]
    public void NativeResults_CorrelateOutOfOrderAndCompleteOnlyOnceOnMainThread()
    {
        var calls = new List<string>();
        int threadId = Thread.CurrentThread.ManagedThreadId;
        int callbackThread = -1;
        int first = OctopusSDK.RegisterGroupFollowingCallbacks(() => calls.Add("first"), e => calls.Add("wrong"));
        int second = OctopusSDK.RegisterGroupFollowingCallbacks(() => calls.Add("wrong"), e =>
        {
            callbackThread = Thread.CurrentThread.ManagedThreadId;
            calls.Add(e.Code.ToString());
        });
        Task.Run(() =>
        {
            OctopusSDK.ReceiveGroupFollowingResult(second + "\n{\"type\":\"missingGroup\"}", true);
            OctopusSDK.ReceiveGroupFollowingResult(first + "\n", false);
        }).Wait();
        _channel.OnGroupFollowUnfollowResult(first + "\n");
        _channel.OnGroupFollowUnfollowError(second + "\n{}");
        _channel.OnGroupFollowUnfollowResult(null);
        _channel.OnGroupFollowUnfollowError("bad-id\n{}");
        Assert.IsEmpty(calls);
        Drain();
        CollectionAssert.AreEqual(new[] { "MissingGroup", "first" }, calls);
        Assert.AreEqual(threadId, callbackThread);
    }

    [Test]
    public void AccessDenied_PadAndMockPreserveIdAndAllowUnsubscription()
    {
        var ids = new List<string>();
        Action<string> handler = ids.Add;
        OctopusSDK.OnGroupAccessDenied += handler;
        try
        {
            _channel.OnGroupAccessDenied("group-雪\n1");
            OctopusSDK.Mock.Enabled = false;
            OctopusSDK.Mock.EmitGroupAccessDenied("locked");
            _channel.OnGroupAccessDenied("");
            Assert.IsEmpty(ids);
            Drain();
            CollectionAssert.AreEqual(new[] { "group-雪\n1", "locked" }, ids);
        }
        finally { OctopusSDK.OnGroupAccessDenied -= handler; }
        OctopusSDK.Mock.EmitGroupAccessDenied("after-unsubscribe");
        Drain();
        Assert.AreEqual(2, ids.Count);
    }

    private static OctopusGroup Group(string id, bool followed = false, bool changeable = true)
    {
        return new OctopusGroup { Id = id, Name = id, IsFollowed = followed,
            CanChangeFollowStatus = changeable, CanAccess = true, CanCreateChildren = false };
    }

    [Test]
    public void Mock_FollowAndUnfollowUpdateFetchAndPublishSnapshots()
    {
        var seed = new List<OctopusGroup> { Group("g") };
        OctopusSDK.Mock.SetGroups(seed);
        var snapshots = new List<IList<OctopusGroup>>();
        Action<IList<OctopusGroup>> handler = snapshots.Add;
        int successes = 0;
        OctopusSDK.OnGroupsChanged += handler;
        try
        {
            OctopusSDK.FollowGroup("g", () => successes++, e => Assert.Fail(e.Message));
            OctopusSDK.UnfollowGroup("g", () => successes++, e => Assert.Fail(e.Message));
            Assert.AreEqual(0, successes);
            Drain();
            Assert.AreEqual(2, successes);
            Assert.AreEqual(2, snapshots.Count);
            Assert.IsTrue(snapshots[0][0].IsFollowed);
            Assert.IsFalse(snapshots[1][0].IsFollowed);
            Assert.IsTrue(snapshots[0][0].CanAccess);
            Assert.IsFalse(snapshots[0][0].CanCreateChildren);
            Assert.IsFalse(seed[0].IsFollowed);
            OctopusSDK.FetchGroups(groups => Assert.IsFalse(groups[0].IsFollowed));
            Assert.AreEqual("g", OctopusSDK.Mock.LastCall("FollowGroup").Value.Args[0]);
            Assert.IsTrue(OctopusSDK.Mock.LastCall("UnfollowGroup").HasValue);
        }
        finally { OctopusSDK.OnGroupsChanged -= handler; }
    }

    [Test]
    public void Mock_ReturnsBusinessErrorsWithoutChangingState()
    {
        OctopusSDK.Mock.SetGroups(new[] { Group("followed", true), Group("unfollowed"), Group("fixed", true, false) });
        var codes = new List<OctopusGroupFollowUnfollowErrorCode>();
        Action<OctopusGroupFollowUnfollowError> error = e => codes.Add(e.Code);
        Action success = () => Assert.Fail("Unexpected success");
        OctopusSDK.FollowGroup("missing", success, error);
        OctopusSDK.FollowGroup("followed", success, error);
        OctopusSDK.UnfollowGroup("unfollowed", success, error);
        OctopusSDK.UnfollowGroup("fixed", success, error);
        Drain();
        CollectionAssert.AreEqual(new[] { OctopusGroupFollowUnfollowErrorCode.MissingGroup,
            OctopusGroupFollowUnfollowErrorCode.GroupAlreadyFollowed,
            OctopusGroupFollowUnfollowErrorCode.GroupAlreadyUnfollowed,
            OctopusGroupFollowUnfollowErrorCode.UnfollowableGroup }, codes);
        OctopusSDK.FetchGroups(groups =>
        {
            Assert.IsTrue(groups[0].IsFollowed);
            Assert.IsFalse(groups[1].IsFollowed);
            Assert.IsTrue(groups[2].IsFollowed);
        });
    }

    [Test]
    public void Mock_InjectedErrorIsConsumedAndDisabledMockStillCompletes()
    {
        OctopusSDK.Mock.SetGroups(new[] { Group("g", true) });
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.Mock.NextGroupFollowUnfollowError = new OctopusGroupFollowUnfollowError(
            OctopusGroupFollowUnfollowErrorCode.LastFollowedGroup, "Last group");
        OctopusGroupFollowUnfollowError received = null;
        int successes = 0;
        OctopusSDK.UnfollowGroup("g", () => successes++, e => received = e);
        OctopusSDK.UnfollowGroup("g", () => successes++, e => Assert.Fail(e.Message));
        Drain();
        Assert.AreEqual(OctopusGroupFollowUnfollowErrorCode.LastFollowedGroup, received.Code);
        Assert.AreEqual(1, successes);
        Assert.IsNull(OctopusSDK.Mock.NextGroupFollowUnfollowError);
        Assert.IsEmpty(OctopusSDK.Mock.Calls);
    }

    [Test]
    public void Mock_ResetClearsSessionStateAndInjectedError()
    {
        OctopusSDK.Mock.SetGroups(new[] { Group("g") });
        OctopusSDK.Mock.NextGroupFollowUnfollowError = new OctopusGroupFollowUnfollowError(
            OctopusGroupFollowUnfollowErrorCode.Unknown, "Injected");
        OctopusSDK.Mock.Reset();
        Assert.IsNull(OctopusSDK.Mock.NextGroupFollowUnfollowError);
        Assert.IsNull(OctopusSDK.MockBackend._groupFollowingGroups);
    }

    [TestCase(null)]
    [TestCase("")]
    public void EmptyId_CompletesWithTypedError(string id)
    {
        OctopusGroupFollowUnfollowError received = null;
        OctopusSDK.FollowGroup(id, () => Assert.Fail("Unexpected success"), e => received = e);
        Assert.IsNull(received);
        Drain();
        Assert.AreEqual(OctopusGroupFollowUnfollowErrorCode.MissingGroup, received.Code);
        OctopusSDK.UnfollowGroup(id, null, null);
        Drain();
    }
}
