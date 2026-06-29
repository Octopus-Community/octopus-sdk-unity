using System;
using NUnit.Framework;

public class OctopusEventDispatchTests
{
    [Test]
    public void OnOctopusEvent_receivesEmittedInstance()
    {
        OctopusEvent received = null;
        Action<OctopusEvent> h = e => received = e;
        OctopusSDK.OnOctopusEvent += h;
        try
        {
            var sent = new PostCreatedEvent { PostId = "p1" };
            OctopusSDK.Mock.EmitOctopusEvent(sent);
            Assert.AreSame(sent, received);
        }
        finally { OctopusSDK.OnOctopusEvent -= h; }
    }

    [Test]
    public void EmitOctopusEventJson_parsesAndDispatches()
    {
        OctopusEvent received = null;
        Action<OctopusEvent> h = e => received = e;
        OctopusSDK.OnOctopusEvent += h;
        try
        {
            OctopusSDK.Mock.EmitOctopusEventJson("{\"type\":\"SessionStarted\",\"sessionId\":\"s1\"}");
            Assert.IsInstanceOf<SessionStartedEvent>(received);
            Assert.AreEqual("s1", ((SessionStartedEvent)received).SessionId);
        }
        finally { OctopusSDK.OnOctopusEvent -= h; }
    }
}
