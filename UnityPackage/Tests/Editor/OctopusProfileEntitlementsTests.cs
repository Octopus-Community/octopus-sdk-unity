using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class OctopusProfileEntitlementsTests
{
    private GameObject _object;
    private OctopusSDK.OctopusChannel _channel;
    private OctopusMainThread _drainer;

    [SetUp]
    public void SetUp()
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        _object = new GameObject("ProfileEntitlementsTests");
        _channel = _object.AddComponent<OctopusSDK.OctopusChannel>();
        _drainer = _object.AddComponent<OctopusMainThread>();
        Drain();
    }

    [TearDown]
    public void TearDown()
    {
        Drain();
        OctopusSDK.Mock.Reset();
        UnityEngine.Object.DestroyImmediate(_object);
    }

    private void Drain()
    {
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_drainer, null);
    }

    [Test]
    public void Profile_ParsesBothFieldsAndDeduplicatesOnlyStrings()
    {
        var profile = OctopusProfileParsing.FromJson("{\"entitlements\":[\"premium\",null,42,{},[\"nested\"],\"premium\",\"a,b\\\"c\"],\"clientUserId\":\"user\\u0031\"}");
        CollectionAssert.AreEquivalent(new[] { "premium", "a,b\"c" }, profile.Entitlements);
        Assert.AreEqual("user1", profile.ClientUserId);
    }

    [TestCase("{}")]
    [TestCase("{\"clientUserId\":null,\"entitlements\":null}")]
    [TestCase("{\"clientUserId\":123,\"entitlements\":\"premium\"}")]
    public void Profile_MissingOrWrongFieldsUseDefaults(string json)
    {
        var profile = OctopusProfileParsing.FromJson(json);
        Assert.IsNull(profile.ClientUserId);
        Assert.AreEqual(0, profile.Entitlements.Count);
    }

    [TestCase(null)]
    [TestCase("null")]
    [TestCase("garbage")]
    [TestCase("{\"clientUserId\":\"\\uZZZZ\"}")]
    public void Profile_NoSnapshotOrBadEscapeDoesNotThrow(string json)
    {
        Assert.IsNull(OctopusProfileParsing.FromJson(json));
    }

    [Test]
    public void Profile_CopiesInputAndPreservesEmptyClientId()
    {
        var input = new[] { "premium" };
        var profile = new OctopusProfile(input, "");
        input[0] = "modified";
        CollectionAssert.AreEqual(new[] { "premium" }, profile.Entitlements);
        Assert.AreEqual("", profile.ClientUserId);
    }

    [Test]
    public void Profile_BackgroundDeliveryUpdatesAccessorBeforeEventAndClearsOnNull()
    {
        int count = 0;
        Action<OctopusProfile> handler = profile => { Assert.AreSame(profile, OctopusSDK.CurrentProfile); count++; };
        OctopusSDK.OnProfileChanged += handler;
        try
        {
            Task.Run(() => OctopusSDK.ReceiveProfile("{\"clientUserId\":\"user1\",\"entitlements\":[]}")).Wait();
            Assert.AreEqual(0, count);
            Assert.IsNull(OctopusSDK.CurrentProfile);
            Drain();
            Assert.AreEqual("user1", OctopusSDK.CurrentProfile.ClientUserId);
            _channel.OnProfileChanged("null");
            Drain();
            Assert.AreEqual(2, count);
            Assert.IsNull(OctopusSDK.CurrentProfile);
        }
        finally { OctopusSDK.OnProfileChanged -= handler; }
    }

    [TestCase("noClientTokenProvider", OctopusRefreshEntitlementsErrorKind.NoClientTokenProvider)]
    [TestCase("userNotConnected", OctopusRefreshEntitlementsErrorKind.UserNotConnected)]
    [TestCase("noNetwork", OctopusRefreshEntitlementsErrorKind.NoNetwork)]
    [TestCase("userBanned", OctopusRefreshEntitlementsErrorKind.UserBanned)]
    [TestCase("serverError", OctopusRefreshEntitlementsErrorKind.ServerError)]
    [TestCase("futureError", OctopusRefreshEntitlementsErrorKind.ServerError)]
    public void Refresh_ParsesTypedErrorAndPreservesMessage(string wire, OctopusRefreshEntitlementsErrorKind expected)
    {
        var error = OctopusRefreshEntitlementsError.FromJson("{\"type\":\"" + wire + "\",\"message\":\"reason\\nnext line\"}");
        Assert.AreEqual(expected, error.Kind);
        Assert.AreEqual("reason\nnext line", error.Message);
    }

    [Test]
    public void Refresh_ConcurrentRequestsResolveOnceOutOfOrderAndIgnoreMalformedMessages()
    {
        int successes = 0, failures = 0;
        int first = OctopusSDK.RegisterEntitlementsRequest(() => successes++, e => Assert.Fail());
        int second = OctopusSDK.RegisterEntitlementsRequest(() => Assert.Fail(), e => { Assert.AreEqual(OctopusRefreshEntitlementsErrorKind.UserBanned, e.Kind); failures++; });
        _channel.OnRefreshEntitlementsResult(null);
        _channel.OnRefreshEntitlementsError("bad\n{}");
        _channel.OnRefreshEntitlementsError(second + "\n{\"type\":\"userBanned\"}");
        _channel.OnRefreshEntitlementsResult(first + "\n");
        _channel.OnRefreshEntitlementsResult(first + "\n");
        Assert.AreEqual(0, successes + failures);
        Drain();
        Assert.AreEqual(1, successes);
        Assert.AreEqual(1, failures);
    }

    [Test]
    public void Mock_ProfileDriverWorksWhenDisabledAndResetDropsQueuedProfile()
    {
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.Mock.EmitProfileChanged(new OctopusProfile(clientUserId: "user1"));
        Drain();
        Assert.AreEqual("user1", OctopusSDK.CurrentProfile.ClientUserId);
        OctopusSDK.Mock.EmitProfileChanged(new OctopusProfile(clientUserId: "stale"));
        OctopusSDK.Mock.Reset();
        Drain();
        Assert.IsNull(OctopusSDK.CurrentProfile);
    }

    [Test]
    public void Mock_RefreshSimulatesSuccessAndTypedFailuresAndAlwaysCompletes()
    {
        OctopusSDK.MockBackend.InitializeProfileMock(ConnectionMode.SSO());
        OctopusSDK.Mock.EmitProfileChanged(new OctopusProfile(new[] { "premium" }, "user1"));
        Drain();
        int success = 0;
        OctopusSDK.RefreshEntitlements(() => success++, e => Assert.Fail(e.Message));
        Drain();
        Assert.AreEqual(1, success);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("RefreshEntitlements").HasValue);
        var expected = new OctopusRefreshEntitlementsError(OctopusRefreshEntitlementsErrorKind.NoNetwork, "offline");
        OctopusSDK.Mock.RefreshEntitlementsError = expected;
        OctopusSDK.Mock.Enabled = false;
        OctopusRefreshEntitlementsError actual = null;
        OctopusSDK.RefreshEntitlements(() => Assert.Fail(), e => actual = e);
        Drain();
        Assert.AreSame(expected, actual);
    }

    [Test]
    public void Mock_ConnectAndDisconnectDriveProfileObservation()
    {
        OctopusSDK.MockBackend.InitializeProfileMock(ConnectionMode.SSO());
        OctopusSDK.ConnectUser("user1", null, null, null, null).GetAwaiter().GetResult();
        Drain();
        Assert.AreEqual("user1", OctopusSDK.CurrentProfile.ClientUserId);
        OctopusSDK.DisconnectUser().GetAwaiter().GetResult();
        Drain();
        Assert.IsNull(OctopusSDK.CurrentProfile);
    }

    [Test]
    public void Mock_RefreshRejectsOctopusAuthAndDisconnectedSso()
    {
        OctopusRefreshEntitlementsError error = null;
        OctopusSDK.MockBackend.InitializeProfileMock(ConnectionMode.OctopusAuth());
        OctopusSDK.RefreshEntitlements(null, e => error = e);
        Drain();
        Assert.AreEqual(OctopusRefreshEntitlementsErrorKind.NoClientTokenProvider, error.Kind);
        OctopusSDK.MockBackend.InitializeProfileMock(ConnectionMode.SSO());
        OctopusSDK.RefreshEntitlements(null, e => error = e);
        Drain();
        Assert.AreEqual(OctopusRefreshEntitlementsErrorKind.UserNotConnected, error.Kind);
    }
}
