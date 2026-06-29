using NUnit.Framework;

public class OctopusNavigateToUrlTests
{
    [TearDown]
    public void Cleanup() => OctopusSDK.NavigateToUrlHandler = null;

    [Test]
    public void ResolveUrlStrategy_NoHandler_DefaultsToHandledByOctopus()
    {
        OctopusSDK.NavigateToUrlHandler = null;
        Assert.AreEqual(UrlOpeningStrategy.HandledByOctopus,
            OctopusSDK.ResolveUrlStrategy("https://x.test"));
    }

    [Test]
    public void ResolveUrlStrategy_HandlerReturningHandledByApp_IsHonored()
    {
        OctopusSDK.NavigateToUrlHandler = _ => UrlOpeningStrategy.HandledByApp;
        Assert.AreEqual(UrlOpeningStrategy.HandledByApp,
            OctopusSDK.ResolveUrlStrategy("https://x.test"));
    }

    [Test]
    public void ResolveUrlStrategy_PassesUrlToHandler()
    {
        string seen = null;
        OctopusSDK.NavigateToUrlHandler = url => { seen = url; return UrlOpeningStrategy.HandledByApp; };
        OctopusSDK.ResolveUrlStrategy("https://x.test/deep");
        Assert.AreEqual("https://x.test/deep", seen);
    }

    [Test]
    public void ResolveUrlStrategyCode_NoHandler_Returns1()
    {
        OctopusSDK.NavigateToUrlHandler = null;
        Assert.AreEqual(1, OctopusSDK.ResolveUrlStrategyCode("https://x.test"));
    }

    [Test]
    public void ResolveUrlStrategyCode_HandlerHandledByApp_Returns0()
    {
        OctopusSDK.NavigateToUrlHandler = _ => UrlOpeningStrategy.HandledByApp;
        Assert.AreEqual(0, OctopusSDK.ResolveUrlStrategyCode("https://x.test"));
    }

    [Test]
    public void ResolveUrlStrategyCode_HandlerHandledByOctopus_Returns1()
    {
        OctopusSDK.NavigateToUrlHandler = _ => UrlOpeningStrategy.HandledByOctopus;
        Assert.AreEqual(1, OctopusSDK.ResolveUrlStrategyCode("https://x.test"));
    }
}
