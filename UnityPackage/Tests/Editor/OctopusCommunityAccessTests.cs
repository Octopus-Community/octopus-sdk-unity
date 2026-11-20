using System;
using NUnit.Framework;
using UnityEngine;

public class OctopusCommunityAccessTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void OverrideCommunityAccess_Records_Completes_AndUpdatesReactiveValue()
    {
        bool completed = false;
        bool? eventValue = null;
        Action<bool> h = v => eventValue = v;
        OctopusSDK.OnHasAccessToCommunityChanged += h;
        try
        {
            OctopusSDK.OverrideCommunityAccess(true, onCompleted: () => completed = true);
        }
        finally { OctopusSDK.OnHasAccessToCommunityChanged -= h; }

        Assert.AreEqual(true, OctopusSDK.Mock.LastCall("OverrideCommunityAccess").Value.Args[0]);
        Assert.IsTrue(completed, "onCompleted should fire in the mock");
        // Overriding access must update the reactive value + fire the change event (mirrors device).
        Assert.IsTrue(OctopusSDK.HasAccessToCommunity);
        Assert.AreEqual(true, eventValue);
    }

    [Test]
    public void OverrideCommunityAccess_False_ClearsAccess()
    {
        OctopusSDK.OverrideCommunityAccess(false);
        Assert.IsFalse(OctopusSDK.HasAccessToCommunity);
    }

    [Test]
    public void EmitHasAccessToCommunity_RaisesEventAndCachesValue()
    {
        bool? seen = null;
        Action<bool> h = v => seen = v;
        OctopusSDK.OnHasAccessToCommunityChanged += h;
        try { OctopusSDK.Mock.EmitHasAccessToCommunity(true); }
        finally { OctopusSDK.OnHasAccessToCommunityChanged -= h; }
        Assert.AreEqual(true, seen);
        Assert.IsTrue(OctopusSDK.HasAccessToCommunity);
    }

    [Test]
    public void Channel_OnHasAccessToCommunity_ParsesWireValueAndRaisesEvent()
    {
        bool? seen = null;
        Action<bool> handler = v => seen = v;
        OctopusSDK.OnHasAccessToCommunityChanged += handler;
        GameObject go = null;
        try
        {
            go = new GameObject("ch");
            var channel = go.AddComponent<OctopusSDK.OctopusChannel>();

            channel.OnHasAccessToCommunity("true");
            Assert.AreEqual(true, seen);
            Assert.IsTrue(OctopusSDK.HasAccessToCommunity);

            channel.OnHasAccessToCommunity("false");
            Assert.AreEqual(false, seen);
            Assert.IsFalse(OctopusSDK.HasAccessToCommunity);
        }
        finally
        {
            OctopusSDK.OnHasAccessToCommunityChanged -= handler;
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [TestCase("NOT_IN_COHORT", OctopusCommunityAccessDeniedReason.NotInCohort)]
    [TestCase("COMMUNITY_TEMPORARILY_DISABLED", OctopusCommunityAccessDeniedReason.CommunityTemporarilyDisabled)]
    [TestCase("COMMUNITY_CLOSED", OctopusCommunityAccessDeniedReason.CommunityClosed)]
    [TestCase("MEMBER_EXCLUDED", OctopusCommunityAccessDeniedReason.MemberExcluded)]
    [TestCase("ACCOUNT_BANNED", OctopusCommunityAccessDeniedReason.AccountBanned)]
    [TestCase("ACCOUNT_DELETED", OctopusCommunityAccessDeniedReason.AccountDeleted)]
    [TestCase("UNKNOWN", OctopusCommunityAccessDeniedReason.Unknown)]
    [TestCase("ACCOUNT_SUSPENDED", OctopusCommunityAccessDeniedReason.Unknown)]
    [TestCase("AccountBanned", OctopusCommunityAccessDeniedReason.Unknown)]
    public void ParseCommunityAccessDeniedReason_MapsNativeNames(string wire, OctopusCommunityAccessDeniedReason expected)
    {
        Assert.AreEqual(expected, OctopusSDK.ParseCommunityAccessDeniedReason(wire));
    }

    [Test]
    public void ParseCommunityAccessDeniedReason_EmptyOrNull_IsNoReason()
    {
        Assert.IsNull(OctopusSDK.ParseCommunityAccessDeniedReason(""));
        Assert.IsNull(OctopusSDK.ParseCommunityAccessDeniedReason(null));
    }

    [Test]
    public void Channel_OnCommunityAccessDeniedReason_ParsesWireValueAndRaisesEvent()
    {
        int calls = 0;
        OctopusCommunityAccessDeniedReason? seen = null;
        Action<OctopusCommunityAccessDeniedReason?> handler = v => { calls++; seen = v; };
        OctopusSDK.OnCommunityAccessDeniedReasonChanged += handler;
        GameObject go = null;
        try
        {
            go = new GameObject("ch");
            var channel = go.AddComponent<OctopusSDK.OctopusChannel>();

            channel.OnCommunityAccessDeniedReason("ACCOUNT_BANNED");
            Assert.AreEqual(1, calls);
            Assert.AreEqual(OctopusCommunityAccessDeniedReason.AccountBanned, seen);
            Assert.AreEqual(OctopusCommunityAccessDeniedReason.AccountBanned, OctopusSDK.CommunityAccessDeniedReason);

            channel.OnCommunityAccessDeniedReason("");
            Assert.AreEqual(2, calls);
            Assert.IsNull(seen);
            Assert.IsNull(OctopusSDK.CommunityAccessDeniedReason);
        }
        finally
        {
            OctopusSDK.OnCommunityAccessDeniedReasonChanged -= handler;
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void EmitCommunityAccessDeniedReason_RaisesEventAndCachesValue()
    {
        OctopusCommunityAccessDeniedReason? seen = null;
        Action<OctopusCommunityAccessDeniedReason?> h = v => seen = v;
        OctopusSDK.OnCommunityAccessDeniedReasonChanged += h;
        try { OctopusSDK.Mock.EmitCommunityAccessDeniedReason(OctopusCommunityAccessDeniedReason.CommunityClosed); }
        finally { OctopusSDK.OnCommunityAccessDeniedReasonChanged -= h; }
        Assert.AreEqual(OctopusCommunityAccessDeniedReason.CommunityClosed, seen);
        Assert.AreEqual(OctopusCommunityAccessDeniedReason.CommunityClosed, OctopusSDK.CommunityAccessDeniedReason);
        OctopusSDK.Mock.EmitCommunityAccessDeniedReason(null);
        Assert.IsNull(OctopusSDK.CommunityAccessDeniedReason);
    }

    [Test]
    public void Reset_ClearsCommunityAccessDeniedReason()
    {
        OctopusSDK.Mock.EmitCommunityAccessDeniedReason(OctopusCommunityAccessDeniedReason.AccountBanned);
        Assert.AreEqual(OctopusCommunityAccessDeniedReason.AccountBanned, OctopusSDK.CommunityAccessDeniedReason);

        OctopusSDK.Mock.Reset();

        Assert.IsNull(OctopusSDK.CommunityAccessDeniedReason);
    }

    // The result/error channel methods must not throw when the request id is unknown
    // (e.g. a late callback after the entry was already consumed or dropped).
    [Test]
    public void Channel_OverrideResultAndError_UnknownId_AreSafe()
    {
        GameObject go = null;
        try
        {
            go = new GameObject("ch");
            var channel = go.AddComponent<OctopusSDK.OctopusChannel>();
            Assert.DoesNotThrow(() => channel.OnOverrideCommunityAccessResult("999999\n"));
            Assert.DoesNotThrow(() => channel.OnOverrideCommunityAccessError("999999\nboom"));
        }
        finally
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
