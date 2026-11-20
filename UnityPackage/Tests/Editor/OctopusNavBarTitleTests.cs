using NUnit.Framework;
using UnityEngine;

public class OctopusNavBarTitleTests
{
    private bool _mockWasEnabled;

    [SetUp]
    public void SetUp()
    {
        _mockWasEnabled = OctopusSDK.Mock.Enabled;
        OctopusSDK.Mock.Enabled = true;
        OctopusSDK.Mock.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = _mockWasEnabled;
    }

    [Test]
    public void LegacyConstructorKeepsAllSevenSlotsAndDefaultsTitleToNull()
    {
        var font = new OctopusFont("body_font", "BodyFont", 16);
        var fonts = new OctopusFonts(font, font, font, font, font, font, font);
        Assert.IsNull(fonts.NavBarTitle);
        Assert.AreSame(font, fonts.NavBarItem);
        Assert.AreSame(font, fonts.Title1);
    }

    [Test]
    public void SetThemeRecordsAndroidTitleNameAndSizeAndClearsThemOnReset()
    {
        var title = new OctopusFont("title_font", "IgnoredOnIOS", 24, 700);
        var fonts = new OctopusFonts { NavBarTitle = title };
        OctopusSDK.SetTheme(fonts: fonts);
        Assert.AreSame(fonts, OctopusSDK.Mock.LastCall("SetFonts").Value.Args[0]);
        CollectionAssert.AreEqual(new object[] { "title_font", 24f },
            OctopusSDK.Mock.LastCall("SetNavBarTitle").Value.Args);
        Assert.AreEqual("[]", OctopusSDK.FontWeightsToJson(fonts));

        OctopusSDK.SetFonts();
        CollectionAssert.AreEqual(new object[] { "", 0f },
            OctopusSDK.Mock.LastCall("SetNavBarTitle").Value.Args);
    }

    [Test]
    public void DisabledMockDoesNotRecordTitle()
    {
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.SetFonts(new OctopusFonts { NavBarTitle = new OctopusFont("", "", 24) });
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [Test]
    public void ExistingSettingsKeepEveryFontStyleWhenTitleSlotIsAdded()
    {
        var settings = ScriptableObject.CreateInstance<OctopusThemeSettings>();
        try
        {
            var legacy = new OctopusThemeSettings.FontStyleConfig[7];
            for (int i = 0; i < legacy.Length; i++)
                legacy[i] = new OctopusThemeSettings.FontStyleConfig(true, i, 10 + i);
            settings.FontStyles = legacy;
            var migrated = settings.FontStyles;
            Assert.AreEqual(8, migrated.Length);
            for (int i = 0; i < legacy.Length; i++) Assert.AreSame(legacy[i], migrated[i]);
            Assert.IsFalse(migrated[7].enabled);
            Assert.AreEqual(-1, migrated[7].fontIndex);
        }
        finally { Object.DestroyImmediate(settings); }
    }

    [TestCase(-1, "", 24)]
    [TestCase(0, "title_font", 24)]
    [TestCase(0, "title_font", 0)]
    public void ThemeSettingsForwardTitleFontOrSizeIndependently(int fontIndex, string name, float size)
    {
        var settings = ScriptableObject.CreateInstance<OctopusThemeSettings>();
        try
        {
            settings.FontFileNames = new[] { "Title-Font.ttf" };
            settings.FontStyles[7] = new OctopusThemeSettings.FontStyleConfig(true, fontIndex, size);
            OctopusSDK.ApplyFontSettings(settings);
            CollectionAssert.AreEqual(new object[] { name, size },
                OctopusSDK.Mock.LastCall("SetNavBarTitle").Value.Args);
        }
        finally { Object.DestroyImmediate(settings); }
    }
}
