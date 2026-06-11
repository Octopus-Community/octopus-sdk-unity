using NUnit.Framework;

public class OctopusMockNavigationTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void OpenPost_RecordsAndSetsScreenAndAccessor()
    {
        OctopusSDK.OpenPost("42");
        Assert.AreEqual("42", OctopusSDK.Mock.LastCall("OpenPost").Value.Args[0]);
        Assert.AreEqual("42", OctopusSDK.Mock.LastOpenedPost);
        StringAssert.Contains("42", OctopusSDK.Mock.CurrentScreen);
    }

    [Test]
    public void Open_NullNotification_ShowsMainFeed()
    {
        OctopusSDK.Open();
        Assert.AreEqual("Main feed", OctopusSDK.Mock.CurrentScreen);
    }

    [Test]
    public void OpenCreatePost_RecordsPrefill()
    {
        var p = new OctopusPrefilledPost { Text = "hi", TopicId = "t1" };
        OctopusSDK.OpenCreatePost(p);
        Assert.AreEqual("hi", OctopusSDK.Mock.LastPrefilledPost.Text);
        Assert.AreEqual("t1", OctopusSDK.Mock.LastPrefilledPost.TopicId);
        var args = OctopusSDK.Mock.LastCall("OpenCreatePost").Value.Args;
        Assert.AreEqual("hi", args[0]);
        Assert.AreEqual("t1", args[1]);
    }

    [Test]
    public void OpenGroup_RecordsGroupId()
    {
        OctopusSDK.OpenGroup("g7");
        Assert.AreEqual("g7", OctopusSDK.Mock.LastCall("OpenGroup").Value.Args[0]);
    }

    [Test]
    public void OpenPost_WhenDisabled_DoesNotRecordOrChangeScreen()
    {
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.OpenPost("99");
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
        Assert.IsNull(OctopusSDK.Mock.CurrentScreen);
    }
}
