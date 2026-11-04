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
    public void LateSubscriber_ReceivesLastCountOnSubscribe()
    {
        OctopusSDK.Mock.EmitNotSeenCount(7);
        int seen = -1;
        System.Action<int> h = n => seen = n;
        OctopusSDK.OnNotSeenNotificationsCount += h;
        OctopusSDK.OnNotSeenNotificationsCount -= h;
        Assert.AreEqual(7, seen);
    }

    [Test]
    public void LateSubscriber_GetsNothingBeforeAnyCount()
    {
        int calls = 0;
        System.Action<int> h = n => calls++;
        OctopusSDK.OnNotSeenNotificationsCount += h;
        OctopusSDK.OnNotSeenNotificationsCount -= h;
        Assert.AreEqual(0, calls);
    }

    [Test]
    public void LateSubscriber_ReplayReachesOnlyTheNewHandler()
    {
        var early = new System.Collections.Generic.List<int>();
        var late = new System.Collections.Generic.List<int>();
        System.Action<int> e = n => early.Add(n);
        System.Action<int> l = n => late.Add(n);
        OctopusSDK.OnNotSeenNotificationsCount += e;
        try
        {
            OctopusSDK.Mock.EmitNotSeenCount(3);
            OctopusSDK.OnNotSeenNotificationsCount += l;
            try { OctopusSDK.Mock.EmitNotSeenCount(4); }
            finally { OctopusSDK.OnNotSeenNotificationsCount -= l; }
        }
        finally { OctopusSDK.OnNotSeenNotificationsCount -= e; }
        CollectionAssert.AreEqual(new[] { 3, 4 }, early);
        CollectionAssert.AreEqual(new[] { 3, 4 }, late);
    }

    [Test]
    public void ConcurrentSubscriptions_AreAllKept()
    {
        const int count = 16;
        int raised = 0;
        var handlers = new System.Action<int>[count];
        for (int i = 0; i < count; i++) handlers[i] = n => System.Threading.Interlocked.Increment(ref raised);
        var start = new System.Threading.ManualResetEventSlim(false);
        var threads = new System.Threading.Thread[count];
        for (int i = 0; i < count; i++)
        {
            var h = handlers[i];
            threads[i] = new System.Threading.Thread(() => { start.Wait(); OctopusSDK.OnNotSeenNotificationsCount += h; });
            threads[i].Start();
        }
        start.Set();
        foreach (var t in threads) t.Join();
        try { OctopusSDK.Mock.EmitNotSeenCount(1); }
        finally { foreach (var h in handlers) OctopusSDK.OnNotSeenNotificationsCount -= h; }
        Assert.AreEqual(count, raised);
    }

    [Test]
    public void WorkerThreadSubscriber_IsReplayedOnTheMainThread()
    {
        OctopusSDK.Mock.EmitNotSeenCount(6);
        int mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        int seen = -1, replayThread = -1;
        System.Action<int> h = n => { seen = n; replayThread = System.Threading.Thread.CurrentThread.ManagedThreadId; };
        SubscribeFromWorker(h);
        try
        {
            Assert.AreEqual(-1, seen, "replayed on the worker thread");
            OctopusMainThread.Drain();
        }
        finally { OctopusSDK.OnNotSeenNotificationsCount -= h; }
        Assert.AreEqual(6, seen);
        Assert.AreEqual(mainThread, replayThread);
    }

    [Test]
    public void WorkerThreadSubscriber_RemovedBeforeReplay_IsNotRaised()
    {
        OctopusSDK.Mock.EmitNotSeenCount(6);
        int calls = 0;
        System.Action<int> h = n => calls++;
        SubscribeFromWorker(h);
        OctopusSDK.OnNotSeenNotificationsCount -= h;
        OctopusMainThread.Drain();
        Assert.AreEqual(0, calls);
    }

    [Test]
    public void WorkerThreadMulticastSubscriber_PartlyRemoved_ReplaysOnlyTheRest()
    {
        OctopusSDK.Mock.EmitNotSeenCount(6);
        int a = 0, b = 0;
        System.Action<int> ha = n => a++;
        System.Action<int> hb = n => b++;
        SubscribeFromWorker(ha + hb);
        OctopusSDK.OnNotSeenNotificationsCount -= hb;
        try { OctopusMainThread.Drain(); }
        finally { OctopusSDK.OnNotSeenNotificationsCount -= ha; }
        Assert.AreEqual(1, a);
        Assert.AreEqual(0, b);
    }

    [Test]
    public void WorkerThreadSubscribers_RemovedAsMulticast_AreNotRaised()
    {
        OctopusSDK.Mock.EmitNotSeenCount(6);
        int calls = 0;
        System.Action<int> ha = n => calls++;
        System.Action<int> hb = n => calls++;
        SubscribeFromWorker(ha);
        SubscribeFromWorker(hb);
        OctopusSDK.OnNotSeenNotificationsCount -= ha + hb;
        OctopusMainThread.Drain();
        Assert.AreEqual(0, calls);
    }

    private static void SubscribeFromWorker(System.Action<int> h)
    {
        var worker = new System.Threading.Thread(() => OctopusSDK.OnNotSeenNotificationsCount += h);
        worker.Start();
        worker.Join();
    }

    [Test]
    public void Unsubscribed_HandlerIsNotRaised()
    {
        int calls = 0;
        System.Action<int> h = n => calls++;
        OctopusSDK.OnNotSeenNotificationsCount += h;
        OctopusSDK.OnNotSeenNotificationsCount -= h;
        OctopusSDK.Mock.EmitNotSeenCount(2);
        Assert.AreEqual(0, calls);
    }

    [Test]
    public void MockReset_ForgetsLastCount()
    {
        OctopusSDK.Mock.EmitNotSeenCount(9);
        OctopusSDK.Mock.Reset();
        int calls = 0;
        System.Action<int> h = n => calls++;
        OctopusSDK.OnNotSeenNotificationsCount += h;
        OctopusSDK.OnNotSeenNotificationsCount -= h;
        Assert.AreEqual(0, calls);
    }

    [Test]
    public void Initialize_PushesSeededCountToLateSubscriber()
    {
        var s = OctopusMockSettings.GetOrCreateSettings();
        OctopusSDK.Initialize("test-key", ConnectionMode.SSO());
        Assume.That(OctopusSDK.Mock.Enabled, "the settings asset disables the mock");
        int seen = -1;
        System.Action<int> h = n => seen = n;
        OctopusSDK.OnNotSeenNotificationsCount += h;
        OctopusSDK.OnNotSeenNotificationsCount -= h;
        Assert.AreEqual(s.InitialNotSeenCount, seen);
    }

    [Test]
    public void RegisterNotificationsToken_Records()
    {
        OctopusSDK.RegisterNotificationsToken("abc");
        Assert.AreEqual("abc", OctopusSDK.Mock.LastCall("RegisterNotificationsToken").Value.Args[0]);
    }
}
