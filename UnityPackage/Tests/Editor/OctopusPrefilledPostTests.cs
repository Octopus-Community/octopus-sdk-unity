using System;
using System.Collections.Generic;
using NUnit.Framework;

public class OctopusPrefilledPostTests
{
    [Test]
    public void ToArgs_NullPrefilled_AllEmpty()
    {
        OctopusPrefilledPostMarshal.ToArgs(null, out string text, out string topicId,
            out string imagePathsJson, out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("", text);
        Assert.AreEqual("", topicId);
        Assert.AreEqual("[]", imagePathsJson);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_NullFields_BecomeEmptyStrings()
    {
        var p = new OctopusPrefilledPost(); // all fields null by default
        OctopusPrefilledPostMarshal.ToArgs(p, out string text, out string topicId,
            out string imagePathsJson, out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("", text);
        Assert.AreEqual("", topicId);
        Assert.AreEqual("[]", imagePathsJson);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_PopulatedFields_PassedThrough()
    {
        var p = new OctopusPrefilledPost { Text = "hello", TopicId = "grp_1", ImagePaths = { "/tmp/a.png" } };
        OctopusPrefilledPostMarshal.ToArgs(p, out string text, out string topicId,
            out string imagePathsJson, out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("hello", text);
        Assert.AreEqual("grp_1", topicId);
        Assert.AreEqual("[\"/tmp/a.png\"]", imagePathsJson);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_BothCtaFieldsSet_PassedThrough()
    {
        var p = new OctopusPrefilledPost
        {
            CtaLocalizedLabel = OctopusLocalizedText.AutoTranslated("Shop \"now\""), CtaUrl = "https://x.test/p"
        };
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _,
            out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("{\"text\":\"Shop \\\"now\\\"\"}", ctaLabel);
        Assert.AreEqual("https://x.test/p", ctaUrl);
    }

    [Test]
    public void ToArgs_PerLanguageCta_TravelsAsJson()
    {
        var p = new OctopusPrefilledPost
        {
            CtaLocalizedLabel = OctopusLocalizedText.PerLanguage(
                new Dictionary<string, string> { { "fr", "Acheter" }, { "en", "Shop now" } }, "en"),
            CtaUrl = "https://x.test/p"
        };
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _,
            out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("{\"defaultLanguage\":\"en\",\"texts\":{\"en\":\"Shop now\",\"fr\":\"Acheter\"}}", ctaLabel);
        Assert.AreEqual("https://x.test/p", ctaUrl);
    }

#pragma warning disable 0618 // the deprecated plain CTA label
    [Test]
    public void ToArgs_DeprecatedCtaLabelIsTheAutoTranslatedFallback()
    {
        var p = new OctopusPrefilledPost { CtaLabel = "Shop now", CtaUrl = "https://x.test/p" };
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _, out string ctaLabel, out _);
        Assert.AreEqual("{\"text\":\"Shop now\"}", ctaLabel);

        p.CtaLocalizedLabel = OctopusLocalizedText.PerLanguage(
            new Dictionary<string, string> { { "en", "Shop now" }, { "fr", "Acheter" } }, "fr");
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _, out ctaLabel, out _);
        Assert.AreEqual("{\"defaultLanguage\":\"fr\",\"texts\":{\"en\":\"Shop now\",\"fr\":\"Acheter\"}}", ctaLabel);

        p.CtaLocalizedLabel = null;
        ref string legacy = ref p.CtaLabel;
        legacy = "";
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _, out ctaLabel, out string ctaUrl);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
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

#pragma warning restore 0618

    [Test]
    public void ToArgs_OnlyCtaUrl_DropsCta()
    {
        var p = new OctopusPrefilledPost { CtaUrl = "https://x.test/p" }; // no label
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out _,
            out string ctaLabel, out string ctaUrl);
        Assert.AreEqual("", ctaLabel);
        Assert.AreEqual("", ctaUrl);
    }

    [Test]
    public void ToArgs_ImagePaths_KeepOrderDropEmptyAndEscape()
    {
        var p = new OctopusPrefilledPost { ImagePaths = { "/tmp/a.png", null, "", "/tmp/b \"c\"\\d.png" } };
        OctopusPrefilledPostMarshal.ToArgs(p, out _, out _, out string imagePathsJson, out _, out _);
        Assert.AreEqual("[\"/tmp/a.png\",\"/tmp/b \\\"c\\\"\\\\d.png\"]", imagePathsJson);
    }

    [Test]
    public void ImagePaths_NullListIsEmpty()
    {
        CollectionAssert.IsEmpty(OctopusPrefilledPostMarshal.ImagePaths(new OctopusPrefilledPost { ImagePaths = null }));
        CollectionAssert.IsEmpty(OctopusPrefilledPostMarshal.ImagePaths(null));
    }

#pragma warning disable 0618 // the deprecated single image
    [Test]
    public void ImagePaths_DeprecatedImagePath_IsTheOnlyImage()
    {
        var p = new OctopusPrefilledPost { ImagePath = "/tmp/legacy.png", ImagePaths = { "", null } };
        CollectionAssert.AreEqual(new[] { "/tmp/legacy.png" }, OctopusPrefilledPostMarshal.ImagePaths(p));
    }

    [Test]
    public void ImagePaths_DeprecatedImagePathWithImagePaths_Throws()
    {
        var p = new OctopusPrefilledPost { ImagePath = "/tmp/legacy.png", ImagePaths = { "/tmp/a.png" } };
        var error = Assert.Throws<ArgumentException>(() => OctopusPrefilledPostMarshal.ImagePaths(p));
        StringAssert.Contains("ImagePaths", error.Message);
    }
#pragma warning restore 0618

    [Test]
    public void ImagePaths_MaxImageCount_IsAccepted()
    {
        Assert.AreEqual(10, OctopusPrefilledPost.MaxImageCount);
        var p = new OctopusPrefilledPost { ImagePaths = Paths(OctopusPrefilledPost.MaxImageCount) };
        Assert.AreEqual(OctopusPrefilledPost.MaxImageCount, OctopusPrefilledPostMarshal.ImagePaths(p).Count);
    }

    [Test]
    public void ImagePaths_OverMaxImageCount_ThrowsTooManyImages()
    {
        var paths = Paths(OctopusPrefilledPost.MaxImageCount + 1);
        paths.Add(""); // not counted
        var p = new OctopusPrefilledPost { ImagePaths = paths };
        var error = Assert.Throws<OctopusPrefilledPostTooManyImagesException>(() => OctopusPrefilledPostMarshal.ImagePaths(p));
        Assert.AreEqual(10, error.Max);
        Assert.AreEqual(11, error.Count);
        Assert.IsInstanceOf<ArgumentException>(error);
        Assert.AreEqual("11 images provided, at most 10 are accepted.", error.Message);
    }

    [Test]
    public void OpenCreatePost_RefusedImages_OpenNothing()
    {
        OctopusSDK.Mock.Enabled = true;
        OctopusSDK.Mock.Reset();
        try
        {
            Assert.Throws<OctopusPrefilledPostTooManyImagesException>(() =>
                OctopusSDK.OpenCreatePost(new OctopusPrefilledPost { ImagePaths = Paths(11), SignBridgeShare = _ => null }));
            Assert.IsNull(OctopusSDK.Mock.LastCall("OpenCreatePost"));
            Assert.IsNull(OctopusSDK.Mock.LastPrefilledPost);
            var accepted = new OctopusPrefilledPost { ImagePaths = Paths(3) };
            OctopusSDK.OpenCreatePost(accepted);
            Assert.AreSame(accepted, OctopusSDK.Mock.LastPrefilledPost);
        }
        finally { OctopusSDK.Mock.Reset(); }
    }

    private static List<string> Paths(int count)
    {
        var paths = new List<string>();
        for (int i = 0; i < count; i++) paths.Add("/tmp/photo-" + i + ".png");
        return paths;
    }
}
