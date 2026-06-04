using NUnit.Framework;

public class OctopusPrefilledPostTests
{
    [Test]
    public void ToArgs_NullPrefilled_AllEmpty()
    {
        OctopusPrefilledPostMarshal.ToArgs(null, out string text, out string topicId, out string imagePath);
        Assert.AreEqual("", text);
        Assert.AreEqual("", topicId);
        Assert.AreEqual("", imagePath);
    }

    [Test]
    public void ToArgs_NullFields_BecomeEmptyStrings()
    {
        var p = new OctopusPrefilledPost(); // all fields null by default
        OctopusPrefilledPostMarshal.ToArgs(p, out string text, out string topicId, out string imagePath);
        Assert.AreEqual("", text);
        Assert.AreEqual("", topicId);
        Assert.AreEqual("", imagePath);
    }

    [Test]
    public void ToArgs_PopulatedFields_PassedThrough()
    {
        var p = new OctopusPrefilledPost { Text = "hello", TopicId = "grp_1", ImagePath = "/tmp/a.png" };
        OctopusPrefilledPostMarshal.ToArgs(p, out string text, out string topicId, out string imagePath);
        Assert.AreEqual("hello", text);
        Assert.AreEqual("grp_1", topicId);
        Assert.AreEqual("/tmp/a.png", imagePath);
    }
}
