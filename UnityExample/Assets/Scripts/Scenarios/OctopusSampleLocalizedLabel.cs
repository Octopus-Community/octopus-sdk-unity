using System;
using System.Collections.Generic;

/// <summary>
/// Builds the label of a bridge post button or a Bridge Share CTA from the scenario's text fields, the
/// way the native samples' per-language switch does: the default label with an editable language
/// (prefilled "en"), then rows of a language tag and a label.
///
/// Rows are written <c>tag=label</c>, separated by ";" or a new line. A row left empty is ignored; a
/// half-filled one ("fr=", "=Ouvrir") is sent as is, so the native SDK's validation error shows (on the
/// bridge post's typed error; a refused CTA is logged and left out). Two rows naming the same tag are
/// reported here, since a dictionary cannot hold them both.
/// </summary>
public static class OctopusSampleLocalizedLabel
{
    public const string PerLanguageFieldLabel = "Per-language label (true or false; false = one auto-translated label)";
    public const string LanguageFieldLabel = "Default label language (BCP-47 tag, used when per-language is true)";
    public const string TranslationsFieldLabel = "Other languages, tag=label rows separated by ; (used when per-language is true)";

    /// <summary>The label, or null with <paramref name="error"/> set when the form cannot build it.
    /// An empty label without per-language gives null without error: no button.</summary>
    public static OctopusLocalizedText Build(string perLanguage, string label, string language, string translations,
        out string error)
    {
        error = null;
        bool isPerLanguage;
        if (!bool.TryParse((perLanguage ?? "").Trim(), out isPerLanguage))
        { error = "per-language label must be true or false"; return null; }
        label = (label ?? "").Trim();
        if (!isPerLanguage) return label.Length == 0 ? null : OctopusLocalizedText.AutoTranslated(label);

        var texts = new Dictionary<string, string>();
        var defaultLanguage = (language ?? "").Trim();
        texts[defaultLanguage] = label;
        foreach (var row in (translations ?? "").Split(new[] { ';', '\n' }))
        {
            if (row.Trim().Length == 0) continue;
            int equals = row.IndexOf('=');
            var tag = (equals < 0 ? row : row.Substring(0, equals)).Trim();
            var text = equals < 0 ? "" : row.Substring(equals + 1).Trim();
            if (texts.ContainsKey(tag)) { error = "two labels are given for \"" + tag + "\""; return null; }
            texts[tag] = text;
        }
        return OctopusLocalizedText.PerLanguage(texts, defaultLanguage);
    }

    /// <summary>One line for the result card: the default text, then each language tag when per language.</summary>
    public static string Describe(OctopusLocalizedText label)
    {
        if (label == null) return "(none)";
        if (!label.IsPerLanguage) return label.DefaultText + " (auto-translated)";
        var tags = new List<string>(label.Texts.Keys);
        tags.Sort(StringComparer.Ordinal);
        return (label.DefaultText ?? "(no default text)") + " (per language, default " + label.DefaultLanguage + ": " +
               string.Join(", ", tags.ToArray()) + ")";
    }
}
