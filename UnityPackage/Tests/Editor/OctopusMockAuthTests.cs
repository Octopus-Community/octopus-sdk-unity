using System.Threading.Tasks;
using NUnit.Framework;

public class OctopusMockAuthTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void ConnectUser_InvokesTokenProviderOnce_AndCompletes()
    {
        int calls = 0;
        var task = OctopusSDK.ConnectUser("u1", "nick", "bio", "pic",
            () => { calls++; return Task.FromResult("token-123"); });
        Assert.IsTrue(task.Wait(2000), "ConnectUser did not complete (hang)");
        Assert.AreEqual(1, calls);
        Assert.IsTrue(OctopusSDK.Mock.LastCall("ConnectUser").HasValue);
    }

    [Test]
    public void DisconnectUser_Completes()
    {
        var task = OctopusSDK.DisconnectUser();
        Assert.IsTrue(task.Wait(2000), "DisconnectUser did not complete (hang)");
        Assert.IsTrue(OctopusSDK.Mock.LastCall("DisconnectUser").HasValue);
    }
}
