using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers the optional theme colors: the constructor overloads that keep older host code
/// compiling, and what actually crosses the bridge for a partially configured scheme. The mock
/// records the six packed RGBA ints in bridge argument order, so these tests assert the wire
/// values rather than the mere fact that a call happened — 0 is the sentinel both natives read as
/// "keep your own default", and a color left disabled must travel as 0 and nothing else.
/// </summary>
public class OctopusThemeColorSchemeTests
{
    private const int PrimaryIndex = 0;
    private const int PrimaryLowIndex = 1;
    private const int PrimaryHighIndex = 2;
    private const int OnPrimaryIndex = 3;
    private const int LinkIndex = 4;
    private const int BackgroundIndex = 5;

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

    // A settings object of its own, never the shared Resources asset the editor tool persists:
    // ApplyColorSchemeSettings takes the settings it applies, so a test needs no global state.
    private static OctopusThemeSettings NewSettings(
        OctopusThemeSettings.ColorSchemeConfig light,
        OctopusThemeSettings.ColorSchemeConfig dark)
    {
        var settings = ScriptableObject.CreateInstance<OctopusThemeSettings>();
        settings.LightColorSchemeConfig = light ?? new OctopusThemeSettings.ColorSchemeConfig();
        settings.DarkColorSchemeConfig = dark ?? new OctopusThemeSettings.ColorSchemeConfig();
        return settings;
    }

    private static int[] SentChannels(string method)
    {
        var call = OctopusSDK.Mock.LastCall(method);
        Assert.IsTrue(call.HasValue, method + " was not called");
        var args = call.Value.Args;
        Assert.AreEqual(6, args.Length, method + " must carry the six bridge arguments");

        var channels = new int[6];
        for (int i = 0; i < 6; i++)
        {
            channels[i] = (int)args[i];
        }
        return channels;
    }

    private static void AssertOnlyChannelSet(int[] channels, int index, Color expected)
    {
        for (int i = 0; i < channels.Length; i++)
        {
            if (i == index)
            {
                Assert.AreEqual(OctopusSDK.ColorToInt(expected), channels[i],
                    "channel " + i + " must carry the color the host enabled");
            }
            else
            {
                Assert.AreEqual(0, channels[i],
                    "channel " + i + " is disabled and must travel as 0 (native default), not as a color");
            }
        }
    }

    [Test]
    public void FourArgumentConstructor_LeavesOptionalColorsUnset()
    {
        // An assembly compiled against an earlier version of the package binds to this exact
        // signature; it must still resolve, and must not invent values for the new slots.
        var scheme = new OctopusColorScheme(Color.red, Color.green, Color.blue, Color.white);

        Assert.AreEqual(Color.clear, scheme.Link);
        Assert.AreEqual(Color.clear, scheme.Background);
        Assert.AreEqual(Color.red, scheme.Primary);
    }

    [Test]
    public void OptionalColors_CanBeSetIndependently()
    {
        var linkOnly = new OctopusColorScheme(
            Color.red, Color.green, Color.blue, Color.white, link: Color.yellow
        );
        Assert.AreEqual(Color.yellow, linkOnly.Link);
        Assert.AreEqual(Color.clear, linkOnly.Background);

        var backgroundOnly = new OctopusColorScheme(
            Color.red, Color.green, Color.blue, Color.white, background: Color.black
        );
        Assert.AreEqual(Color.clear, backgroundOnly.Link);
        Assert.AreEqual(Color.black, backgroundOnly.Background);
    }

    [Test]
    public void ColorSchemeConfig_OptionalColorAloneCountsAsEnabled()
    {
        var config = new OctopusThemeSettings.ColorSchemeConfig
        {
            linkEnabled = true,
            link = Color.yellow
        };

        Assert.IsTrue(config.HasAnyEnabled, "link alone must be sent to the native SDK");
        Assert.IsFalse(config.HasAnyPrimaryEnabled, "no color of the primary set is enabled");
        Assert.IsFalse(config.IsComplete, "IsComplete only covers the primary set");
    }

    [Test]
    public void LinkOnly_SendsLinkAndLeavesEveryOtherChannelUnset()
    {
        var settings = NewSettings(
            new OctopusThemeSettings.ColorSchemeConfig { linkEnabled = true, link = Color.yellow },
            null);

        OctopusSDK.ApplyColorSchemeSettings(settings);

        AssertOnlyChannelSet(SentChannels("SetLightColorScheme"), LinkIndex, Color.yellow);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("SetDarkColorScheme").HasValue,
            "an all-disabled config must not reach the bridge at all");
    }

    [Test]
    public void BackgroundOnly_SendsBackgroundAndLeavesEveryOtherChannelUnset()
    {
        var settings = NewSettings(
            null,
            new OctopusThemeSettings.ColorSchemeConfig
            {
                backgroundEnabled = true,
                background = Color.black
            });

        OctopusSDK.ApplyColorSchemeSettings(settings);

        // Dark carries the color; light is all-disabled, so nothing is sent for it. This is the
        // mirror of the light-only case above: the two schemes are configured independently.
        AssertOnlyChannelSet(SentChannels("SetDarkColorScheme"), BackgroundIndex, Color.black);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("SetLightColorScheme").HasValue,
            "an all-disabled config must not reach the bridge at all");
    }

    [Test]
    public void SinglePrimaryColor_SendsThatColorOnlyAndLeavesTheRestToTheNativeDefaults()
    {
        // The regression this guards: a partially enabled scheme used to send Color.clear for
        // every disabled slot, which both natives applied literally.
        var settings = NewSettings(
            new OctopusThemeSettings.ColorSchemeConfig { primaryEnabled = true, primary = Color.red },
            new OctopusThemeSettings.ColorSchemeConfig
            {
                onPrimaryEnabled = true,
                onPrimary = Color.green
            });

        OctopusSDK.ApplyColorSchemeSettings(settings);

        AssertOnlyChannelSet(SentChannels("SetLightColorScheme"), PrimaryIndex, Color.red);
        AssertOnlyChannelSet(SentChannels("SetDarkColorScheme"), OnPrimaryIndex, Color.green);
    }

    [Test]
    public void FullyConfiguredScheme_SendsEveryChannel()
    {
        var config = new OctopusThemeSettings.ColorSchemeConfig
        {
            primaryEnabled = true,
            primary = Color.red,
            primaryLowEnabled = true,
            primaryLow = Color.green,
            primaryHighEnabled = true,
            primaryHigh = Color.blue,
            onPrimaryEnabled = true,
            onPrimary = Color.white,
            linkEnabled = true,
            link = Color.yellow,
            backgroundEnabled = true,
            background = Color.black
        };

        OctopusSDK.ApplyColorSchemeSettings(NewSettings(config, config));

        foreach (var method in new[] { "SetLightColorScheme", "SetDarkColorScheme" })
        {
            var channels = SentChannels(method);
            Assert.AreEqual(OctopusSDK.ColorToInt(Color.red), channels[PrimaryIndex]);
            Assert.AreEqual(OctopusSDK.ColorToInt(Color.green), channels[PrimaryLowIndex]);
            Assert.AreEqual(OctopusSDK.ColorToInt(Color.blue), channels[PrimaryHighIndex]);
            Assert.AreEqual(OctopusSDK.ColorToInt(Color.white), channels[OnPrimaryIndex]);
            Assert.AreEqual(OctopusSDK.ColorToInt(Color.yellow), channels[LinkIndex]);
            Assert.AreEqual(OctopusSDK.ColorToInt(Color.black), channels[BackgroundIndex]);
        }
    }

    [Test]
    public void AllDisabled_SendsNothingForEitherScheme()
    {
        OctopusSDK.ApplyColorSchemeSettings(NewSettings(null, null));

        Assert.IsFalse(OctopusSDK.Mock.LastCall("SetLightColorScheme").HasValue);
        Assert.IsFalse(OctopusSDK.Mock.LastCall("SetDarkColorScheme").HasValue);
    }
}
