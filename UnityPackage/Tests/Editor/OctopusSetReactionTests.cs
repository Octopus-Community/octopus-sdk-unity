using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

public class OctopusSetReactionTests
{
    private GameObject _channelObject;
    private OctopusSDK.OctopusChannel _channel;

    private static void Drain()
    {
        var dispatcher = UnityEngine.Object.FindObjectOfType<OctopusMainThread>();
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(dispatcher, null);
    }

    [SetUp]
    public void SetUp()
    {
        OctopusMainThread.EnsureExists();
        Drain();
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        _channelObject = new GameObject("SetReactionTestChannel");
        _channel = _channelObject.AddComponent<OctopusSDK.OctopusChannel>();
    }

    [TearDown]
    public void TearDown()
    {
        Drain();
        UnityEngine.Object.DestroyImmediate(_channelObject);
        OctopusSDK.Mock.Enabled = true;
        OctopusSDK.Mock.Reset();
    }

    [TestCase(OctopusReactionKind.Heart, "heart")]
    [TestCase(OctopusReactionKind.Joy, "joy")]
    [TestCase(OctopusReactionKind.MouthOpen, "mouthOpen")]
    [TestCase(OctopusReactionKind.Clap, "clap")]
    [TestCase(OctopusReactionKind.Cry, "cry")]
    [TestCase(OctopusReactionKind.Rage, "rage")]
    [TestCase(OctopusReactionKind.None, "")]
    public void KindUsesExistingEventWireNames(OctopusReactionKind kind, string expected)
    {
        string wire;
        Assert.IsTrue(OctopusSetReactionParsing.TryKindToWire(kind, out wire));
        Assert.AreEqual(expected, wire);
    }

    [Test]
    public void NullMeansRemoval()
    {
        string wire;
        Assert.IsTrue(OctopusSetReactionParsing.TryKindToWire(null, out wire));
        Assert.AreEqual("", wire);
    }

    [TestCase("unknownReaction", OctopusSetReactionErrorCode.UnknownReaction)]
    [TestCase("postNotFound", OctopusSetReactionErrorCode.PostNotFound)]
    [TestCase("reactionError", OctopusSetReactionErrorCode.ReactionError)]
    [TestCase("futureCase", OctopusSetReactionErrorCode.ReactionError)]
    public void ErrorPreservesTypeAndEscapedMessage(string wireType, OctopusSetReactionErrorCode code)
    {
        var error = OctopusSetReactionParsing.ErrorFromJson(
            "{\"type\":\"" + wireType + "\",\"message\":\"first\\n\\\"second\\\" \\u2764\"}");
        Assert.AreEqual(code, error.Code);
        Assert.AreEqual("first\n\"second\" ❤", error.Message);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-json")]
    [TestCase("{}")]
    [TestCase("{\"message\":\"\\uZZZZ\"}")]
    public void MissingOrMalformedErrorsFallBack(string json)
    {
        var error = OctopusSetReactionParsing.ErrorFromJson(json);
        Assert.AreEqual(OctopusSetReactionErrorCode.ReactionError, error.Code);
        Assert.IsNotEmpty(error.Message);
    }

    [TestCase(OctopusReactionKind.Unknown)]
    [TestCase((OctopusReactionKind)99)]
    [TestCase((OctopusReactionKind)(-1))]
    public void UnknownKindsFailAsynchronouslyWithoutNativeDispatch(OctopusReactionKind kind)
    {
        OctopusSetReactionError error = null;
        OctopusSDK.SetReaction("post-1", kind, () => Assert.Fail("Unexpected success"), e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.AreEqual(OctopusSetReactionErrorCode.UnknownReaction, error.Code);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("SetReaction").HasValue);
    }

    [TestCase(null)]
    [TestCase("")]
    public void EmptyPostFailsWithoutDispatch(string contentId)
    {
        OctopusSetReactionError error = null;
        OctopusSDK.SetReaction(contentId, null, () => Assert.Fail(), e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.AreEqual(OctopusSetReactionErrorCode.PostNotFound, error.Code);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [Test]
    public void NativeResultsCorrelateOutOfOrderAndIgnoreDuplicatesAndMalformedIds()
    {
        var completed = new List<int>();
        OctopusSetReactionError error = null;
        int first = OctopusSDK.RegisterSetReactionCallbacks(() => completed.Add(1), e => error = e);
        int second = OctopusSDK.RegisterSetReactionCallbacks(() => completed.Add(2), e => Assert.Fail());
        foreach (string payload in new[] { null, "", "1", "abc\n", "0\n", "-1\n", "2147483648\n" })
            _channel.OnSetReactionError(payload);
        _channel.OnSetReactionResult((second + 1) + "\n");
        _channel.OnSetReactionResult(second + "\n");
        _channel.OnSetReactionError(first + "\n{\"type\":\"postNotFound\",\"message\":\"Gone\"}");
        _channel.OnSetReactionResult(first + "\n");
        _channel.OnSetReactionError(second + "\n{}");
        Assert.IsNull(error);
        Assert.AreEqual(0, completed.Count);
        Drain();
        CollectionAssert.AreEqual(new[] { 2 }, completed);
        Assert.AreEqual(OctopusSetReactionErrorCode.PostNotFound, error.Code);
        Assert.AreEqual("Gone", error.Message);
    }

    [Test]
    public void BackgroundCompletionWaitsForMainThreadAndCanStartAnotherCall()
    {
        int mainThread = Thread.CurrentThread.ManagedThreadId;
        int callbackThread = 0;
        int completed = 0;
        int id = OctopusSDK.RegisterSetReactionCallbacks(() =>
        {
            callbackThread = Thread.CurrentThread.ManagedThreadId;
            OctopusSDK.SetReaction("post-2", null, () => completed++, null);
        }, e => Assert.Fail());
        var worker = new Thread(() => OctopusSDK.ReceiveSetReactionResult(id + "\n", false));
        worker.Start();
        Assert.IsTrue(worker.Join(5000));
        Assert.AreEqual(0, callbackThread);
        Drain();
        Assert.AreEqual(mainThread, callbackThread);
        Assert.AreEqual(1, completed);
    }

    [Test]
    public void NullCallbacksAreRemovedOnErrorAndSuccess()
    {
        int first = OctopusSDK.RegisterSetReactionCallbacks(null, null);
        int second = OctopusSDK.RegisterSetReactionCallbacks(null, null);
        _channel.OnSetReactionError(first + "\n{}");
        _channel.OnSetReactionResult(second + "\n");
        var callbacks = typeof(OctopusSDK).GetField("_setReactionCallbacks", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        Assert.AreEqual(0, callbacks.GetType().GetProperty("Count").GetValue(callbacks, null));
    }

    [TestCase(OctopusReactionKind.Heart)]
    [TestCase(OctopusReactionKind.None)]
    [TestCase(null)]
    public void MockRecordsOriginalArgumentsAndCompletesLater(OctopusReactionKind? kind)
    {
        int completed = 0;
        OctopusSDK.SetReaction("post-1", kind, () => completed++, e => Assert.Fail(e.Message));
        var call = OctopusSDK.Mock.LastCall("SetReaction").Value;
        Assert.AreEqual("post-1", call.Args[0]);
        Assert.AreEqual(kind, call.Args[1]);
        Assert.AreEqual(0, completed);
        Drain();
        Assert.AreEqual(1, completed);
    }

    [TestCase(OctopusSetReactionErrorCode.UnknownReaction)]
    [TestCase(OctopusSetReactionErrorCode.PostNotFound)]
    [TestCase(OctopusSetReactionErrorCode.ReactionError)]
    public void MockFailureIsConsumedOnceEvenWhenDisabled(OctopusSetReactionErrorCode code)
    {
        OctopusSDK.Mock.Enabled = false;
        var expected = new OctopusSetReactionError(code, "Simulated failure");
        OctopusSDK.Mock.NextSetReactionError = expected;
        OctopusSetReactionError error = null;
        int completed = 0;
        OctopusSDK.SetReaction("post-1", OctopusReactionKind.Heart, () => Assert.Fail(), e => error = e);
        OctopusSDK.SetReaction("post-2", null, () => completed++, e => Assert.Fail());
        Assert.IsNull(error);
        Assert.IsNull(OctopusSDK.Mock.NextSetReactionError);
        Drain();
        Assert.AreSame(expected, error);
        Assert.AreEqual(1, completed);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [Test]
    public void MockResetClearsInjectedError()
    {
        OctopusSDK.Mock.NextSetReactionError = new OctopusSetReactionError(
            OctopusSetReactionErrorCode.ReactionError, "Old failure");
        OctopusSDK.Mock.Reset();
        Assert.IsNull(OctopusSDK.Mock.NextSetReactionError);
    }
}
