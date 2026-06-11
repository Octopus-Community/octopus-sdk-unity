using NUnit.Framework;

public class OctopusPrefilledPostTests
{
    [Test]
    public void ToArgs_NullPrefilled_AllEmpty()
    {
        OctopusPrefilledPostMarshal.ToArgs(null, out string text, out string topicId,
            out string imagePath, out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("", text);
        Assert.AreEqual("", topicId);
        Assert.AreEqual("", imagePath);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_NullFields_BecomeEmptyStrings()
    {
        var p = new OctopusPrefilledPost(); // all fields null by default
        OctopusPrefilledPostMarshal.ToArgs(p, out string text, out string topicId,
            out string imagePath, out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("", text);
        Assert.AreEqual("", topicId);
        Assert.AreEqual("", imagePath);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_PopulatedFields_PassedThrough()
    {
        var p = new OctopusPrefilledPost { Text = "hello", TopicId = "grp_1", ImagePath = "/tmp/a.png" };
        OctopusPrefilledPostMarshal.ToArgs(p, out string text, out string topicId,
            out string imagePath, out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("hello", text);
        Assert.AreEqual("grp_1", topicId);
        Assert.AreEqual("/tmp/a.png", imagePath);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_BothCtaFieldsSet_PassedThrough()
    {
        var p = new OctopusPrefilledPost { CtaLabel = "Shop now", CtaUrl = "https://x.test/p" };
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _,
            out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("Shop now", ctaLabel);
        Assert.AreEqual("https://x.test/p", ctaUrl);
    }

    [Test]
    public void ToArgs_OnlyCtaLabel_DropsCta()
    {
        var p = new OctopusPrefilledPost { CtaLabel = "Shop now" }; // no url
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _,
            out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_OnlyCtaUrl_DropsCta()
    {
        var p = new OctopusPrefilledPost { CtaUrl = "https://x.test/p" }; // no label
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _,
            out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }
}
