using System;
using NUnit.Framework;
using UnityEngine;

public class OctopusSampleThemePersistenceTests
{
    private string _key;
    private OctopusSampleTheme _previousTheme;

    [SetUp]
    public void SetUp()
    {
        _key = "OctopusSample.Tests.Theme." + Guid.NewGuid().ToString("N");
        _previousTheme = OctopusSampleBranding.Theme;
        OctopusSampleBranding.RestoreTheme(_key);
    }

    [TearDown]
    public void TearDown()
    {
        OctopusSampleBranding.ApplyLaunchTheme(null);
        OctopusSampleBranding.RestoreTheme(null);
        OctopusSampleBranding.Theme = _previousTheme;
        PlayerPrefs.DeleteKey(_key);
        PlayerPrefs.Save();
    }

    [Test]
    public void FreshLaunchDefaultsToDarkWithoutWritingAPreference()
    {
        Assert.AreEqual(OctopusSampleTheme.Dark, OctopusSampleBranding.Theme);
        Assert.AreEqual(OctopusSamplePalette.Dark().Page, OctopusSampleBranding.Palette.Page);
        Assert.IsFalse(PlayerPrefs.HasKey(_key));
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void AChangedChoiceIsSavedAndRestoredWithItsPalette(OctopusSampleTheme choice)
    {
        // Exercise an actual change for each value, including returning from Light to Dark.
        OctopusSampleBranding.Theme = choice == OctopusSampleTheme.Light
            ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
        OctopusSampleBranding.Theme = choice;
        Assert.AreEqual((int)choice, PlayerPrefs.GetInt(_key, -1));

        // Forget process state while retaining the preference, as a fresh player does.
        OctopusSampleBranding.RestoreTheme(null);
        OctopusSampleBranding.Theme = choice == OctopusSampleTheme.Light
            ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
        var announced = 0;
        Action listener = () => announced++;
        OctopusSampleBranding.ThemeChanged += listener;
        try
        {
            OctopusSampleBranding.RestoreTheme(_key);
            Assert.AreEqual(choice, OctopusSampleBranding.Theme);
            Assert.AreEqual(choice == OctopusSampleTheme.Light ? OctopusSamplePalette.Light().Page
                : OctopusSamplePalette.Dark().Page, OctopusSampleBranding.Palette.Page);
            Assert.AreEqual(1, announced);
            OctopusSampleBranding.RestoreTheme(_key);
            Assert.AreEqual(1, announced, "Restoring the same choice must not rebuild screens again.");
        }
        finally { OctopusSampleBranding.ThemeChanged -= listener; }
    }

    [Test]
    public void ThePersistedNumbersAreAStoredContract()
    {
        Assert.AreEqual(0, (int)OctopusSampleTheme.Light);
        Assert.AreEqual(1, (int)OctopusSampleTheme.Dark);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AQaLaunchThemeWinsOverTheSavedChoiceInEitherOrderAndIsNeverSaved(bool launchFirst)
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        Assert.AreEqual((int)OctopusSampleTheme.Light, PlayerPrefs.GetInt(_key, -1));
        OctopusSampleBranding.RestoreTheme(null);

        // Both run before the first scene, in an order Unity leaves undefined.
        var request = new OctopusSampleQaRequest(OctopusSampleQaLaunchOptions.Parse(
            new System.Collections.Generic.Dictionary<string, string> { { "qaTheme", "dark" } }));
        if (launchFirst) OctopusSampleBranding.RestoreTheme(_key);
        else
        {
            OctopusSampleBranding.ApplyLaunchTheme(null);
            OctopusSampleBranding.RestoreTheme(_key);
            Assert.AreEqual(OctopusSampleTheme.Light, OctopusSampleBranding.Theme);
            request = new OctopusSampleQaRequest(OctopusSampleQaLaunchOptions.Parse(
                new System.Collections.Generic.Dictionary<string, string> { { "qaTheme", "dark" } }));
        }

        Assert.IsNotNull(request);
        Assert.AreEqual(OctopusSampleTheme.Dark, OctopusSampleBranding.Theme);
        Assert.AreEqual(OctopusSamplePalette.Dark().Page, OctopusSampleBranding.Palette.Page);
        Assert.AreEqual((int)OctopusSampleTheme.Light, PlayerPrefs.GetInt(_key, -1),
            "The launch theme overwrote the tester's saved choice.");
    }

    [Test]
    public void AnInvalidQaLaunchLeavesTheSavedChoiceInForce()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        new OctopusSampleQaRequest(OctopusSampleQaLaunchOptions.Parse(
            new System.Collections.Generic.Dictionary<string, string> { { "qaTheme", "dark" }, { "qaTab", "nope" } }));
        OctopusSampleBranding.RestoreTheme(_key);
        Assert.AreEqual(OctopusSampleTheme.Light, OctopusSampleBranding.Theme);
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void InvalidSavedValuesFallBackToDark(int value)
    {
        PlayerPrefs.SetInt(_key, value);
        PlayerPrefs.Save();
        OctopusSampleBranding.RestoreTheme(null);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        OctopusSampleBranding.RestoreTheme(_key);
        Assert.AreEqual(OctopusSampleTheme.Dark, OctopusSampleBranding.Theme);
        Assert.AreEqual(OctopusSamplePalette.Dark().Page, OctopusSampleBranding.Palette.Page);
        Assert.AreEqual(value, PlayerPrefs.GetInt(_key), "Reading must not rewrite the saved preference.");
    }
}
