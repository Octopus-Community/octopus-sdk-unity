using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

/// <summary>A text the host app gives either for automatic translation or per language: the label
/// of a Bridge Share call to action (OctopusPrefilledPost.CtaLocalizedLabel) and of the button of a
/// bridge post (OctopusClientObject.ViewObjectButton).
/// <para>A reader whose language is in the list sees that text as it is; any other reader sees an
/// automatic translation of the default language's text.</para></summary>
public sealed class OctopusLocalizedText
{
    private static readonly IReadOnlyDictionary<string, string> NoTexts =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>The text to start from: the text itself when auto-translated, the text given for
    /// DefaultLanguage when given per language (null when no text is given under that exact tag).</summary>
    public string DefaultText { get; private set; }

    /// <summary>The language tag of the default text, as given, or null when the text is auto-translated.</summary>
    public string DefaultLanguage { get; private set; }

    /// <summary>The text of each language tag, as given; empty when the text is auto-translated.</summary>
    public IReadOnlyDictionary<string, string> Texts { get; private set; }

    /// <summary>True when the text is given per language, false when it is auto-translated.</summary>
    public bool IsPerLanguage { get { return DefaultLanguage != null; } }

    private OctopusLocalizedText(string defaultText, string defaultLanguage, IReadOnlyDictionary<string, string> texts)
    {
        DefaultText = defaultText;
        DefaultLanguage = defaultLanguage;
        Texts = texts;
    }

    /// <summary>One text, which Octopus translates automatically for every reader. This is what a plain
    /// string label (the deprecated CtaLabel and ViewObjectButtonText) means.</summary>
    /// <param name="text">The text, in any language.</param>
    /// <exception cref="ArgumentNullException">text is null.</exception>
    public static OctopusLocalizedText AutoTranslated(string text)
    {
        if (text == null) throw new ArgumentNullException("text");
        return new OctopusLocalizedText(text, null, NoTexts);
    }

    /// <summary>One text per language, with the language to start from for the readers whose language
    /// is not listed.
    /// <para>Languages are BCP-47 language tags ("en", "pt-BR", "zh-Hant-TW"). They are kept as given:
    /// the native SDK reads each tag ("_" is read as "-") and validates the texts when the label is
    /// used. It refuses no text, a blank text, a tag without a language, two tags giving the same
    /// language code, and a default language that is not one of the tags. A refused bridge post button
    /// fails FetchOrCreateClientObjectRelatedPost with OctopusClientPostErrorCode.InvalidLocalizedText;
    /// a refused CTA label is logged and the CTA left out.</para>
    /// <para>Give a language, with a region if needed and no script ("fr", "pt-BR", "zh-HK"): such a tag
    /// is sent as written on Android and iOS, which matters when your backend signs the content.</para>
    /// <para>The labels of a bridge post are taken when the post is created: a post that already exists
    /// for the object id keeps its button.</para></summary>
    /// <param name="texts">The text of each language tag. Copied.</param>
    /// <param name="defaultLanguage">The language tag of the text to show and translate when none matches
    /// the reader.</param>
    /// <exception cref="ArgumentNullException">texts or defaultLanguage is null.</exception>
    public static OctopusLocalizedText PerLanguage(IDictionary<string, string> texts, string defaultLanguage)
    {
        if (texts == null) throw new ArgumentNullException("texts");
        if (defaultLanguage == null) throw new ArgumentNullException("defaultLanguage");
        var copy = new Dictionary<string, string>(texts);
        string defaultText;
        copy.TryGetValue(defaultLanguage, out defaultText);
        return new OctopusLocalizedText(defaultText, defaultLanguage, new ReadOnlyDictionary<string, string>(copy));
    }

    /// <summary>Two texts are equal when both are auto-translated with the same text, or both are given
    /// per language with the same tags, texts and default language, as given.</summary>
    public override bool Equals(object obj)
    {
        var other = obj as OctopusLocalizedText;
        if (other == null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (DefaultText != other.DefaultText || DefaultLanguage != other.DefaultLanguage) return false;
        if (Texts.Count != other.Texts.Count) return false;
        foreach (var pair in Texts)
        {
            string text;
            if (!other.Texts.TryGetValue(pair.Key, out text) || text != pair.Value) return false;
        }
        return true;
    }

    /// <summary>A hash consistent with Equals.</summary>
    public override int GetHashCode()
    {
        int hash = DefaultText == null ? 0 : DefaultText.GetHashCode();
        if (DefaultLanguage != null) hash = hash * 31 + DefaultLanguage.GetHashCode();
        foreach (var pair in Texts) hash ^= pair.Key.GetHashCode() ^ (pair.Value == null ? 0 : pair.Value.GetHashCode());
        return hash;
    }

    /// <summary>A readable form, for logs.</summary>
    public override string ToString()
    {
        if (!IsPerLanguage) return "AutoTranslated(" + DefaultText + ")";
        var sb = new StringBuilder("PerLanguage(defaultLanguage=").Append(DefaultLanguage);
        foreach (var tag in SortedTags()) sb.Append(", ").Append(tag).Append('=').Append(Texts[tag]);
        return sb.Append(')').ToString();
    }

    private List<string> SortedTags()
    {
        var tags = new List<string>(Texts.Keys);
        tags.Sort(StringComparer.Ordinal);
        return tags;
    }

    // The wire both bridges read: {"text":"…"} when auto-translated,
    // {"defaultLanguage":"en","texts":{"en":"…","fr":"…"}} when given per language, tags as given (a null
    // text travels as ""). The bridges tell the two apart by the presence of "texts".
    internal string ToJson()
    {
        var json = new StringBuilder("{");
        if (!IsPerLanguage) return AppendString(json.Append("\"text\":"), DefaultText).Append('}').ToString();
        AppendString(json.Append("\"defaultLanguage\":"), DefaultLanguage).Append(",\"texts\":{");
        var tags = SortedTags();
        for (int i = 0; i < tags.Count; i++)
        {
            if (i > 0) json.Append(',');
            AppendString(json, tags[i]).Append(':');
            AppendString(json, Texts[tags[i]] ?? "");
        }
        return json.Append("}}").ToString();
    }

    private static StringBuilder AppendString(StringBuilder json, string value)
    {
        json.Append('"');
        foreach (char c in value)
        {
            if (c == '"') json.Append("\\\"");
            else if (c == '\\') json.Append("\\\\");
            else if (c < 32) json.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            else json.Append(c);
        }
        return json.Append('"');
    }
}

/// <summary>Why the native SDK refused a per-language text (OctopusClientPostError.LocalizedTextErrorKind).
/// Mirrors the native OctopusLocalizedText.ValidationError kinds.</summary>
public enum OctopusLocalizedTextValidationErrorKind
{
    /// <summary>No text was given.</summary>
    EmptyTexts,
    /// <summary>The text of a language is empty or whitespace only.</summary>
    BlankText,
    /// <summary>A language tag has no language, such as "" or "und".</summary>
    EmptyLanguage,
    /// <summary>Two language tags give the same code, such as "pt-BR" and "pt_BR".</summary>
    DuplicateLanguage,
    /// <summary>The default language is not exactly one of the languages given: "en-US" when only "en" is.</summary>
    DefaultLanguageMissing
}
