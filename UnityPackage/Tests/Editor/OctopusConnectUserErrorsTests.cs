using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class OctopusConnectUserErrorsTests
{
    private GameObject _channelObject;
    private OctopusSDK.OctopusChannel _channel;
    private object _previousProvider;
    private object _previousCompleter;

    private static FieldInfo PrivateField(string name)
    {
        return typeof(OctopusSDK).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
    }

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
        _previousProvider = PrivateField("TokenProvider").GetValue(null);
        _previousCompleter = PrivateField("ConnectUserTaskCompleter").GetValue(null);
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        _channelObject = new GameObject("ConnectUserErrorsTestChannel");
        _channel = _channelObject.AddComponent<OctopusSDK.OctopusChannel>();
    }

    [TearDown]
    public void TearDown()
    {
        Drain();
        UnityEngine.Object.DestroyImmediate(_channelObject);
        OctopusSDK.Mock.Enabled = true;
        OctopusSDK.Mock.Reset();
        PrivateField("TokenProvider").SetValue(null, _previousProvider);
        PrivateField("ConnectUserTaskCompleter").SetValue(null, _previousCompleter);
    }

    [TestCase("missingToken", OctopusClientUserErrorCode.MissingToken)]
    [TestCase("userBanned", OctopusClientUserErrorCode.UserBanned)]
    [TestCase("profileError", OctopusClientUserErrorCode.ProfileError)]
    [TestCase("invalidToken", OctopusClientUserErrorCode.InvalidToken)]
    [TestCase("communityAccessDenied", OctopusClientUserErrorCode.CommunityAccessDenied)]
    [TestCase("other", OctopusClientUserErrorCode.Other)]
    [TestCase("futureCase", OctopusClientUserErrorCode.Other)]
    public void ParsesCodesAndPreservesEscapedMessages(string wireCode, OctopusClientUserErrorCode expected)
    {
        var error = OctopusClientUserErrorParsing.FromJson(
            "{\"code\":\"" + wireCode + "\",\"message\":\"nickname: \\\"taken\\\"\\n\\u2764\"}");
        Assert.AreEqual(expected, error.Code);
        Assert.AreEqual("nickname: \"taken\"\n❤", error.Message);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-json")]
    [TestCase("{}")]
    [TestCase("{\"message\":\"\\uZZZZ\"}")]
    public void MalformedAndMissingPayloadsReturnOther(string json)
    {
        var error = OctopusClientUserErrorParsing.FromJson(json);
        Assert.AreEqual(OctopusClientUserErrorCode.Other, error.Code);
        Assert.IsNotEmpty(error.Message);
    }

    [Test]
    public void PadsCorrelateOutOfOrderIgnoreDuplicatesAndDoNotCompleteLegacyTask()
    {
        var legacy = new TaskCompletionSource<bool>();
        PrivateField("ConnectUserTaskCompleter").SetValue(null, legacy);
        var successes = new List<int>();
        OctopusClientUserError error = null;
        int first = OctopusSDK.RegisterConnectUserCallbacks(() => successes.Add(1), e => error = e);
        int second = OctopusSDK.RegisterConnectUserCallbacks(() => successes.Add(2), e => Assert.Fail(e.Message));
        foreach (string payload in new[] { null, "", "1", "abc\n", "0\n", "-1\n", "2147483648\n" })
            _channel.OnConnectUserFailed(payload);
        _channel.OnConnectUserSucceeded((second + 1) + "\n");
        _channel.OnConnectUserSucceeded(second + "\n");
        _channel.OnConnectUserFailed(first + "\n{\"code\":\"userBanned\",\"message\":\"Reason\"}");
        _channel.OnConnectUserSucceeded(first + "\n");
        _channel.OnConnectUserFailed(second + "\n{}");
        Assert.IsNull(error);
        Assert.AreEqual(0, successes.Count);
        Drain();
        CollectionAssert.AreEqual(new[] { 2 }, successes);
        Assert.AreEqual(OctopusClientUserErrorCode.UserBanned, error.Code);
        Assert.AreEqual("Reason", error.Message);
        Assert.IsFalse(legacy.Task.IsCompleted);
        _channel.OnConnectUserCompleted("");
        Assert.IsTrue(legacy.Task.IsCompleted);
    }

    [Test]
    public void BackgroundFailureDispatchesOnMainThreadAndCanStartAnotherCall()
    {
        int mainThread = Thread.CurrentThread.ManagedThreadId;
        int callbackThread = 0;
        int successes = 0;
        int id = OctopusSDK.RegisterConnectUserCallbacks(() => Assert.Fail(), e =>
        {
            callbackThread = Thread.CurrentThread.ManagedThreadId;
            Assert.AreEqual(OctopusClientUserErrorCode.Other, e.Code);
            OctopusSDK.ConnectUser("user-2", null, null, null, () => Task.FromResult("mock-token"),
                () => successes++, err => Assert.Fail(err.Message));
        });
        var worker = new Thread(() => OctopusSDK.ReceiveConnectUserResult(id + "\nnot-json", true));
        worker.Start();
        Assert.IsTrue(worker.Join(5000));
        Assert.AreEqual(0, callbackThread);
        Drain();
        Assert.AreEqual(mainThread, callbackThread);
        Assert.AreEqual(1, successes);
    }

    [Test]
    public void NullCallbacksAreRemovedForSuccessAndFailure()
    {
        int first = OctopusSDK.RegisterConnectUserCallbacks(null, null);
        int second = OctopusSDK.RegisterConnectUserCallbacks(null, null);
        _channel.OnConnectUserFailed(first + "\n{}");
        _channel.OnConnectUserSucceeded(second + "\n");
        var callbacks = PrivateField("_connectUserCallbacks").GetValue(null);
        Assert.AreEqual(0, callbacks.GetType().GetProperty("Count").GetValue(callbacks, null));
        Drain();
    }

    [Test]
    public void MockSuccessRequestsTokenNormalizesProfileAndCompletesLater()
    {
        int tokens = 0;
        int successes = 0;
        OctopusSDK.ConnectUser("user-1", null, null, null,
            () => { tokens++; return Task.FromResult("mock-token"); },
            () => successes++, e => Assert.Fail(e.Message));
        var call = OctopusSDK.Mock.LastCall("ConnectUser").Value;
        Assert.AreEqual("user-1", call.Args[0]);
        Assert.AreEqual("", call.Args[1]);
        Assert.AreEqual(1, tokens);
        Assert.AreEqual(0, successes);
        Drain();
        Assert.AreEqual(1, successes);
    }

    [TestCase(OctopusClientUserErrorCode.MissingToken)]
    [TestCase(OctopusClientUserErrorCode.UserBanned)]
    [TestCase(OctopusClientUserErrorCode.ProfileError)]
    [TestCase(OctopusClientUserErrorCode.InvalidToken)]
    [TestCase(OctopusClientUserErrorCode.CommunityAccessDenied)]
    [TestCase(OctopusClientUserErrorCode.Other)]
    public void MockFailureIsConsumedOnceEvenWhenDisabled(OctopusClientUserErrorCode code)
    {
        OctopusSDK.Mock.Enabled = false;
        var expected = new OctopusClientUserError(code, "Simulated failure");
        OctopusSDK.Mock.NextConnectUserError = expected;
        OctopusClientUserError error = null;
        int successes = 0;
        OctopusSDK.ConnectUser("user-1", "", "", "", () => Task.FromResult("mock-token"),
            () => Assert.Fail(), e => error = e);
        OctopusSDK.ConnectUser("user-2", "", "", "", () => Task.FromResult("mock-token"),
            () => successes++, e => Assert.Fail(e.Message));
        Assert.IsNull(error);
        Assert.IsNull(OctopusSDK.Mock.NextConnectUserError);
        Drain();
        Assert.AreSame(expected, error);
        Assert.AreEqual(1, successes);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [TestCase(null)]
    [TestCase("")]
    public void EmptyTokenFailsInsteadOfReportingSuccess(string token)
    {
        OctopusClientUserError error = null;
        OctopusSDK.ConnectUser("user-1", null, null, null, () => Task.FromResult(token),
            () => Assert.Fail(), e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.AreEqual(OctopusClientUserErrorCode.MissingToken, error.Code);
    }

    [TestCase(null)]
    [TestCase("")]
    public void MissingUserIdFailsWithoutDispatch(string userId)
    {
        OctopusClientUserError error = null;
        OctopusSDK.ConnectUser(userId, null, null, null, () => Task.FromResult("mock-token"),
            () => Assert.Fail(), e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.AreEqual(OctopusClientUserErrorCode.Other, error.Code);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [Test]
    public void MissingProviderFailsWithoutDispatch()
    {
        OctopusClientUserError error = null;
        OctopusSDK.ConnectUser("user-1", null, null, null, null, () => Assert.Fail(), e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.AreEqual(OctopusClientUserErrorCode.MissingToken, error.Code);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ProviderExceptionsAreOther(bool asynchronous)
    {
        OctopusClientUserError error = null;
        Func<Task<string>> provider = () =>
        {
            if (asynchronous) return Task.FromException<string>(new InvalidOperationException("Provider failed"));
            throw new InvalidOperationException("Provider failed");
        };
        OctopusSDK.ConnectUser("user-1", null, null, null, provider, () => Assert.Fail(), e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.AreEqual(OctopusClientUserErrorCode.Other, error.Code);
        Assert.AreEqual("Provider failed", error.Message);
    }

    [Test]
    public void LegacyTaskStillCompletesAndDoesNotConsumeInjectedTypedError()
    {
        var expected = new OctopusClientUserError(OctopusClientUserErrorCode.UserBanned, "Simulated failure");
        OctopusSDK.Mock.NextConnectUserError = expected;
        Task legacy = OctopusSDK.ConnectUser("user-1", null, null, null, () => Task.FromResult("mock-token"));
        Assert.IsTrue(legacy.IsCompleted);
        Assert.AreSame(expected, OctopusSDK.Mock.NextConnectUserError);
        OctopusSDK.Mock.Reset();
        Assert.IsNull(OctopusSDK.Mock.NextConnectUserError);
    }
}
