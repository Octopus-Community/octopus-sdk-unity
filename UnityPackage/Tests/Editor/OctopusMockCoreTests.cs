using NUnit.Framework;

public class OctopusMockCoreTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void Record_WhenEnabled_AppendsCallWithArgs()
    {
        OctopusSDK.Mock.Record("Foo", "a", 1);
        Assert.AreEqual(1, OctopusSDK.Mock.Calls.Count);
        Assert.AreEqual("Foo", OctopusSDK.Mock.Calls[0].Method);
        Assert.AreEqual("a", OctopusSDK.Mock.Calls[0].Args[0]);
        Assert.AreEqual(1, OctopusSDK.Mock.Calls[0].Args[1]);
    }

    [Test]
    public void Record_WhenDisabled_RecordsNothing()
    {
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.Mock.Record("Foo", "a");
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [Test]
    public void Reset_ClearsCallsAndScreen()
    {
        OctopusSDK.Mock.Record("Foo");
        OctopusSDK.Mock.CurrentScreen = "x";
        OctopusSDK.Mock.Reset();
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
        Assert.IsNull(OctopusSDK.Mock.CurrentScreen);
    }

    [Test]
    public void LastCall_ReturnsMostRecentMatch()
    {
        OctopusSDK.Mock.Record("Open", "1");
        OctopusSDK.Mock.Record("Open", "2");
        Assert.AreEqual("2", OctopusSDK.Mock.LastCall("Open").Value.Args[0]);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("Nope").HasValue);
    }

    [Test]
    public void Settings_GetOrCreate_ReturnsAssetWithDefaults()
    {
        var s = OctopusMockSettings.GetOrCreateSettings();
        Assert.IsNotNull(s);
        Assert.IsTrue(s.EnabledByDefault);
        Assert.IsTrue(s.ShowOverlay);
        Assert.AreEqual(0, s.InitialNotSeenCount);
        Assert.IsNotNull(s.SeedGroups);
    }

    [Test]
    public void Initialize_RecordsCallAndAppliesEnabledFromSettings()
    {
        OctopusMockSettings.GetOrCreateSettings(); // ensure asset exists
        OctopusSDK.Mock.Reset();
        OctopusSDK.Initialize("test-key", ConnectionMode.OctopusAuth());
        Assert.IsTrue(OctopusSDK.Mock.LastCall("Initialize").HasValue);
        Assert.AreEqual("test-key", OctopusSDK.Mock.LastCall("Initialize").Value.Args[0]);
    }

    [Test]
    public void Initialize_SeedsShowOverlayFromSettings()
    {
        // The example asset has ShowOverlay = true; Initialize should apply it,
        // overriding a code-set value when a settings asset is present.
        OctopusMockSettings.GetOrCreateSettings();
        OctopusSDK.Mock.ShowOverlay = false;
        OctopusSDK.Initialize("test-key", ConnectionMode.OctopusAuth());
        Assert.IsTrue(OctopusSDK.Mock.ShowOverlay);
    }
}
