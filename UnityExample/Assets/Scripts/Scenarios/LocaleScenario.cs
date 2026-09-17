using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// The `locale` scenario: `OctopusSDK.OverrideDefaultLocale(languageCode)`.
///
/// The SDK call is the one `LanguageOverrideExample` makes. The catalogue's third preset is
/// "Reset to system", and this package has **no reset overload**: the Android bridge takes a
/// non-null `String` and builds a `java.util.Locale` from it, so passing null throws rather than
/// clearing the override. The preset therefore restores the system language explicitly — it reads
/// the device's own two-letter code and overrides with that, which is observably the same end
/// state and the only thing the current surface allows. A real reset would be an SDK change.
///
/// Only "fr" and "en" are presets, because the catalogue lists three. Customize unlocks the field, so
/// the other locales `LanguageOverrideExample` offered ("es", "tr", and anything else the
/// community ships) are still reachable by hand.
/// </summary>
public sealed class LocaleScenario : OctopusScenarioPilot
{
    private const string LocaleKey = "locale";

    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField(LocaleKey, "Language code (e.g. \"fr\", \"en-US\")"));

    private readonly List<OctopusScenarioPreset> _presets;

    public LocaleScenario() : base("locale")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Override(1, "Force fr", "fr"),
            Override(2, "Force en", "en"),
            Override(3, "Reset to system", SystemLanguageCode()),
        };
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }

    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }

    public override bool CanCustomize { get { return true; } }

    public override string Capability { get { return "Choose the language used by the community."; } }
    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "OverrideDefaultLocale" }; } }
    public override string ParameterNotice
    {
        get { return "Reset to system applies the current system language explicitly; it does not clear the override."; }
    }

    public override void RunCustom()
    {
        OverrideNow(Fields.Get(LocaleKey));
    }

    /// <summary>
    /// The device's two-letter language code. `Application.systemLanguage` is the OS language
    /// Unity reports; the thread culture is only a fallback, because Mono leaves it at the
    /// invariant culture on some players and it can be changed by any code.
    /// </summary>
    private static string SystemLanguageCode()
    {
        return LanguageCodeFor(Application.systemLanguage, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Maps Unity's <see cref="SystemLanguage"/> to a two-letter ISO 639-1 code, then falls back
    /// to <paramref name="threadCulture"/>, then to "en". The table is explicit on purpose: a
    /// name-based lookup fails on Unity's own enum, whose misspelt `Hugarian` member shares its
    /// value with `Hungarian` and is the name `ToString()` returns. "iv" is .NET's invariant
    /// culture — a real value the SDK would reject as a language, so it counts as unknown.
    /// </summary>
    public static string LanguageCodeFor(SystemLanguage language, CultureInfo threadCulture)
    {
        switch (language)
        {
            case SystemLanguage.Afrikaans: return "af";
            case SystemLanguage.Arabic: return "ar";
            case SystemLanguage.Basque: return "eu";
            case SystemLanguage.Belarusian: return "be";
            case SystemLanguage.Bulgarian: return "bg";
            case SystemLanguage.Catalan: return "ca";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "zh";
            case SystemLanguage.Czech: return "cs";
            case SystemLanguage.Danish: return "da";
            case SystemLanguage.Dutch: return "nl";
            case SystemLanguage.English: return "en";
            case SystemLanguage.Estonian: return "et";
            case SystemLanguage.Faroese: return "fo";
            case SystemLanguage.Finnish: return "fi";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Greek: return "el";
            case SystemLanguage.Hebrew: return "he";
            case SystemLanguage.Hindi: return "hi";
            case SystemLanguage.Hungarian: return "hu"; // also covers the misspelt alias, same value
            case SystemLanguage.Icelandic: return "is";
            case SystemLanguage.Indonesian: return "id";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Latvian: return "lv";
            case SystemLanguage.Lithuanian: return "lt";
            case SystemLanguage.Norwegian: return "no";
            case SystemLanguage.Polish: return "pl";
            case SystemLanguage.Portuguese: return "pt";
            case SystemLanguage.Romanian: return "ro";
            case SystemLanguage.Russian: return "ru";
            case SystemLanguage.SerboCroatian: return "sr"; // "sh" is withdrawn from ISO 639-1
            case SystemLanguage.Slovak: return "sk";
            case SystemLanguage.Slovenian: return "sl";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.Swedish: return "sv";
            case SystemLanguage.Thai: return "th";
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Ukrainian: return "uk";
            case SystemLanguage.Vietnamese: return "vi";
        }

        var code = threadCulture == null ? null : threadCulture.TwoLetterISOLanguageName;
        return string.IsNullOrEmpty(code) || code == "iv" ? "en" : code;
    }

    private OctopusScenarioPreset Override(int index, string description, string languageCode)
    {
        return new OctopusScenarioPreset(
            PresetTestId(index),
            PresetLabel(index, description),
            fields => fields.Set(LocaleKey, languageCode),
            fields => OverrideNow(fields.Get(LocaleKey)));
    }

    private void OverrideNow(string languageCode)
    {
        string mode;
        var profile = OctopusScenarioSdk.EnsureInitialized(
            OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode);
        if (profile == null)
        {
            Report(mode);
            return;
        }

        if (string.IsNullOrEmpty(languageCode))
        {
            Report("No language code: this package has no reset overload, so an empty value " +
                   "would reach the native SDK as an invalid locale. Type a code, or tap a preset.");
            return;
        }

        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OverrideDefaultLocale",
                                            "languageCode=" + languageCode);
        OctopusScenarioSdk.Current.OverrideDefaultLocale(languageCode);
        OctopusSampleState.ReportLocaleOverride(languageCode);
        Report("Locale overridden to '" + languageCode + "' (mode: " + mode + "). The override " +
               "applies to the community UI — open the Community tab to see it.");
    }
}
