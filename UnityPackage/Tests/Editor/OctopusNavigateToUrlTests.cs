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
}
