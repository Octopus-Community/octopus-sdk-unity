using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using NUnit.Framework;

public class OctopusMockAuthTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    private static void Drain()
    {
        OctopusMainThread.EnsureExists();
        var dispatcher = Object.FindObjectOfType<OctopusMainThread>();
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(dispatcher, null);
    }

    [Test]
    public void ConnectUser_InvokesTokenProviderOnce_AndCompletes()
    {
        int calls = 0;
        var task = OctopusSDK.ConnectUser("u1", "nick", "bio", "pic",
            () => { calls++; return Task.FromResult("token-123"); });
        Drain();
        Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        Assert.AreEqual(1, calls);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("ConnectUser").HasValue);
    }

    [Test]
    public void ConnectUser_AcceptsNullProfileFields_CoalescesToEmpty_AndCompletes()
    {
        // null nickname/bio/picture must be accepted: the bridges can't marshal null (iOS crashes,
        // Android hangs), so ConnectUser coalesces to "". Verify it completes and the recorded
        // nickname is empty, not null.
        var task = OctopusSDK.ConnectUser("u1", null, null, null,
            () => Task.FromResult("token-123"));
        Drain();
        Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        var call = OctopusSDK.Mock.LastCall("ConnectUser");
        Assert.IsTrue(call.HasValue);
        Assert.AreEqual("", call.Value.Args[1], "null nickname should be coalesced to empty string");
    }

    [Test]
    public void DisconnectUser_Completes()
    {
        var task = OctopusSDK.DisconnectUser();
        Assert.IsTrue(task.Wait(2000), "DisconnectUser did not complete (hang)");
        Assert.IsTrue(OctopusSDK.Mock.LastCall("DisconnectUser").HasValue);
    }
}
