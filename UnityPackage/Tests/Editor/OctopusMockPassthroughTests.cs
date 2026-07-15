using System.Collections.Generic;
using NUnit.Framework;

public class OctopusMockPassthroughTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    // NavigateToUrlHandler is a static field; clear it even if a test assertion throws,
    // so a leaked handler can't bleed into other fixtures.
    [TearDown]
    public void TearDown() => OctopusSDK.NavigateToUrlHandler = null;

    [Test]
    public void Track_RecordsNameAndSetsAccessor()
    {
        OctopusSDK.Track("level_up", new Dictionary<string, string> { { "lvl", "3" } });
        Assert.AreEqual("level_up", OctopusSDK.Mock.LastCall("Track").Value.Args[0]);
        Assert.AreEqual("level_up", OctopusSDK.Mock.LastTrackedEvent);
    }

    [Test]
    public void TrackAccessToCommunity_Records()
    {
        OctopusSDK.TrackAccessToCommunity(true);
        Assert.AreEqual(true, OctopusSDK.Mock.LastCall("TrackAccessToCommunity").Value.Args[0]);
    }

    [Test]
    public void OverrideDefaultLocale_Records()
    {
        OctopusSDK.OverrideDefaultLocale("fr");
        Assert.AreEqual("fr", OctopusSDK.Mock.LastCall("OverrideDefaultLocale").Value.Args[0]);
    }

    [Test]
    public void SetAppName_Records()
    {
        OctopusSDK.SetAppName("MyApp");
        Assert.AreEqual("MyApp", OctopusSDK.Mock.LastCall("SetAppName").Value.Args[0]);
    }

    [Test]
    public void NavigateToUrlHandler_Set_EnablesInterceptionRecorded()
    {
        OctopusSDK.NavigateToUrlHandler = url => UrlOpeningStrategy.HandledByApp;
        Assert.IsTrue(OctopusSDK.Mock.LastCall("SetUrlInterceptionEnabled").HasValue);
        Assert.AreEqual(true, OctopusSDK.Mock.LastCall("SetUrlInterceptionEnabled").Value.Args[0]);
    }

    [Test]
    public void SetForcedOrientation_Records()
    {
        OctopusSDK.SetForcedOrientation((int)OctopusThemeSettings.ForcedOrientationType.Portrait);
        Assert.AreEqual(1, OctopusSDK.Mock.LastCall("SetForcedOrientation").Value.Args[0]);
    }

    [Test]
    public void SetUnityTheme_CascadesThroughRoutedSetters()
    {
        // SetUnityTheme runs in the Editor now (it only calls the routed public setters).
        // With a settings asset present it always applies nav-bar + color-scheme type + forced
        // orientation, so those should appear in the recorded calls — matching device behavior.
        OctopusThemeSettings.GetOrCreateSettings();
        OctopusSDK.Mock.Reset();
        OctopusSDK.SetUnityTheme();
        Assert.IsTrue(OctopusSDK.Mock.LastCall("SetColorSchemeType").HasValue);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("SetNavBarUsesPrimaryColor").HasValue);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("SetForcedOrientation").HasValue);
    }
}
