using NUnit.Framework;

public class OctopusMockNotificationsTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void EmitNotSeenCount_RaisesEventWithValue()
    {
        int seen = -1;
        System.Action<int> h = n => seen = n;
        OctopusSDK.OnNotSeenNotificationsCount += h;
        try { OctopusSDK.Mock.EmitNotSeenCount(5); }
        finally { OctopusSDK.OnNotSeenNotificationsCount -= h; }
        Assert.AreEqual(5, seen);
    }

    [Test]
    public void UpdateNotSeenNotificationsCount_EmitsSeededCount()
    {
        var s = OctopusMockSettings.GetOrCreateSettings(); // default initialNotSeenCount = 0
        int seen = -1;
        System.Action<int> h = n => seen = n;
        OctopusSDK.OnNotSeenNotificationsCount += h;
        try { OctopusSDK.UpdateNotSeenNotificationsCount(); }
        finally { OctopusSDK.OnNotSeenNotificationsCount -= h; }
        Assert.AreEqual(s.InitialNotSeenCount, seen);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("UpdateNotSeenNotificationsCount").HasValue);
    }

    [Test]
    public void RegisterNotificationsToken_Records()
    {
        OctopusSDK.RegisterNotificationsToken("abc");
        Assert.AreEqual("abc", OctopusSDK.Mock.LastCall("RegisterNotificationsToken").Value.Args[0]);
    }
}
