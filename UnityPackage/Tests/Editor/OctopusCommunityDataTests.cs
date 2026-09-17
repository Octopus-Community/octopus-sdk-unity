using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class OctopusCommunityDataTests
{
    private static readonly OctopusCommunityMemberId Member = OctopusCommunityMemberId.FromProfileId("member");
    private GameObject _object;
    private OctopusSDK.OctopusChannel _channel;
    private OctopusMainThread _drainer;
    private readonly List<OctopusCommunityData> _updates = new List<OctopusCommunityData>();

    [SetUp]
    public void SetUp()
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        _object = new GameObject("CommunityDataTests");
        _channel = _object.AddComponent<OctopusSDK.OctopusChannel>();
        _drainer = _object.AddComponent<OctopusMainThread>();
        Drain();
        _updates.Clear();
        OctopusSDK.OnCommunityDataChanged += OnChanged;
    }

    [TearDown]
    public void TearDown()
    {
        OctopusSDK.OnCommunityDataChanged -= OnChanged;
        OctopusSDK.Mock.Reset();
        Drain();
        UnityEngine.Object.DestroyImmediate(_object);
    }

    private void OnChanged(OctopusCommunityData data) { _updates.Add(data); }
    private void Drain()
    {
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_drainer, null);
    }

    [TestCase(null)]
    [TestCase("")]
    public void MemberId_FactoriesRejectMissingIds(string value)
    {
        Assert.Throws<ArgumentException>(() => OctopusCommunityMemberId.FromProfileId(value));
        Assert.Throws<ArgumentException>(() => OctopusCommunityMemberId.FromClientUserId(value));
    }

    [Test]
    public void MemberId_FactoriesSelectExactlyOneUnmodifiedId()
    {
        var profile = OctopusCommunityMemberId.FromProfileId("a\"b");
        Assert.AreEqual("a\"b", profile.ProfileId);
        Assert.IsNull(profile.ClientUserId);
        var client = OctopusCommunityMemberId.FromClientUserId("host-user");
        Assert.AreEqual("host-user", client.ClientUserId);
        Assert.IsNull(client.ProfileId);
    }

    [Test]
    public void MemberId_NullIsRejectedSynchronouslyWithoutReplacingObservation()
    {
        OctopusSDK.StartObservingCommunityData(Member);
        Assert.Throws<ArgumentNullException>(() => OctopusSDK.FetchCommunityData(null, data => Assert.Fail(), e => Assert.Fail()));
        Assert.Throws<ArgumentNullException>(() => OctopusSDK.StartObservingCommunityData(null));
        Assert.Throws<ArgumentNullException>(() => OctopusSDK.Mock.SetCommunityData(null, null));
        Drain();
        Assert.AreEqual(1, _updates.Count);
        OctopusSDK.Mock.SetCommunityData(Member, new OctopusCommunityData("member"));
        Drain();
        Assert.AreEqual("member", _updates[1].ProfileId);
    }

    [Test]
    public void Mock_KeysByIdKindAndValueAndOnlyEmitsForObservedMember()
    {
        var profile = OctopusCommunityMemberId.FromProfileId("same-id");
        var client = OctopusCommunityMemberId.FromClientUserId("same-id");
        var first = new OctopusCommunityData("same-id", 1);
        var second = new OctopusCommunityData("resolved-profile", 2);
        OctopusSDK.Mock.SetCommunityData(profile, first);
        OctopusSDK.Mock.SetCommunityData(client, second);
        int replies = 0;
        OctopusSDK.FetchCommunityData(OctopusCommunityMemberId.FromProfileId("same-id"), data => { Assert.AreSame(first, data); replies++; }, e => Assert.Fail(e));
        OctopusSDK.FetchCommunityData(OctopusCommunityMemberId.FromClientUserId("same-id"), data => { Assert.AreSame(second, data); replies++; }, e => Assert.Fail(e));
        OctopusSDK.FetchCommunityData(OctopusCommunityMemberId.FromProfileId("unknown"), data => { Assert.IsNull(data); replies++; }, e => Assert.Fail(e));
        OctopusSDK.StartObservingCommunityData(client);
        Drain();
        Assert.AreEqual(3, replies);
        Assert.AreSame(second, _updates[0]);
        OctopusSDK.Mock.SetCommunityData(profile, null);
        Drain();
        Assert.AreEqual(1, _updates.Count);
        OctopusSDK.Mock.SetCommunityData(OctopusCommunityMemberId.FromClientUserId("same-id"), null);
        Drain();
        Assert.IsNull(_updates[1]);
    }

    [Test]
    public void Observation_ReplacementDropsQueuedOldMemberAndReplaysNewMember()
    {
        var other = OctopusCommunityMemberId.FromClientUserId("other");
        var next = new OctopusCommunityData("other-profile", 3);
        OctopusSDK.Mock.SetCommunityData(other, next);
        OctopusSDK.StartObservingCommunityData(Member);
        _channel.OnCommunityDataChanged("{\"profileId\":\"member\"}");
        OctopusSDK.StartObservingCommunityData(other);
        OctopusSDK.Mock.SetCommunityData(Member, new OctopusCommunityData("member", 4));
        Drain();
        Assert.AreEqual(1, _updates.Count);
        Assert.AreSame(next, _updates[0]);
        OctopusSDK.StartObservingCommunityData(OctopusCommunityMemberId.FromProfileId("unknown"));
        Drain();
        Assert.IsNull(_updates[1]);
        OctopusSDK.Mock.SetCommunityData(other, null);
        Drain();
        Assert.AreEqual(2, _updates.Count);
    }

    [Test]
    public void Parsing_PreservesWireNamesNestedNumbersAndEscapedProfileId()
    {
        var data = OctopusJson.CommunityDataFromJson("{\"profileId\":\"member\\u0031\\\"x\",\"messageCount\":23,\"gamification\":{\"level\":4,\"score\":123}}");
        Assert.AreEqual("member1\"x", data.ProfileId);
        Assert.AreEqual(23, data.MessageCount);
        Assert.AreEqual(4, data.Gamification.Level);
        Assert.AreEqual(123, data.Gamification.Score);
    }

    [TestCase("{}")]
    [TestCase("{\"profileId\":null,\"messageCount\":null,\"gamification\":null}")]
    [TestCase("{\"profileId\":42,\"messageCount\":\"12\",\"gamification\":[]}")]
    [TestCase("{\"messageCount\":2147483648,\"gamification\":false}")]
    public void Parsing_MissingOrWrongFieldsRemainUnavailable(string json)
    {
        var data = OctopusJson.CommunityDataFromJson(json);
        Assert.AreEqual("", data.ProfileId);
        Assert.IsNull(data.MessageCount);
        Assert.IsNull(data.Gamification);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" null ")]
    [TestCase("[]")]
    [TestCase("garbage")]
    [TestCase("{\"profileId\":\"\\uZZZZ\"}")]
    public void Parsing_AbsentOrInvalidSnapshotIsNull(string json)
    {
        Assert.IsNull(OctopusJson.CommunityDataFromJson(json));
    }

    [Test]
    public void Parsing_ZeroIsDifferentFromUnavailableAndScoreIsNullable()
    {
        var data = OctopusJson.CommunityDataFromJson("{\"messageCount\":0,\"gamification\":{\"level\":0,\"score\":null}}");
        Assert.AreEqual(0, data.MessageCount);
        Assert.AreEqual(0, data.Gamification.Level);
        Assert.IsNull(data.Gamification.Score);
        data = OctopusJson.CommunityDataFromJson("{\"messageCount\":1.5,\"gamification\":{\"level\":\"4\",\"score\":true}}");
        Assert.IsNull(data.MessageCount);
        Assert.AreEqual(0, data.Gamification.Level);
        Assert.IsNull(data.Gamification.Score);
    }

    [Test]
    public void Dispatch_BackgroundEventsAreQueuedAndNullClearsTheSnapshot()
    {
        OctopusSDK.StartObservingCommunityData(Member);
        Drain();
        _updates.Clear();
        Task.Run(() => OctopusSDK.ReceiveCommunityData("{\"profileId\":\"member\",\"messageCount\":7}")).Wait();
        Assert.AreEqual(0, _updates.Count);
        Drain();
        Assert.AreEqual("member", _updates[0].ProfileId);
        _channel.OnCommunityDataChanged("null");
        Drain();
        Assert.AreEqual(2, _updates.Count);
        Assert.IsNull(_updates[1]);
    }

    [Test]
    public void Fetch_CorrelatesConcurrentResponsesOnceAndIgnoresMalformedPayloads()
    {
        int successes = 0, errors = 0;
        int first = OctopusSDK.RegisterCommunityDataRequest(data => { Assert.IsNull(data); successes++; }, e => Assert.Fail(e));
        int second = OctopusSDK.RegisterCommunityDataRequest(data => Assert.Fail(), e => { Assert.AreEqual("offline\nretry", e); errors++; });
        _channel.OnFetchCommunityDataResult(null);
        _channel.OnFetchCommunityDataError("bad\nerror");
        _channel.OnFetchCommunityDataError(second + "\noffline\nretry");
        _channel.OnFetchCommunityDataResult(first + "\nnull");
        _channel.OnFetchCommunityDataResult(first + "\n{}");
        Assert.AreEqual(0, successes + errors);
        Drain();
        Assert.AreEqual(1, successes);
        Assert.AreEqual(1, errors);
    }

    [Test]
    public void Mock_FetchAndObservationWorkWhenRecordingIsDisabled()
    {
        OctopusSDK.Mock.Enabled = false;
        var expected = new OctopusCommunityData("member", 12, new OctopusGamification(2));
        OctopusSDK.Mock.SetCommunityData(Member, expected);
        OctopusSDK.StartObservingCommunityData(Member);
        OctopusSDK.StartObservingCommunityData(Member);
        OctopusCommunityData actual = null;
        OctopusSDK.FetchCommunityData(Member, data => actual = data, e => Assert.Fail(e));
        Drain();
        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, _updates.Count);
        Assert.AreSame(expected, _updates[0]);
        OctopusSDK.Mock.SetCommunityData(Member, null);
        Drain();
        Assert.IsNull(_updates[1]);
        bool completed = false;
        OctopusSDK.FetchCommunityData(Member, data => { Assert.IsNull(data); completed = true; }, e => Assert.Fail(e));
        Drain();
        Assert.IsTrue(completed);
    }

    [Test]
    public void StopObservation_DropsQueuedEventsButKeepsFetchesAndStoredData()
    {
        var data = new OctopusCommunityData("member");
        OctopusSDK.StartObservingCommunityData(Member);
        OctopusSDK.Mock.SetCommunityData(Member, data);
        bool completed = false;
        OctopusSDK.FetchCommunityData(Member, result => { Assert.AreSame(data, result); completed = true; }, e => Assert.Fail(e));
        OctopusSDK.StopObservingCommunityData();
        OctopusSDK.StopObservingCommunityData();
        Drain();
        Assert.AreEqual(0, _updates.Count);
        Assert.IsTrue(completed);
        OctopusSDK.StartObservingCommunityData(Member);
        Drain();
        Assert.AreSame(data, _updates[0]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Lifecycle_ResetAndStopEndObservationAndCancelPendingFetches(bool stop)
    {
        OctopusSDK.StartObservingCommunityData(Member);
        OctopusSDK.Mock.SetCommunityData(Member, new OctopusCommunityData("old"));
        int errors = 0;
        OctopusSDK.FetchCommunityData(Member, data => Assert.Fail("Stale result"), e => errors++);
        if (stop) OctopusSDK.Stop(); else OctopusSDK.Reset();
        OctopusSDK.Mock.SetCommunityData(Member, new OctopusCommunityData("new"));
        Drain();
        Assert.AreEqual(0, _updates.Count);
        Assert.AreEqual(1, errors);
    }

    [Test]
    public void Lifecycle_RebindClearsOldDataButRetainsObservationAndSubscribers()
    {
        OctopusSDK.StartObservingCommunityData(Member);
        OctopusSDK.Mock.SetCommunityData(Member, new OctopusCommunityData("old"));
        OctopusSDK.ResetCommunityDataState(false);
        Drain();
        Assert.AreEqual(1, _updates.Count);
        Assert.IsNull(_updates[0]);
        OctopusSDK.Mock.SetCommunityData(Member, new OctopusCommunityData("new"));
        Drain();
        Assert.AreEqual("new", _updates[1].ProfileId);
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.SetCommunityData(Member, new OctopusCommunityData("ignored"));
        Drain();
        Assert.AreEqual(2, _updates.Count);
    }
}
