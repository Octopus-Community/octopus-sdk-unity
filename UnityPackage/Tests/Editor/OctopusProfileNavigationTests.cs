using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class OctopusProfileNavigationTests
{
    private GameObject _dispatcher;
    private GameObject _channel;
    private Action<string> _observer;
    private OctopusMainThread _drainer;
    private OctopusSDK.OctopusChannel _receiver;

    private void Drain()
    {
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_drainer, null);
    }

    [SetUp]
    public void SetUp()
    {
        _dispatcher = new GameObject("Navigation test dispatcher");
        _drainer = _dispatcher.AddComponent<OctopusMainThread>();
        _channel = new GameObject("Navigation test channel");
        _receiver = _channel.AddComponent<OctopusSDK.OctopusChannel>();
        OctopusSDK.Mock.Enabled = true;
        OctopusSDK.NavigateToProfileHandler = null;
        Drain();
        OctopusSDK.Mock.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Drain();
        OctopusSDK.NavigateToProfileHandler = null;
        if (_observer != null) OctopusSDK.OnNavigateToProfile -= _observer;
        _observer = null;
        OctopusSDK.Mock.Reset();
        UnityEngine.Object.DestroyImmediate(_channel);
        UnityEngine.Object.DestroyImmediate(_dispatcher);
    }

    [Test]
    public void NativePad_ParsesEscapedId_QueuesHandlerAndEvent()
    {
        string handled = null;
        string observed = null;
        OctopusSDK.NavigateToProfileHandler = id => handled = id;
        _observer = id => observed = id;
        OctopusSDK.OnNavigateToProfile += _observer;
        _receiver.OnNavigateToProfile(
            "{\"clientUserId\":\"member-\\\"\\u00e9\\\\\"}");
        Assert.IsNull(handled);
        Assert.IsNull(observed);
        Drain();
        Assert.AreEqual("member-\"é\\", handled);
        Assert.AreEqual(handled, observed);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("{}")]
    [TestCase("{\"profileId\":\"wrong-id-kind\"}")]
    [TestCase("{\"clientUserId\":null}")]
    [TestCase("{\"clientUserId\":\" \"}")]
    [TestCase("{\"clientUserId\":\"\\uZZZZ\"}")]
    public void NativePad_InvalidPayload_DoesNotDispatch(string payload)
    {
        int count = 0;
        OctopusSDK.NavigateToProfileHandler = _ => count++;
        OctopusSDK.ReceiveNavigateToProfile(payload);
        Drain();
        Assert.AreEqual(0, count);
    }

    [Test]
    public void HandlerToggle_UpdatesNativeOptIn_AndMockDismissesBeforeDelivery()
    {
        OctopusSDK.OpenProfile();
        OctopusSDK.Mock.EmitNavigateToProfile("user-1");
        Assert.AreEqual("Current profile", OctopusSDK.Mock.CurrentScreen);
        string seen = null;
        OctopusSDK.NavigateToProfileHandler = id => seen = id;
        Assert.AreEqual(true, OctopusSDK.Mock.LastCall("SetProfileInterceptionEnabled").Value.Args[0]);
        OctopusSDK.Mock.EmitNavigateToProfile("user-1");
        Assert.IsNull(OctopusSDK.Mock.CurrentScreen);
        Assert.IsNull(seen);
        Drain();
        Assert.AreEqual("user-1", seen);
        OctopusSDK.NavigateToProfileHandler = null;
        Assert.AreEqual(false, OctopusSDK.Mock.LastCall("SetProfileInterceptionEnabled").Value.Args[0]);
    }

    [Test]
    public void BackgroundCallback_DeliversOnlyWhenMainThreadDrains()
    {
        int unityThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        int callbackThread = -1;
        OctopusSDK.NavigateToProfileHandler = _ =>
            callbackThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        System.Threading.Tasks.Task.Run(() => OctopusSDK.ReceiveNavigateToProfile(
            "{\"clientUserId\":\"user-1\"}")).GetAwaiter().GetResult();
        Assert.AreEqual(-1, callbackThread);
        Drain();
        Assert.AreEqual(unityThread, callbackThread);
    }

    [Test]
    public void EventSubscriptionAlone_DoesNotEnableInterception()
    {
        int count = 0;
        _observer = _ => count++;
        OctopusSDK.OnNavigateToProfile += _observer;
        OctopusSDK.OpenActivity();
        OctopusSDK.Mock.EmitNavigateToProfile("user-1");
        Drain();
        Assert.AreEqual(0, count);
        Assert.AreEqual("Activity", OctopusSDK.Mock.CurrentScreen);
    }

    [Test]
    public void QueuedTap_KeepsHandlerCapturedAtReceipt()
    {
        int oldCount = 0;
        int newCount = 0;
        OctopusSDK.NavigateToProfileHandler = _ => oldCount++;
        OctopusSDK.Mock.EmitNavigateToProfile("user-1");
        OctopusSDK.NavigateToProfileHandler = _ => newCount++;
        Drain();
        Assert.AreEqual(1, oldCount);
        Assert.AreEqual(0, newCount);
    }

    [Test]
    public void EveryOpen_RecordsMode_AndOriginalSignaturesRemainCallable()
    {
        // Method-group conversions check the old signatures, not just call-site compatibility.
        Action<OctopusNotification> open = OctopusSDK.Open;
        Action<string> group = OctopusSDK.OpenGroup;
        Action<string> post = OctopusSDK.OpenPost;
        Action<OctopusPrefilledPost> create = OctopusSDK.OpenCreatePost;
        open(null); group("g"); post("p"); create(null);
        OctopusSDK.Open(default(OctopusNotification));
        OctopusSDK.Open();
        Assert.IsNull(OctopusSDK.Mock.LastCall("Open").Value.Args[1]);
        OctopusSDK.OpenCreatePost();
        Assert.IsNull(OctopusSDK.Mock.LastCall("OpenCreatePost").Value.Args[2]);
        OctopusSDK.Open(navigationMode: OctopusNavigationMode.NavigationStack);
        Assert.AreEqual(OctopusNavigationMode.NavigationStack, OctopusSDK.Mock.LastCall("Open").Value.Args[1]);
        OctopusSDK.OpenGroup("g", OctopusNavigationMode.Automatic);
        Assert.AreEqual(OctopusNavigationMode.Automatic, OctopusSDK.Mock.LastCall("OpenGroup").Value.Args[1]);
        OctopusSDK.OpenPost("p", OctopusNavigationMode.NavigationStack);
        Assert.AreEqual(OctopusNavigationMode.NavigationStack, OctopusSDK.Mock.LastCall("OpenPost").Value.Args[1]);
        OctopusSDK.OpenCreatePost(navigationMode: OctopusNavigationMode.Automatic);
        Assert.AreEqual(OctopusNavigationMode.Automatic, OctopusSDK.Mock.LastCall("OpenCreatePost").Value.Args[2]);
        OctopusSDK.OpenProfile("user-1", OctopusNavigationMode.NavigationStack);
        Assert.AreEqual("Profile user-1", OctopusSDK.Mock.CurrentScreen);
        Assert.AreEqual(OctopusNavigationMode.NavigationStack, OctopusSDK.Mock.LastCall("OpenProfile").Value.Args[1]);
        OctopusSDK.OpenActivity(OctopusNavigationMode.Automatic);
        Assert.AreEqual("Activity", OctopusSDK.Mock.CurrentScreen);
        Assert.AreEqual(OctopusNavigationMode.Automatic, OctopusSDK.Mock.LastCall("OpenActivity").Value.Args[0]);
    }

    [Test]
    public void DisabledMock_DoesNotRecordOpenOrChangeScreen()
    {
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.OpenProfile("user-1");
        OctopusSDK.OpenActivity();
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
        Assert.IsNull(OctopusSDK.Mock.CurrentScreen);
    }

    [Test]
    public void NavigationModeWireCodes_PreserveDefaultAndRejectInvalidValues()
    {
        Assert.AreEqual(-1, OctopusSDK.NavigationModeCode(null));
        Assert.AreEqual(0, OctopusSDK.NavigationModeCode(OctopusNavigationMode.NavigationStack));
        Assert.AreEqual(1, OctopusSDK.NavigationModeCode(OctopusNavigationMode.Automatic));
        Assert.Throws<ArgumentOutOfRangeException>(() => OctopusSDK.OpenActivity((OctopusNavigationMode)99));
        Assert.IsNull(OctopusSDK.Mock.LastCall("OpenActivity"));
    }
}
