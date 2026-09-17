using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class OctopusLifecycleTests
{
    private static void Drain()
    {
        var dispatcher = Object.FindObjectOfType<OctopusMainThread>();
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(dispatcher, null);
    }

    [SetUp]
    public void SetUp()
    {
        OctopusSDK.Mock.Enabled = true;
        OctopusSDK.Stop();
        Drain();
        OctopusSDK.Mock.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Drain();
        OctopusSDK.Stop();
        Drain();
        OctopusSDK.Mock.Enabled = true;
        OctopusSDK.Mock.Reset();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("1")]
    [TestCase("0\n")]
    [TestCase("-1\n")]
    [TestCase("abc\n")]
    [TestCase("2147483648\n")]
    public void MalformedPayloadIsRejected(string payload)
    {
        int id;
        string message;
        Assert.IsFalse(OctopusSDK.TryParseLifecycleResponse(payload, out id, out message));
    }

    [Test]
    public void ParsePreservesMultilineError()
    {
        int id;
        string message;
        Assert.IsTrue(OctopusSDK.TryParseLifecycleResponse("42\nfirst\nsecond", out id, out message));
        Assert.AreEqual(42, id);
        Assert.AreEqual("first\nsecond", message);
    }

    [Test]
    public void SwitchColdStartsAndClearsOldScreenWithoutLoggingCredential()
    {
        OctopusSDK.Mock.CurrentScreen = "old screen";
        int completions = 0;
        OctopusSDK.SwitchCommunity("YOUR_API_KEY", ConnectionMode.SSO(ProfileField.BIO), () => completions++);
        Assert.AreEqual(0, completions);
        Drain();
        Assert.AreEqual(1, completions);
        Assert.IsTrue(OctopusSDK.MockBackend.LifecycleInitialized);
        Assert.IsNull(OctopusSDK.Mock.CurrentScreen);
        var call = OctopusSDK.Mock.LastCall("SwitchCommunity").Value;
        Assert.AreEqual("sso", call.Args[0]);
        CollectionAssert.AreEqual(new[] { (int)ProfileField.BIO }, (int[])call.Args[1]);
        StringAssert.DoesNotContain("YOUR_API_KEY", call.ToString());
    }

    [Test]
    public void OctopusAuthVariantUsesOctopusMode()
    {
        OctopusSDK.SwitchCommunityOctopusAuth("YOUR_API_KEY");
        Drain();
        Assert.AreEqual("octopus", OctopusSDK.Mock.LastCall("SwitchCommunity").Value.Args[0]);
    }

    [Test]
    public void ResetKeepsInitializationAndCallHistoryStopReleasesInitialization()
    {
        OctopusSDK.SwitchCommunityOctopusAuth("YOUR_API_KEY");
        Drain();
        OctopusSDK.Reset();
        Drain();
        Assert.IsTrue(OctopusSDK.MockBackend.LifecycleInitialized);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("SwitchCommunity").HasValue);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("Reset").HasValue);
        OctopusSDK.Stop();
        Drain();
        Assert.IsFalse(OctopusSDK.MockBackend.LifecycleInitialized);
        OctopusSDK.SwitchCommunityOctopusAuth("YOUR_API_KEY");
        Drain();
        Assert.IsTrue(OctopusSDK.MockBackend.LifecycleInitialized);
    }

    [Test]
    public void ResetBeforeInitializationAndRepeatedStopComplete()
    {
        int count = 0;
        OctopusSDK.Reset(() => count++);
        Drain();
        Assert.IsFalse(OctopusSDK.MockBackend.LifecycleInitialized);
        OctopusSDK.Stop(() => count++);
        Drain();
        OctopusSDK.Stop(() => count++);
        Drain();
        Assert.AreEqual(3, count);
    }

    [Test]
    public void CloseOnlyDismissesAndIsIdempotent()
    {
        OctopusSDK.SwitchCommunityOctopusAuth("YOUR_API_KEY");
        Drain();
        OctopusSDK.Mock.CurrentScreen = "Main feed";
        OctopusSDK.Close();
        OctopusSDK.Close();
        Assert.IsNull(OctopusSDK.Mock.CurrentScreen);
        Assert.IsTrue(OctopusSDK.MockBackend.LifecycleInitialized);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("Stop").HasValue);
    }

    [Test]
    public void DisabledMockStillCompletesLifecycleOperations()
    {
        OctopusSDK.Mock.Enabled = false;
        int count = 0;
        OctopusSDK.SwitchCommunityOctopusAuth("YOUR_API_KEY", () => count++);
        Drain();
        OctopusSDK.Reset(() => count++);
        Drain();
        OctopusSDK.Stop(() => count++);
        Drain();
        Assert.AreEqual(3, count);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [Test]
    public void OverlapIsRejectedWithoutReplacingFirstCallback()
    {
        int completed = 0;
        string error = null;
        OctopusSDK.Reset(() => completed++);
        OctopusSDK.Stop(() => Assert.Fail("Overlapping stop must not complete"), e => error = e);
        Drain();
        Assert.AreEqual(1, completed);
        StringAssert.Contains("already in progress", error);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("Stop").HasValue);
    }

    [Test]
    public void CompletionCanStartNextOperation()
    {
        int completed = 0;
        OctopusSDK.Reset(() => OctopusSDK.Stop(() => completed++));
        Drain();
        Assert.AreEqual(1, completed);
    }

    [Test]
    public void InvalidArgumentsFailAsynchronouslyWithoutDispatch()
    {
        string error = null;
        OctopusSDK.SwitchCommunity("YOUR_API_KEY", null, () => Assert.Fail(), e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.IsNotNull(error);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("SwitchCommunity").HasValue);
    }

    [Test]
    public void NativeLandingPadsIgnoreUnknownAndDuplicateResponsesAndReleaseErrors()
    {
        // Register without the Mock's automatic success so the native failure lane is exercised.
        var begin = typeof(OctopusSDK).GetMethod("BeginLifecycleRequest", BindingFlags.NonPublic | BindingFlags.Static);
        int completed = 0;
        string error = null;
        int id = (int)begin.Invoke(null, new object[] { new System.Action(() => completed++),
            new System.Action<string>(e => error = e) });
        var go = new GameObject("LifecycleTestChannel");
        try
        {
            var channel = go.AddComponent<OctopusSDK.OctopusChannel>();
            channel.OnLifecycleResult((id + 1) + "\n");
            channel.OnLifecycleError(null);
            Drain();
            Assert.IsNull(error);
            channel.OnLifecycleError(id + "\nfirst\nsecond");
            channel.OnLifecycleResult(id + "\n");
            Drain();
            Assert.AreEqual("first\nsecond", error);
            Assert.AreEqual(0, completed);
            OctopusSDK.Reset(() => completed++);
            Drain();
            Assert.AreEqual(1, completed);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
