using System.Collections.Generic;
using UnityEngine;

public sealed class ThemeScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("primary", "Primary colors", true),
        new OctopusScenarioField("background", "Background", true),
        new OctopusScenarioField("link", "Link", true),
        new OctopusScenarioField("navBarItem", "Nav-bar item size", true),
        new OctopusScenarioField("logo", "Bundled native logo", true));
    private readonly List<OctopusScenarioPreset> _presets;

    public ThemeScenario() : base("theme")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Preset(1, "Octopus navy", true, false, true),
            Preset(2, "Custom theme (brand colors + logo)", true, false, true),
            Preset(3, "Surface keys (background + link + nav-bar item)", true, true, true),
            Preset(4, "Surface keys only (no primary colors)", false, true, false),
            Preset(5, "SDK default (no theme)", false, false, false),
        };
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols
    {
        get { return new[] { "SetLightColorScheme", "SetDarkColorScheme", "SetLogo", "SetFonts" }; }
    }
    public override string ParameterNotice
    {
        get { return "Custom presets reuse the bundled blueberry logo: Unity takes native resource names, " +
                     "not Flutter's base64 logo. Nav-bar item size is iOS only. Unset colors and fonts " +
                     "use SDK defaults. Open Community to verify."; }
    }

    private OctopusScenarioPreset Preset(int index, string label, bool brand, bool surface, bool logo)
    {
        return new OctopusScenarioPreset(PresetTestId(index), PresetLabel(index, label), fields =>
        {
            fields.Set("primary", brand ? "brand" : "unset");
            fields.Set("background", surface ? "#1B1035" : "unset");
            fields.Set("link", surface ? "#FFC857" : "unset");
            fields.Set("navBarItem", brand && surface ? "22" : "unset");
            fields.Set("logo", logo ? "theme_blueberry_logo" : "unset");
        }, Apply);
    }

    private void Apply(OctopusScenarioFields fields)
    {
        string reason;
        if (OctopusScenarioSdk.EnsurePilotInitialized(out reason) == null) { Report(reason); return; }
        var brand = fields.Get("primary") == "brand";
        var light = Scheme(brand ? OctopusSampleNativeTheme.LightScheme : null, fields);
        var dark = Scheme(brand ? OctopusSampleNativeTheme.DarkScheme : null, fields);
        var logoName = fields.Get("logo");
        var logo = logoName == "unset" ? null : new OctopusLogo(logoName, "Data/Raw/" + logoName + ".png");
        var fonts = fields.Get("navBarItem") == "22"
            ? new OctopusFonts(navBarItem: new OctopusFont("", "", 22)) : null;
        var sdk = OctopusScenarioSdk.Current;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetLightColorScheme", "scenario colors");
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetDarkColorScheme", "scenario colors");
        // A non-null, all-clear scheme is required: null skips the native setter entirely.
        sdk.ApplyTheme(light, dark);
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetLogo", logoName);
        sdk.SetLogo(logo);
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetFonts", "navBarItem=" + fields.Get("navBarItem"));
        sdk.SetFonts(fonts);
        Report("Applied: " + (brand ? "brand theme" : "SDK default primary") +
               "; background=" + fields.Get("background") + "; link=" + fields.Get("link") +
               "; navBarItem=" + fields.Get("navBarItem") + "; logo=" + logoName +
               ". Open the Community tab to see it. Nav-bar item size is iOS only.");
    }

    private static OctopusColorScheme Scheme(OctopusColorScheme brand, OctopusScenarioFields fields)
    {
        return new OctopusColorScheme(brand == null ? Color.clear : brand.Primary,
            brand == null ? Color.clear : brand.PrimaryLow,
            brand == null ? Color.clear : brand.PrimaryHigh,
            brand == null ? Color.clear : brand.OnPrimary,
            ColorValue(fields.Get("link")), ColorValue(fields.Get("background")));
    }

    private static Color ColorValue(string value)
    {
        // Catalogue fixtures, not host chrome colors. Their exact values match Flutter's oracle.
        if (value == "#1B1035") return new Color32(0x1B, 0x10, 0x35, 0xFF);
        if (value == "#FFC857") return new Color32(0xFF, 0xC8, 0x57, 0xFF);
        return Color.clear;
    }
}
