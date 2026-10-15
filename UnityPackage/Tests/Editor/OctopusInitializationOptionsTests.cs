using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class OctopusInitializationOptionsTests
{
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
        OctopusSDK.Mock.Reset();
    }

    [Test]
    public void AppManagedAudioSessionDefaultsToFalse()
    {
        Assert.IsFalse(new OctopusInitializationOptions().AppManagedAudioSession);
    }

    [Test]
    public void ExistingInitializeSignatureSendsNativeDefault()
    {
        OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO());
        AssertAudioSessionPayload(false);
    }

    [Test]
    public void NullOptionsSendNativeDefault()
    {
        OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO(), null, 443, null);
        AssertAudioSessionPayload(false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InitializeForwardsAudioSessionOptionInIosPayload(bool appManagedAudioSession)
    {
        var options = new OctopusInitializationOptions { AppManagedAudioSession = appManagedAudioSession };
        OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO(), null, 443, options);
        // The options are a snapshot, not a live reference to host-owned mutable state.
        options.AppManagedAudioSession = !appManagedAudioSession;
        AssertAudioSessionPayload(appManagedAudioSession);
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public void CommunitySwitchDoesNotOverwriteInitializedAudioSessionOptions(
        bool appManagedAudioSession, bool stopBeforeSwitch, bool customServer)
    {
        var options = new OctopusInitializationOptions { AppManagedAudioSession = appManagedAudioSession };
        OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO(), null, 443, options);
        options.AppManagedAudioSession = !appManagedAudioSession;

        if (stopBeforeSwitch)
        {
            bool stopped = false;
            OctopusSDK.Stop(() => stopped = true);
            Drain();
            Assert.IsTrue(stopped);
            Assert.IsFalse(OctopusSDK.MockBackend.LifecycleInitialized);
        }

        bool switched = false;
        if (customServer)
        {
            OctopusSDK.SwitchCommunity("YOUR_API_KEY", ConnectionMode.SSO(), "api.example.test", 8443,
                () => switched = true);
        }
        else
        {
            OctopusSDK.SwitchCommunity("YOUR_API_KEY", ConnectionMode.SSO(), () => switched = true);
        }
        Drain();

        Assert.IsTrue(switched);
        Assert.IsTrue(OctopusSDK.MockBackend.LifecycleInitialized);
        // The mock cannot prove Swift's retained state. It does prove that neither C# switch
        // path reinitializes with default options, even after Stop or options-object mutation.
        Assert.AreEqual(1, OctopusSDK.Mock.Calls.Count(call => call.Method == "Initialize"),
            "SwitchCommunity must not replace the host's initialization options with defaults.");
        AssertAudioSessionPayload(appManagedAudioSession);
        var switchCall = OctopusSDK.Mock.LastCall("SwitchCommunity").Value;
        Assert.AreEqual(customServer ? "api.example.test" : "", switchCall.Args[2]);
        Assert.AreEqual(customServer ? 8443 : 443, switchCall.Args[3]);
    }

    private static void Drain()
    {
        var dispatcher = Object.FindObjectOfType<OctopusMainThread>();
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(dispatcher, null);
    }

    private static void AssertAudioSessionPayload(bool expected)
    {
        var call = OctopusSDK.Mock.LastCall("Initialize");
        Assert.IsTrue(call.HasValue);
        Assert.AreEqual("YOUR_API_KEY", call.Value.Args[0]);
        Assert.AreEqual("sso", call.Value.Args[1]);
        // The Editor records the same JSON argument that the iOS P/Invoke consumes.
        var payload = OctopusJson.ParseRawObject((string)call.Value.Args[2]);
        Assert.AreEqual(expected ? "true" : "false", payload["appManagedAudioSession"]);
    }
}
