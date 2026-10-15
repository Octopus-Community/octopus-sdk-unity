using System.Globalization;
using UnityEngine;

public static class OctopusSampleLanguage
{
    internal static string PreferenceKey = "OctopusSample.Locale";
    public static readonly string[] Codes = { "", "en", "fr", "de", "it", "pl", "pt", "es", "sv", "tr" };
    public static readonly string[] Names = { "Follow system", "English", "French", "German", "Italian", "Polish", "Portuguese", "Spanish", "Swedish", "Turkish" };
    public static string Selected { get { return PlayerPrefs.GetString(PreferenceKey, ""); } }
    public static string Label(string code)
    {
        for (var i = 0; i < Codes.Length; i++) if (Codes[i] == (code ?? "")) return Names[i];
        return code;
    }
    public static void Select(string code)
    {
        PlayerPrefs.SetString(PreferenceKey, code ?? "");
        PlayerPrefs.Save();
        ApplySaved();
    }
    public static void ApplySaved()
    {
        if (!OctopusSampleState.IsInitialized || !PlayerPrefs.HasKey(PreferenceKey)) return;
        var code = Selected;
        // The bridge has no nullable locale reset. Reapply the current OS language on resume.
        var applied = string.IsNullOrEmpty(code)
            ? LocaleScenario.LanguageCodeFor(Application.systemLanguage, CultureInfo.CurrentCulture) : code;
        OctopusScenarioSdk.Current.OverrideDefaultLocale(applied);
        OctopusSampleState.ReportLocaleOverride(string.IsNullOrEmpty(code) ? null : code);
    }
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        OctopusSampleState.ReportLocaleOverride(null);
    }
}
