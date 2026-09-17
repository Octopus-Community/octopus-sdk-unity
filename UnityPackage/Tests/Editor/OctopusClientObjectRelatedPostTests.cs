using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class OctopusClientObjectRelatedPostTests
{
    private GameObject _object;
    private OctopusMainThread _drainer;
    private OctopusSDK.OctopusChannel _channel;

    [SetUp]
    public void SetUp()
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        _object = new GameObject("ClientPostTest");
        _drainer = _object.AddComponent<OctopusMainThread>();
        _channel = _object.AddComponent<OctopusSDK.OctopusChannel>();
        Drain();
    }

    [TearDown]
    public void TearDown()
    {
        OctopusSDK.Mock.Reset();
        Drain();
        UnityEngine.Object.DestroyImmediate(_object);
        OctopusSDK.Mock.Enabled = true;
    }

    private void Drain()
    {
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_drainer, null);
    }

    [Test]
    public void PostParsing_NestedCountsNullUnknownAndEscapes()
    {
        var post = OctopusJson.PostFromJson("{\"id\":\"p-雪\\n\\\"1\",\"commentCount\":7,\"viewCount\":19," +
            "\"reactions\":[{\"reactionKind\":\"Heart\",\"count\":4},{\"reactionKind\":\"future\",\"count\":2}]," +
            "\"userReactionKind\":\"Rage\"}");
        Assert.AreEqual("p-雪\n\"1", post.Id);
        Assert.AreEqual(7, post.CommentCount);
        Assert.AreEqual(19, post.ViewCount);
        Assert.AreEqual(OctopusReactionKind.Rage, post.UserReactionKind);
        Assert.AreEqual(OctopusReactionKind.Heart, post.Reactions[0].ReactionKind);
        Assert.AreEqual(4, post.Reactions[0].Count);
        Assert.AreEqual(OctopusReactionKind.Unknown, post.Reactions[1].ReactionKind);
        Assert.AreEqual(2, post.Reactions[1].Count);
        var defaults = OctopusJson.PostFromJson("{\"id\":17,\"viewCount\":\"9\",\"reactions\":[null,5,{}],\"userReactionKind\":null}");
        Assert.AreEqual("", defaults.Id);
        Assert.AreEqual(0, defaults.ViewCount);
        Assert.IsNull(defaults.UserReactionKind);
        Assert.AreEqual(1, defaults.Reactions.Count);
        Assert.AreEqual(OctopusReactionKind.Unknown, defaults.Reactions[0].ReactionKind);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("null")]
    [TestCase("garbage")]
    [TestCase("[]")]
    [TestCase("{\"id\":\"\\uZZZZ\"}")]
    public void PostParsing_InvalidOrNullDoesNotThrow(string json)
    {
        Assert.IsNull(OctopusJson.PostFromJson(json));
    }

    [TestCase("Heart", OctopusReactionKind.Heart)]
    [TestCase("Joy", OctopusReactionKind.Joy)]
    [TestCase("MouthOpen", OctopusReactionKind.MouthOpen)]
    [TestCase("Clap", OctopusReactionKind.Clap)]
    [TestCase("Cry", OctopusReactionKind.Cry)]
    [TestCase("Rage", OctopusReactionKind.Rage)]
    [TestCase("future", OctopusReactionKind.Unknown)]
    public void PostParsing_ReactionKinds(string token, OctopusReactionKind expected)
    {
        Assert.AreEqual(expected, OctopusJson.PostFromJson("{\"userReactionKind\":\"" + token + "\"}").UserReactionKind);
    }

    [Test]
    public void PostSnapshot_CopiesReactionCollection()
    {
        var reactions = new List<OctopusReactionCount> { new OctopusReactionCount(OctopusReactionKind.Joy, 2) };
        var post = new OctopusPost("p", reactions);
        reactions.Clear();
        Assert.AreEqual(1, post.Reactions.Count);
        Assert.AreEqual(2, post.Reactions[0].Count);
    }

    [Test]
    public void ErrorParsing_AllFlutterCasesAndUnknownFallback()
    {
        var wires = new[] { "other", "textMissing", "textTooLong", "fileEmpty", "fileTooLarge", "fileBadFormat",
            "fileUpload", "fileDownload", "missingObjectId", "missingCta", "postUnavailable", "postNotFound",
            "postAlreadyExists", "invalidGroupId", "invalidAuthor", "tokenInvalid", "tokenExpired" };
        var codes = (OctopusClientPostErrorCode[])Enum.GetValues(typeof(OctopusClientPostErrorCode));
        Assert.AreEqual(codes.Length, wires.Length);
        for (int i = 0; i < wires.Length; i++)
        {
            var error = OctopusJson.ClientPostErrorFromJson("{\"type\":\"" + wires[i] + "\",\"message\":\"detail\\nnext\"}");
            Assert.AreEqual(codes[i], error.Code, wires[i]);
            Assert.AreEqual("detail\nnext", error.Message);
        }
        foreach (string json in new[] { null, "", "{}", "{\"type\":\"future\"}", "{\"type\":\"\\uZZZZ\"}" })
        {
            var error = OctopusJson.ClientPostErrorFromJson(json);
            Assert.AreEqual(OctopusClientPostErrorCode.Other, error.Code);
            Assert.IsNotEmpty(error.Message);
        }
    }

    [Test]
    public void ClientObjectWire_PreservesContentAndOmitsSigner()
    {
        var content = new OctopusClientObject { ObjectId = "item-雪", Text = "line\n\"two\"\b\f\u0001", GroupId = "group",
            CatchPhrase = "Discuss", ViewObjectButtonText = "Read", ImagePath = "/tmp/picture.png",
            SignBridgeShare = _ => Task.FromResult("unused") };
        var row = OctopusJson.ParseObject(OctopusJson.ClientObjectToJson(content));
        Assert.AreEqual(content.ObjectId, row["objectId"]);
        Assert.AreEqual(content.Text, row["text"]);
        Assert.AreEqual(content.ImagePath, row["imagePath"]);
        Assert.AreEqual("", row["imageUrl"]);
        Assert.AreEqual("group", row["groupId"]);
        Assert.AreEqual("Discuss", row["catchPhrase"]);
        Assert.AreEqual("Read", row["viewObjectButtonText"]);
        Assert.AreEqual(7, row.Count);
    }

    [Test]
    public void NativeResults_CorrelateOutOfOrderAndCompleteOnceOnUnityThread()
    {
        var calls = new List<string>();
        int mainThread = Thread.CurrentThread.ManagedThreadId, receivedThread = -1;
        int first = OctopusSDK.RegisterClientPostCallbacks(calls.Add, e => calls.Add("wrong"));
        int second = OctopusSDK.RegisterClientPostCallbacks(_ => calls.Add("wrong"), e =>
        {
            receivedThread = Thread.CurrentThread.ManagedThreadId;
            calls.Add(e.Code.ToString());
        });
        Task.Run(() =>
        {
            OctopusSDK.ReceiveClientPostResult(second + "\n{\"type\":\"postUnavailable\"}", true);
            OctopusSDK.ReceiveClientPostResult(first + "\npost-id", false);
        }).Wait();
        _channel.OnClientPostResult(first + "\nignored");
        _channel.OnClientPostError(second + "\n{}");
        _channel.OnClientPostResult(null);
        _channel.OnClientPostError("bad\n{}");
        Assert.IsEmpty(calls);
        Drain();
        CollectionAssert.AreEqual(new[] { "PostUnavailable", "post-id" }, calls);
        Assert.AreEqual(mainThread, receivedThread);
    }

    [Test]
    public void Observation_InitialNullMultipleObjectsReplacementAndStop()
    {
        var events = new List<string>();
        Action<string, OctopusPost> handler = (id, post) => events.Add(id + ":" + (post == null ? "null" : post.Id));
        OctopusSDK.OnClientObjectRelatedPostChanged += handler;
        try
        {
            OctopusSDK.StartObservingClientObjectRelatedPost("a");
            OctopusSDK.StartObservingClientObjectRelatedPost("a");
            OctopusSDK.StartObservingClientObjectRelatedPost("b");
            Drain();
            CollectionAssert.AreEqual(new[] { "a:null", "b:null" }, events);
            events.Clear();
            Task.Run(() => OctopusSDK.ReceiveClientObjectRelatedPost("a\n{\"id\":\"native\"}")).Wait();
            _channel.OnClientObjectRelatedPostChanged("b\nnull");
            _channel.OnClientObjectRelatedPostChanged("missing\n{}");
            _channel.OnClientObjectRelatedPostChanged("malformed");
            Assert.IsEmpty(events);
            Drain();
            CollectionAssert.AreEqual(new[] { "a:native", "b:null" }, events);
            events.Clear();
            OctopusSDK.Mock.SetClientObjectRelatedPost("a", new OctopusPost("stale"));
            OctopusSDK.StopObservingClientObjectRelatedPost("a");
            OctopusSDK.Mock.SetClientObjectRelatedPost("a", new OctopusPost("latest"));
            OctopusSDK.StartObservingClientObjectRelatedPost("a");
            OctopusSDK.Mock.SetClientObjectRelatedPost("b", new OctopusPost("other"));
            Drain();
            CollectionAssert.AreEqual(new[] { "a:latest", "b:other" }, events);
            OctopusSDK.StopObservingClientObjectRelatedPost("a");
            OctopusSDK.StopObservingClientObjectRelatedPost("a");
        }
        finally { OctopusSDK.OnClientObjectRelatedPostChanged -= handler; }
    }

    [Test]
    public void ObservationBoundary_DropsLateNativeMessagesUntilCurrentStartIsAcknowledged()
    {
        var ids = new List<string>();
        Action<string, OctopusPost> handler = (id, post) => ids.Add(post == null ? "null" : post.Id);
        OctopusSDK.OnClientObjectRelatedPostChanged += handler;
        try
        {
            int oldGeneration = OctopusSDK.RegisterClientPostObservation("item");
            _channel.OnClientPostObservationStarted("item\n" + oldGeneration);
            _channel.OnClientObjectRelatedPostChanged("item\n{\"id\":\"old\"}");
            OctopusSDK.ClearClientPostSession();
            int generation = OctopusSDK.RegisterClientPostObservation("item");
            _channel.OnClientPostObservationStarted("item\n" + oldGeneration);
            _channel.OnClientObjectRelatedPostChanged("item\n{\"id\":\"late\"}");
            _channel.OnClientPostObservationStarted("item\n" + generation);
            _channel.OnClientObjectRelatedPostChanged("item\n{\"id\":\"current\"}");
            Drain();
            CollectionAssert.AreEqual(new[] { "current" }, ids);
        }
        finally { OctopusSDK.OnClientObjectRelatedPostChanged -= handler; }
    }

    [Test]
    public void Mock_IdempotentFetchTypedErrorsAndDisabledCompletion()
    {
        var content = new OctopusClientObject { ObjectId = "item", Text = "A discussion about an item" };
        var ids = new List<string>();
        OctopusSDK.FetchOrCreateClientObjectRelatedPost(content, ids.Add, e => Assert.Fail(e.Message));
        content.Text = ""; // Existing content is never rewritten or revalidated.
        OctopusSDK.FetchOrCreateClientObjectRelatedPost(content, ids.Add, e => Assert.Fail(e.Message));
        Assert.IsEmpty(ids);
        Drain();
        CollectionAssert.AreEqual(new[] { "mock-post-item", "mock-post-item" }, ids);
        var errors = new List<OctopusClientPostErrorCode>();
        OctopusSDK.Mock.NextClientPostError = new OctopusClientPostError(OctopusClientPostErrorCode.TokenExpired, "Expired");
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.FetchOrCreateClientObjectRelatedPost(content, _ => Assert.Fail("Unexpected success"), e => errors.Add(e.Code));
        OctopusSDK.FetchOrCreateClientObjectRelatedPost(content, ids.Add, e => Assert.Fail(e.Message));
        content.ObjectId = "new";
        OctopusSDK.FetchOrCreateClientObjectRelatedPost(content, _ => Assert.Fail("Unexpected success"), e => errors.Add(e.Code));
        Drain();
        CollectionAssert.AreEqual(new[] { OctopusClientPostErrorCode.TokenExpired, OctopusClientPostErrorCode.TextMissing }, errors);
        Assert.AreEqual(3, ids.Count);
        Assert.IsNull(OctopusSDK.Mock.NextClientPostError);
    }

    [TestCase("reset")]
    [TestCase("stop")]
    [TestCase("switch")]
    [TestCase("initialize")]
    [TestCase("mockReset")]
    public void Lifecycle_CancelsQueuedResultsAndObservations(string operation)
    {
        int events = 0, results = 0, errors = 0;
        Action<string, OctopusPost> handler = (id, post) => events++;
        OctopusSDK.OnClientObjectRelatedPostChanged += handler;
        try
        {
            OctopusSDK.StartObservingClientObjectRelatedPost("item");
            OctopusSDK.FetchOrCreateClientObjectRelatedPost(new OctopusClientObject { ObjectId = "item", Text = "Discussion text" },
                _ => results++, e => { Assert.AreEqual(OctopusClientPostErrorCode.Other, e.Code); errors++; });
            switch (operation)
            {
                case "reset": OctopusSDK.Reset(); break;
                case "stop": OctopusSDK.Stop(); break;
                case "switch": OctopusSDK.SwitchCommunity("YOUR_API_KEY", ConnectionMode.OctopusAuth()); break;
                case "initialize": OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.OctopusAuth()); break;
                default: OctopusSDK.Mock.Reset(); break;
            }
            Drain();
            Assert.AreEqual(0, results);
            Assert.AreEqual(0, events);
            Assert.AreEqual(1, errors);
            Assert.IsEmpty(OctopusSDK.Mock.ClientPosts);
        }
        finally { OctopusSDK.OnClientObjectRelatedPostChanged -= handler; }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("a\nb")]
    [TestCase("a\rb")]
    public void InvalidIds_ReportTypedErrorAndCannotStartObservation(string id)
    {
        OctopusClientPostError error = null;
        OctopusSDK.FetchOrCreateClientObjectRelatedPost(new OctopusClientObject { ObjectId = id }, null, e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.AreEqual(OctopusClientPostErrorCode.MissingObjectId, error.Code);
        Assert.Throws<ArgumentException>(() => OctopusSDK.StartObservingClientObjectRelatedPost(id));
        OctopusSDK.StopObservingClientObjectRelatedPost(id);
    }

    [Test]
    public void Signers_AreRequestScopedRunOffUnityThreadAndFailClosed()
    {
        int mainThread = Thread.CurrentThread.ManagedThreadId;
        int first = OctopusSDK.RegisterClientPostCallbacks(null, null, fp =>
        {
            Assert.AreNotEqual(mainThread, Thread.CurrentThread.ManagedThreadId);
            return Task.FromResult("first:" + fp);
        });
        int second = OctopusSDK.RegisterClientPostCallbacks(null, null, fp => Task.FromResult("second:" + fp));
        int failed = OctopusSDK.RegisterClientPostCallbacks(null, null, fp => { throw new InvalidOperationException(); });
        Assert.AreEqual("first:a", OctopusSDK.SignClientPost(first, "a").Result);
        Assert.AreEqual("second:b", OctopusSDK.SignClientPost(second, "b").Result);
        Assert.AreEqual("", OctopusSDK.SignClientPost(failed, "c").Result);
        OctopusSDK.ClearClientPostSession();
        Assert.AreEqual("", OctopusSDK.SignClientPost(first, "a").Result);
    }
}
