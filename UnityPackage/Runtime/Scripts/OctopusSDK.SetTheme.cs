using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static void SetTheme(OctopusColorScheme colorScheme = null, OctopusColorScheme lightColorScheme = null, OctopusColorScheme darkColorScheme = null, OctopusLogo logo = null, bool navBarUsesPrimaryColor = false, OctopusFonts fonts = null, string appName = "")
    {
        SetLightColorScheme(lightColorScheme ?? colorScheme);
        SetDarkColorScheme(darkColorScheme ?? colorScheme);
        SetLogo(logo);
        SetNavBarUsesPrimaryColor(navBarUsesPrimaryColor);
        SetFonts(fonts);
        SetAppName(appName);
    }

    public static void SetLightColorScheme(OctopusColorScheme colorScheme)
    {
        if (colorScheme != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass plugin = new ("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic(
                    "setLightColorScheme",
                    ColorToInt(colorScheme.Primary),
                    ColorToInt(colorScheme.PrimaryLow),
                    ColorToInt(colorScheme.PrimaryHigh),
                    ColorToInt(colorScheme.OnPrimary)
                );
            }
#elif UNITY_IOS && !UNITY_EDITOR
            OctopusSdkSetLightColorScheme(
                ColorToInt(colorScheme.Primary),
                ColorToInt(colorScheme.PrimaryLow),
                ColorToInt(colorScheme.PrimaryHigh),
                ColorToInt(colorScheme.OnPrimary)
            );
#endif
        }
    }

    public static void SetDarkColorScheme(OctopusColorScheme colorScheme)
    {
        if (colorScheme != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass plugin = new ("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic(
                    "setDarkColorScheme",
                    ColorToInt(colorScheme.Primary),
                    ColorToInt(colorScheme.PrimaryLow),
                    ColorToInt(colorScheme.PrimaryHigh),
                    ColorToInt(colorScheme.OnPrimary)
                );
            }
#elif UNITY_IOS && !UNITY_EDITOR
            OctopusSdkSetDarkColorScheme(
                ColorToInt(colorScheme.Primary),
                ColorToInt(colorScheme.PrimaryLow),
                ColorToInt(colorScheme.PrimaryHigh),
                ColorToInt(colorScheme.OnPrimary)
            );
#endif
        }
    }

    public static void SetLogo(OctopusLogo logo)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setLogo", logo?.AndroidDrawableName ?? "");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetLogo(logo?.IOSResourceName ?? "");
#endif
    }

    public static void SetAppName(string appName)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setAppName", appName);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetAppName(appName);
#endif
    }

    public static void SetNavBarUsesPrimaryColor(bool usesPrimary)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setNavBarUsesPrimaryColor", usesPrimary);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetNavBarUsesPrimaryColor(usesPrimary);
#endif
    }

    /// <summary>
    /// Sets the color scheme type: 0 = System, 1 = Light, 2 = Dark
    /// </summary>
    public static void SetColorSchemeType(int colorSchemeType)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setColorSchemeType", colorSchemeType);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetColorSchemeType(colorSchemeType);
#endif
    }

    public static void SetFonts(OctopusFonts fonts = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setFonts",
                fonts?.Title1?.AndroidFontResourceName ?? "",
                fonts?.Title1?.Size ?? 0f,
                fonts?.Title2?.AndroidFontResourceName ?? "",
                fonts?.Title2?.Size ?? 0f,
                fonts?.Body1?.AndroidFontResourceName ?? "",
                fonts?.Body1?.Size ?? 0f,
                fonts?.Body2?.AndroidFontResourceName ?? "",
                fonts?.Body2?.Size ?? 0f,
                fonts?.Caption1?.AndroidFontResourceName ?? "",
                fonts?.Caption1?.Size ?? 0f,
                fonts?.Caption2?.AndroidFontResourceName ?? "",
                fonts?.Caption2?.Size ?? 0f,
                fonts?.NavBarItem?.AndroidFontResourceName ?? "",
                fonts?.NavBarItem?.Size ?? 0f
            );
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetFonts(
            fonts?.Title1?.IOSFontName ?? "",
            fonts?.Title1?.Size ?? 0f,
            fonts?.Title2?.IOSFontName ?? "",
            fonts?.Title2?.Size ?? 0f,
            fonts?.Body1?.IOSFontName ?? "",
            fonts?.Body1?.Size ?? 0f,
            fonts?.Body2?.IOSFontName ?? "",
            fonts?.Body2?.Size ?? 0f,
            fonts?.Caption1?.IOSFontName ?? "",
            fonts?.Caption1?.Size ?? 0f,
            fonts?.Caption2?.IOSFontName ?? "",
            fonts?.Caption2?.Size ?? 0f,
            fonts?.NavBarItem?.IOSFontName ?? "",
            fonts?.NavBarItem?.Size ?? 0f
        );
#endif
    }

    public static int ColorToInt(Color color)
    {
        Color32 c = color;
        return ((int)c.a << 24) |
               ((int)c.r << 16) |
               ((int)c.g << 8) |
               (int)c.b;
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSetLightColorScheme(int primary, int primaryLow, int primaryHigh, int onPrimary);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetDarkColorScheme(int primary, int primaryLow, int primaryHigh, int onPrimary);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetLogo(string logo);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetAppName(string appName);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetNavBarUsesPrimaryColor(bool usesPrimary);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetColorSchemeType(int colorSchemeType);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetFonts(
        string title1Font, float title1Size,
        string title2Font, float title2Size,
        string body1Font, float body1Size,
        string body2Font, float body2Size,
        string caption1Font, float caption1Size,
        string caption2Font, float caption2Size,
        string navBarItemFont, float navBarItemSize
    );
#endif 
}

public class OctopusColorScheme
{
    public readonly Color Primary;
    public readonly Color PrimaryLow;
    public readonly Color PrimaryHigh;
    public readonly Color OnPrimary;

    public OctopusColorScheme(
        Color primary,
        Color primaryLow,
        Color primaryHigh,
        Color onPrimary
    )
    {
        this.Primary = primary;
        this.PrimaryLow = primaryLow;
        this.PrimaryHigh = primaryHigh;
        this.OnPrimary = onPrimary;
    }
}

public class OctopusLogo
{
    public readonly string AndroidDrawableName;
    public readonly string IOSResourceName;

    public OctopusLogo(string androidDrawableName, string iOSResourceName)
    {
        AndroidDrawableName = androidDrawableName;
        IOSResourceName = iOSResourceName;
    }
}

public class OctopusFonts
{
    public readonly OctopusFont Title1;
    public readonly OctopusFont Title2;
    public readonly OctopusFont Body1;
    public readonly OctopusFont Body2;
    public readonly OctopusFont Caption1;
    public readonly OctopusFont Caption2;
    public readonly OctopusFont NavBarItem;

    public OctopusFonts(
        OctopusFont title1 = null,
        OctopusFont title2 = null,
        OctopusFont body1 = null,
        OctopusFont body2 = null,
        OctopusFont caption1 = null,
        OctopusFont caption2 = null,
        OctopusFont navBarItem = null
    )
    {
        Title1 = title1;
        Title2 = title2;
        Body1 = body1;
        Body2 = body2;
        Caption1 = caption1;
        Caption2 = caption2;
        NavBarItem = navBarItem;
    }
}

public class OctopusFont
{
    public readonly string AndroidFontResourceName;
    public readonly string IOSFontName;
    public readonly float Size;

    public OctopusFont(string androidFontResourceName, string iOSFontName, float size)
    {
        AndroidFontResourceName = androidFontResourceName;
        IOSFontName = iOSFontName;
        Size = size;
    }
}
