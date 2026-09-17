using System.Threading;
using NUnit.Framework;

/// <summary>
/// The sample hands its brand palette to the native SDK once per initialisation and forces the
/// native appearance to follow the sample theme (#134). Everything is asserted through the
/// recording seam: no bridge call, no asset.
/// </summary>
public class OctopusSampleNativeThemeTests
{
    private OctopusRecordingScenarioSdk _sdk;
    private OctopusSampleTheme _theme;
    private SynchronizationContext _previousContext;

    [SetUp]
    public void SetUp()
    {
        _theme = OctopusSampleBranding.Theme;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key", authToken = "not-a-real-token" },
        };
        OctopusScenarioSdk.Use(_sdk);
        OctopusSampleLog.Current = OctopusSampleLog.None;
        // `ConnectAsync` is `async void`: run its continuation inline so nothing is left on the
        // editor's context after the fixture.
        _previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
    }

    [TearDown]
    public void TearDown()
    {
        SynchronizationContext.SetSynchronizationContext(_previousContext);
        OctopusScenarioSdk.Use(null);
        OctopusSampleBranding.Theme = _theme;
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [Test]
    public void LiveThemeAdapterOnlyChangesColorsAndPreservesOtherAssetThemeSettings()
    {
        bool enabled = OctopusSDK.Mock.Enabled;
        try
        {
            OctopusSDK.Mock.Enabled = true;
            int before = OctopusSDK.Mock.Calls.Count;
            new OctopusLiveScenarioSdk().ApplyTheme(OctopusSampleNativeTheme.LightScheme,
                OctopusSampleNativeTheme.DarkScheme);
            Assert.AreEqual(before + 2, OctopusSDK.Mock.Calls.Count);
            Assert.AreEqual("SetLightColorScheme", OctopusSDK.Mock.Calls[before].Method);
            Assert.AreEqual("SetDarkColorScheme", OctopusSDK.Mock.Calls[before + 1].Method);
        }
        finally { OctopusSDK.Mock.Enabled = enabled; }
    }

    [Test]
    public void SchemesCarryTheBrandColorsOfEachAppearance()
    {
        var light = OctopusSampleNativeTheme.LightScheme;
        Assert.AreEqual(OctopusSampleBranding.SdkLightPrimaryMain, light.Primary);
        Assert.AreEqual(OctopusSampleBranding.SdkLightPrimaryLow, light.PrimaryLow);
        Assert.AreEqual(OctopusSampleBranding.SdkLightPrimaryHigh, light.PrimaryHigh);
        Assert.AreEqual(OctopusSampleBranding.SdkLightOnPrimary, light.OnPrimary);
        var dark = OctopusSampleNativeTheme.DarkScheme;
        Assert.AreEqual(OctopusSampleBranding.SdkDarkPrimaryMain, dark.Primary);
        Assert.AreEqual(OctopusSampleBranding.SdkDarkPrimaryLow, dark.PrimaryLow);
        Assert.AreEqual(OctopusSampleBranding.SdkDarkPrimaryHigh, dark.PrimaryHigh);
        Assert.AreEqual(OctopusSampleBranding.SdkDarkOnPrimary, dark.OnPrimary);
        Assert.AreNotEqual(light.Primary, dark.Primary, "Both appearances share one primary.");
    }

    [Test]
    public void SchemeTypeIsForcedNeverSystem()
    {
        Assert.AreEqual(1, OctopusSampleNativeTheme.SchemeTypeFor(OctopusSampleTheme.Light));
        Assert.AreEqual(2, OctopusSampleNativeTheme.SchemeTypeFor(OctopusSampleTheme.Dark));
    }

    [Test]
    public void InitialisationSendsBothSchemesThenTheCurrentAppearance()
    {
        Initialise();
        CollectionAssert.AreEqual(new[] { "Initialize", "ConnectUser" }, _sdk.ScenarioMethods,
            "Theme plumbing must not show up as a scenario step.");
        var theme = _sdk.ThemeCalls;
        Assert.AreEqual(2, theme.Count, "Expected ApplyTheme then SetColorSchemeType.");
        Assert.AreEqual("ApplyTheme", theme[0].Method);
        Assert.AreEqual(OctopusSampleBranding.SdkLightPrimaryMain, ((OctopusColorScheme)theme[0].Args[0]).Primary);
        Assert.AreEqual(OctopusSampleBranding.SdkDarkPrimaryMain, ((OctopusColorScheme)theme[0].Args[1]).Primary);
        Assert.AreEqual("SetColorSchemeType", theme[1].Method);
        Assert.AreEqual(1, theme[1].Args[0]);
        Assert.AreEqual("Initialize", _sdk.Calls[0].Method, "The theme must be sent after Initialize.");
        Assert.AreEqual("ApplyTheme", _sdk.Calls[1].Method, "The theme must be sent before the first scenario call.");
    }

    [Test]
    public void AppearanceChangesReachTheSdkOnlyOnceInitialised()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        Assert.AreEqual(0, _sdk.ThemeCalls.Count, "Nothing to update before an initialisation.");

        Initialise();
        Assert.AreEqual(2, _sdk.ThemeCalls[1].Args[0], "The dark appearance chosen before initialising was lost.");

        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        var calls = _sdk.ThemeCalls;
        Assert.AreEqual(3, calls.Count);
        Assert.AreEqual("SetColorSchemeType", calls[2].Method);
        Assert.AreEqual(1, calls[2].Args[0]);

        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        // Integration with Branding.Theme: its setter suppresses the unchanged-value event.
        Assert.AreEqual(3, _sdk.ThemeCalls.Count, "An unchanged theme must not be re-sent.");
    }

    [Test]
    // Integration with ScenarioSdk.Use: swapping resets initialization until the new SDK starts.
    public void SwappingTheSdkStopsUpdatingTheOldOne()
    {
        Initialise();
        var replacement = new OctopusRecordingScenarioSdk();
        OctopusScenarioSdk.Use(replacement);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        Assert.AreEqual(2, _sdk.ThemeCalls.Count, "The replaced SDK still receives theme updates.");
        Assert.AreEqual(0, replacement.ThemeCalls.Count, "A swap resets the initialised state.");
    }

    /// <summary>Initialises through the public path a user takes: the first Connection preset.</summary>
    private static void Initialise()
    {
        var pilot = new ConnectionScenario();
        var preset = pilot.Presets[0];
        preset.Fill(pilot.Fields);
        preset.Run(pilot.Fields);
        Assert.IsTrue(OctopusScenarioSdk.IsInPilotMode, "The preset tap did not initialise the sample.");
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state) { d(state); }
    }
}
