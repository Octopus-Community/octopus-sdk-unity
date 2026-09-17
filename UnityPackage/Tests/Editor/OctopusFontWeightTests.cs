using System;
using NUnit.Framework;

public class OctopusFontWeightTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [TearDown]
    public void TearDown() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void LegacyConstructorRetainsFieldsAndOmitsWeight()
    {
        var font = new OctopusFont("native_font", "NativeFont", 18);
        Assert.AreEqual("native_font", font.AndroidFontResourceName);
        Assert.AreEqual("NativeFont", font.IOSFontName);
        Assert.AreEqual(18, font.Size);
        Assert.IsNull(font.FontWeight);
        Assert.AreEqual("[]", OctopusSDK.FontWeightsToJson(new OctopusFonts(body1: font)));
        Assert.IsNull(new OctopusFont("", "", 0, null).FontWeight);
    }

    [TestCase(100)]
    [TestCase(400)]
    [TestCase(550)]
    [TestCase(700)]
    [TestCase(900)]
    public void SerializesNumericWeightOnlyForExplicitSlots(int weight)
    {
        var fonts = new OctopusFonts(title1: new OctopusFont("", "", 0, weight),
            body1: new OctopusFont("", "", 0));
        string json = OctopusSDK.FontWeightsToJson(fonts);
        StringAssert.Contains("\"fontWeight\":" + weight, json);
        var rows = OctopusJson.ParseArray(json);
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("title1", rows[0]["slot"]);
        Assert.AreEqual(weight.ToString(), rows[0]["fontWeight"]);
    }

    [TestCase(0)]
    [TestCase(99)]
    [TestCase(901)]
    [TestCase(int.MaxValue)]
    public void RejectsInvalidWeight(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OctopusFont("", "", 0, weight));
    }

    [Test]
    public void SetThemeDispatchesAllSlotsAndMockRecordsWeights()
    {
        var font = new OctopusFont("", "", 0, 700);
        var fonts = new OctopusFonts(font, font, font, font, font, font, font);
        OctopusSDK.SetTheme(fonts: fonts);
        Assert.AreSame(fonts, OctopusSDK.Mock.LastCall("SetFonts").Value.Args[0]);
        var rows = OctopusJson.ParseArray((string)OctopusSDK.Mock.LastCall("SetFontWeights").Value.Args[0]);
        CollectionAssert.AreEqual(new[] { "title1", "title2", "body1", "body2", "caption1", "caption2", "navBarItem" },
            rows.ConvertAll(row => row["slot"]));
        Assert.IsTrue(rows.TrueForAll(row => row["fontWeight"] == "700"));
    }

    [Test]
    public void SubsequentLegacyOrNullFontsClearWeightPayload()
    {
        OctopusSDK.SetFonts(new OctopusFonts(body1: new OctopusFont("", "", 0, 700)));
        OctopusSDK.SetFonts(new OctopusFonts(body1: new OctopusFont("", "", 0)));
        Assert.AreEqual("[]", OctopusSDK.Mock.LastCall("SetFontWeights").Value.Args[0]);
        OctopusSDK.SetFonts();
        Assert.IsNull(OctopusSDK.Mock.LastCall("SetFonts").Value.Args[0]);
        Assert.AreEqual("[]", OctopusSDK.Mock.LastCall("SetFontWeights").Value.Args[0]);
        OctopusSDK.Mock.Reset();
        Assert.IsFalse(OctopusSDK.Mock.LastCall("SetFontWeights").HasValue);
    }

    [Test]
    public void DisabledMockSuppressesFontRecording()
    {
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.SetFonts(new OctopusFonts(body1: new OctopusFont("", "", 0, 700)));
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }
}
