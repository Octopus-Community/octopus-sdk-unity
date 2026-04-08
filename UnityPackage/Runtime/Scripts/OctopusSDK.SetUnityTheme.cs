using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Applies theme settings from OctopusThemeSettings ScriptableObject.
    /// Called automatically during Initialize(), but can be called manually if needed.
    /// </summary>
    public static void SetUnityTheme()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        var settings = OctopusThemeSettings.Instance;
        if (settings == null) return;

        if (!string.IsNullOrEmpty(settings.LogoFileName))
        {
            var logo = new OctopusLogo("my_logo", "Data/Raw/" + settings.LogoFileName);
            SetLogo(logo);
        }

        SetNavBarUsesPrimaryColor(settings.NavBarUsesPrimaryColor);

        SetColorSchemeType((int)settings.ColorScheme);

        ApplyColorSchemeSettings(settings);

        if (!string.IsNullOrEmpty(settings.AppName))
        {
            SetAppName(settings.AppName);
        }

        ApplyFontSettings(settings);
#endif
    }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private static void ApplyColorSchemeSettings(OctopusThemeSettings settings)
    {
        // Apply light color scheme if any colors are enabled
        var lightConfig = settings.LightColorSchemeConfig;
        if (lightConfig != null && lightConfig.HasAnyEnabled)
        {
            SetLightColorScheme(new OctopusColorScheme(
                lightConfig.primaryEnabled ? lightConfig.primary : Color.clear,
                lightConfig.primaryLowEnabled ? lightConfig.primaryLow : Color.clear,
                lightConfig.primaryHighEnabled ? lightConfig.primaryHigh : Color.clear,
                lightConfig.onPrimaryEnabled ? lightConfig.onPrimary : Color.clear
            ));
        }

        // Apply dark color scheme if any colors are enabled
        var darkConfig = settings.DarkColorSchemeConfig;
        if (darkConfig != null && darkConfig.HasAnyEnabled)
        {
            SetDarkColorScheme(new OctopusColorScheme(
                darkConfig.primaryEnabled ? darkConfig.primary : Color.clear,
                darkConfig.primaryLowEnabled ? darkConfig.primaryLow : Color.clear,
                darkConfig.primaryHighEnabled ? darkConfig.primaryHigh : Color.clear,
                darkConfig.onPrimaryEnabled ? darkConfig.onPrimary : Color.clear
            ));
        }
    }

    private static void ApplyFontSettings(OctopusThemeSettings settings)
    {
        var fontStyles = settings.FontStyles;
        if (fontStyles == null) return;

        bool hasFonts = false;
        for (int i = 0; i < fontStyles.Length; i++)
        {
            if (fontStyles[i] != null && fontStyles[i].enabled)
            {
                hasFonts = true;
                break;
            }
        }
        if (!hasFonts) return;

        var fonts = new OctopusFonts(
            CreateOctopusFont(settings, OctopusThemeSettings.FontStyleType.Title1),
            CreateOctopusFont(settings, OctopusThemeSettings.FontStyleType.Title2),
            CreateOctopusFont(settings, OctopusThemeSettings.FontStyleType.Body1),
            CreateOctopusFont(settings, OctopusThemeSettings.FontStyleType.Body2),
            CreateOctopusFont(settings, OctopusThemeSettings.FontStyleType.Caption1),
            CreateOctopusFont(settings, OctopusThemeSettings.FontStyleType.Caption2),
            CreateOctopusFont(settings, OctopusThemeSettings.FontStyleType.NavBarItem)
        );

        SetFonts(fonts);
    }

    private static OctopusFont CreateOctopusFont(OctopusThemeSettings settings, OctopusThemeSettings.FontStyleType styleType)
    {
        int index = (int)styleType;
        var fontStyles = settings.FontStyles;
        if (index < 0 || index >= fontStyles.Length) return null;

        var config = fontStyles[index];
        if (config == null || !config.enabled || config.fontIndex < 0) return null;

        var fontFileNames = settings.FontFileNames;
        if (config.fontIndex >= fontFileNames.Length) return null;

        string fontFileName = fontFileNames[config.fontIndex];
        if (string.IsNullOrEmpty(fontFileName)) return null;

        string androidName = SanitizeFontNameForAndroid(fontFileName);
        string iosName = System.IO.Path.GetFileNameWithoutExtension(fontFileName);

        return new OctopusFont(androidName, iosName, config.fontSize);
    }

    private static string SanitizeFontNameForAndroid(string fileName)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        name = name.Replace('-', '_').Replace(' ', '_');

        var sanitized = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
            {
                sanitized.Append(c);
            }
        }
        return sanitized.ToString();
    }
#endif
}
