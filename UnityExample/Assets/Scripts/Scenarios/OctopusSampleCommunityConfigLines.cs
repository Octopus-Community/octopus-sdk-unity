using System.Collections.Generic;

/// <summary>
/// The wording of the Scenarios tab's "Live community config" panel, kept pure so it is tested
/// without a canvas.
///
/// Android's `CommunityConfigPreview` lists the community configuration the SDK resolved. The
/// Unity package exposes selected fields through <c>OctopusSDK.DebugGetCommunityConfig</c>;
/// the panel shows those, under Android's labels, and says which ones it cannot show
/// (<see cref="Limits"/>). It reads on tap rather than live: the package has no config stream.
/// </summary>
public static class OctopusSampleCommunityConfigLines
{
    public const string WrapperId = "scenarios-live-community-config";
    public const string PreviewId = "community-config-preview";
    public const string ErrorId = "community-config-preview-error";
    public const string ReadButtonId = "community-config-preview-read";

    public const string Title = "Live community config";
    public const string Description =
        "What the SDK sees for the current community (backend config + any local override applied below). Updates after switchCommunity.";
    public const string ApiKeyLabel = "API key";
    public const string StatusLabel = "Status";
    public const string NotInitialised = "SDK not initialised";
    public const string Loading = "loading…";

    /// <summary>
    /// The read answered without a configuration: nothing failed, the SDK has none to give yet. A
    /// neutral line, not the error block — that one is kept for a read that actually threw.
    /// </summary>
    public const string NotAvailable = "Not available yet";

    /// <summary>How long a read may stay on "loading…" before the panel gives the Read button back.</summary>
    public const float ReadTimeoutSeconds = 10f;

    public const string ReadLabel = "Read config";
    public const string RefreshLabel = "Refresh";
    public const string RetryLabel = "Retry";
    public const string Limits =
        "Not exposed by the Unity SDK: Post/Comment/Reply content options, Profile lock, Gamification, Powered by Octopus. Read on tap, no live updates.";

    /// <summary>
    /// A boolean as Android prints it. The bundled Inter carries U+2713 and U+2717, so the check and
    /// cross render as glyphs, not boxes.
    /// </summary>
    public static string Tick(bool on)
    {
        return on ? "✓ on" : "✗ off";
    }

    /// <summary>One "label: value" line of the panel, as Android's `Line(label, value)` reads.</summary>
    public static string Line(string label, string value)
    {
        return label + ": " + value;
    }

    /// <summary>A terms acceptance mode as Android prints it.</summary>
    public static string Terms(OctopusTermsAcceptanceMode mode)
    {
        switch (mode)
        {
            case OctopusTermsAcceptanceMode.ExplicitMultiCheckbox: return "explicit · multi checkbox";
            case OctopusTermsAcceptanceMode.ExplicitSingleCheckbox: return "explicit · single checkbox";
            default: return "implicit (legal footer)";
        }
    }

    /// <summary>The label/value pairs the panel prints once a read succeeded, in Android's order.</summary>
    public static List<KeyValuePair<string, string>> Lines(bool exposeClientUserId,
        OctopusTermsAcceptanceMode terms, bool forceLogin, bool displayAccountAge, bool forceNicknameChoice = false, OctopusConsentTexts consentTexts = null)
    {
        return new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Unified Profile (exposeClientUserId)", Tick(exposeClientUserId)),
            new KeyValuePair<string, string>("Terms acceptance", Terms(terms)),
            new KeyValuePair<string, string>("Force login (strong actions)", Tick(forceLogin)),
            new KeyValuePair<string, string>("Display account age", Tick(displayAccountAge)),
            new KeyValuePair<string, string>("Force nickname choice", Tick(forceNicknameChoice)),
            new KeyValuePair<string, string>("Consent texts", Consent(consentTexts)),
        };
    }

    public static string Consent(OctopusConsentTexts texts)
    {
        if (texts == null) return "SDK wordings (none supplied)";
        switch (texts.Mode)
        {
            case OctopusConsentTextsMode.Implicit: return "implicit · notice=" + Wording(texts.Notice);
            case OctopusConsentTextsMode.MultiCheckbox: return "multi checkbox · terms=" + Wording(texts.TermsLabel) +
                "; privacy=" + Wording(texts.PrivacyLabel) + "; rules=" + Wording(texts.RulesLabel);
            default: return "single checkbox · label=" + Wording(texts.Label) + "; below=" + Wording(texts.BelowText);
        }
    }

    private static string Wording(string text) { return text == null ? "(SDK wording)" : "\"" + text + "\""; }

    /// <summary>The section count badge's text.</summary>
    public static string CountText(int count)
    {
        return count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
