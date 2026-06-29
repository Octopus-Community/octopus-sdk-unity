using System;
using System.Threading;
using NUnit.Framework;

public class OctopusEventDispatcherTests
{
    [Test]
    public void Enqueue_parsesAndRaises_offCallingThread_inOrder()
    {
        var received = new System.Collections.Generic.List<OctopusEvent>();
        int handlerThread = 0;
        var done = new CountdownEvent(2);
        Action<OctopusEvent> h = e =>
        {
            lock (received) received.Add(e);
            handlerThread = Thread.CurrentThread.ManagedThreadId;
            done.Signal();
        };
        OctopusSDK.OnOctopusEvent += h;
        try
        {
            OctopusEventDispatcher.Enqueue("{\"type\":\"SessionStarted\",\"sessionId\":\"s1\"}");
            OctopusEventDispatcher.Enqueue("{\"type\":\"PostCreated\",\"postId\":\"p1\",\"textLength\":\"0\",\"content\":\"\"}");

            Assert.IsTrue(done.Wait(2000), "events not delivered within timeout");
            Assert.AreEqual(2, received.Count);
            Assert.IsInstanceOf<SessionStartedEvent>(received[0]);   // FIFO order preserved
            Assert.IsInstanceOf<PostCreatedEvent>(received[1]);
            Assert.AreEqual("s1", ((SessionStartedEvent)received[0]).SessionId);
            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, handlerThread,
                "handler should run off the calling thread (background dispatcher)");
        }
        finally { OctopusSDK.OnOctopusEvent -= h; }
    }
}
