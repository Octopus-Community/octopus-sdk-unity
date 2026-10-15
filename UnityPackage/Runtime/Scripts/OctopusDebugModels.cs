/// <summary>Debug/QA only — do not ship in production builds. Per-field profile editability.</summary>
public enum OctopusProfileFieldLockState
{
    /// <summary>Debug/QA only — do not ship in production builds. Displayed and modifiable.</summary>
    Editable,
    /// <summary>Debug/QA only — do not ship in production builds. Displayed but not modifiable.</summary>
    ReadOnly,
    /// <summary>Debug/QA only — do not ship in production builds. Hidden; meaningful for bio only.</summary>
    Disabled,
}

/// <summary>Debug/QA only — do not ship in production builds. How legal documents are accepted at first contribution.</summary>
public enum OctopusTermsAcceptanceMode
{
    /// <summary>Debug/QA only — do not ship in production builds. Acceptance when publishing.</summary>
    Implicit,
    /// <summary>Debug/QA only — do not ship in production builds. One required checkbox per legal document.</summary>
    ExplicitMultiCheckbox,
    /// <summary>Debug/QA only — do not ship in production builds. One combined checkbox.</summary>
    ExplicitSingleCheckbox,
}

/// <summary>Debug/QA only — do not ship in production builds. Immutable ProfileFieldsLock override.</summary>
public sealed class OctopusProfileFieldsLock
{
    /// <summary>Debug/QA only — do not ship in production builds. Nickname editability; use Editable or ReadOnly.</summary>
    public OctopusProfileFieldLockState Nickname { get; private set; }
    /// <summary>Debug/QA only — do not ship in production builds. Avatar editability; use Editable or ReadOnly.</summary>
    public OctopusProfileFieldLockState Avatar { get; private set; }
    /// <summary>Debug/QA only — do not ship in production builds. Bio editability; Disabled hides the field.</summary>
    public OctopusProfileFieldLockState Bio { get; private set; }

    /// <summary>Debug/QA only — do not ship in production builds. Create a profile lock; all fields default to Editable.</summary>
    public OctopusProfileFieldsLock(
        OctopusProfileFieldLockState nickname = OctopusProfileFieldLockState.Editable,
        OctopusProfileFieldLockState avatar = OctopusProfileFieldLockState.Editable,
        OctopusProfileFieldLockState bio = OctopusProfileFieldLockState.Editable)
    {
        Nickname = nickname;
        Avatar = avatar;
        Bio = bio;
    }
}

/// <summary>Debug/QA only — do not ship in production builds. Immutable PostOptions override.</summary>
public sealed class OctopusPostOptions
{
    /// <summary>Debug/QA only — do not ship in production builds. Whether pictures may be attached when creating this content.</summary>
    public bool EnablePictures { get; private set; }
    /// <summary>Debug/QA only — do not ship in production builds. Whether a post may contain a poll.</summary>
    public bool EnablePolls { get; private set; }

    /// <summary>Debug/QA only — do not ship in production builds. Whether a post needs text. Defaults to true; false permits an image without text, while posts without an image still need text.</summary>
    public bool TextRequired { get; private set; }

    /// <summary>Debug/QA only — do not ship in production builds. Create options with text required and native picture/poll defaults.</summary>
    public OctopusPostOptions(bool enablePictures = true, bool enablePolls = true)
        : this(enablePictures, enablePolls, textRequired: true)
    {
    }

    /// <summary>Debug/QA only — do not ship in production builds. Create options with explicit picture, poll and text requirements.</summary>
    public OctopusPostOptions(bool enablePictures, bool enablePolls, bool textRequired)
    {
        EnablePictures = enablePictures;
        EnablePolls = enablePolls;
        TextRequired = textRequired;
    }
}

/// <summary>Debug/QA only — do not ship in production builds. Immutable CommentOptions override.</summary>
public sealed class OctopusCommentOptions
{
    /// <summary>Debug/QA only — do not ship in production builds. Whether pictures may be attached when creating this content.</summary>
    public bool EnablePictures { get; private set; }

    /// <summary>Debug/QA only — do not ship in production builds. Create options with native defaults; omitted content flags are enabled.</summary>
    public OctopusCommentOptions(bool enablePictures = true)
    {
        EnablePictures = enablePictures;
    }
}

/// <summary>Debug/QA only — do not ship in production builds. Immutable ReplyOptions override.</summary>
public sealed class OctopusReplyOptions
{
    /// <summary>Debug/QA only — do not ship in production builds. Whether pictures may be attached when creating this content.</summary>
    public bool EnablePictures { get; private set; }

    /// <summary>Debug/QA only — do not ship in production builds. Create options with native defaults; omitted content flags are enabled.</summary>
    public OctopusReplyOptions(bool enablePictures = true)
    {
        EnablePictures = enablePictures;
    }
}

/// <summary>Debug/QA only — do not ship in production builds. Immutable ContentOptions override.</summary>
public sealed class OctopusContentOptions
{
    /// <summary>Debug/QA only — do not ship in production builds. Options for post creation.</summary>
    public OctopusPostOptions Post { get; private set; }
    /// <summary>Debug/QA only — do not ship in production builds. Options for comment creation.</summary>
    public OctopusCommentOptions Comment { get; private set; }
    /// <summary>Debug/QA only — do not ship in production builds. Options for reply creation.</summary>
    public OctopusReplyOptions Reply { get; private set; }

    /// <summary>Debug/QA only — do not ship in production builds. Create options with native defaults; omitted content flags are enabled.</summary>
    public OctopusContentOptions(OctopusPostOptions post = null, OctopusCommentOptions comment = null, OctopusReplyOptions reply = null)
    {
        Post = post ?? new OctopusPostOptions();
        Comment = comment ?? new OctopusCommentOptions();
        Reply = reply ?? new OctopusReplyOptions();
    }
}

/// <summary>Debug/QA only — do not ship in production builds. The variant of community consent wordings, independent of the terms acceptance mode.</summary>
public enum OctopusConsentTextsMode
{
    /// <summary>Notice under the composers.</summary>
    Implicit = 0,
    /// <summary>One label for each legal document checkbox.</summary>
    MultiCheckbox = 1,
    /// <summary>One checkbox label and text below it.</summary>
    SingleCheckbox = 2,
}

/// <summary>Debug/QA only — do not ship in production builds. Immutable native consent wordings in markdown. Null or blank fields and mode mismatches use native SDK wordings; rendering, links and layout remain native.</summary>
public sealed class OctopusConsentTexts
{
    /// <summary>The wording variant; it does not change TermsAcceptanceMode.</summary>
    public OctopusConsentTextsMode Mode { get; private set; }
    /// <summary>Implicit notice, or null to keep native wording.</summary>
    public string Notice { get; private set; }
    /// <summary>Multi-checkbox terms label, or null to keep native wording.</summary>
    public string TermsLabel { get; private set; }
    /// <summary>Multi-checkbox privacy label, or null to keep native wording.</summary>
    public string PrivacyLabel { get; private set; }
    /// <summary>Multi-checkbox rules label, or null to keep native wording.</summary>
    public string RulesLabel { get; private set; }
    /// <summary>Single-checkbox label, or null to keep native wording.</summary>
    public string Label { get; private set; }
    /// <summary>Text below the single checkbox, or null to keep native wording.</summary>
    public string BelowText { get; private set; }

    private OctopusConsentTexts(OctopusConsentTextsMode mode) { Mode = mode; }

    /// <summary>Create implicit consent wordings. Null or blank notice keeps the native wording.</summary>
    public static OctopusConsentTexts Implicit(string notice = null)
    {
        return new OctopusConsentTexts(OctopusConsentTextsMode.Implicit) { Notice = notice };
    }

    /// <summary>Create multi-checkbox wordings. Null or blank labels keep the native wording per field.</summary>
    public static OctopusConsentTexts MultiCheckbox(string termsLabel = null, string privacyLabel = null, string rulesLabel = null)
    {
        return new OctopusConsentTexts(OctopusConsentTextsMode.MultiCheckbox)
        { TermsLabel = termsLabel, PrivacyLabel = privacyLabel, RulesLabel = rulesLabel };
    }

    /// <summary>Create single-checkbox wordings. Null or blank fields keep the native wording per field.</summary>
    public static OctopusConsentTexts SingleCheckbox(string label = null, string belowText = null)
    {
        return new OctopusConsentTexts(OctopusConsentTextsMode.SingleCheckbox) { Label = label, BelowText = belowText };
    }
}

/// <summary>Debug/QA only — do not ship in production builds. Last effective native configuration; not proof of a network fetch this session.</summary>
public sealed class OctopusCommunityConfig
{
    /// <summary>Debug/QA only — do not ship in production builds. ExposeClientUserId from the effective community configuration.</summary>
    public bool ExposeClientUserId { get; internal set; }
    /// <summary>Debug/QA only — do not ship in production builds. ForceLoginOnStrongActions from the effective community configuration.</summary>
    public bool ForceLoginOnStrongActions { get; internal set; }
    /// <summary>Debug/QA only — do not ship in production builds. DisplayAccountAge from the effective community configuration.</summary>
    public bool DisplayAccountAge { get; internal set; }
    /// <summary>Debug/QA only — do not ship in production builds. TermsAcceptanceMode from the effective community configuration.</summary>
    public OctopusTermsAcceptanceMode TermsAcceptanceMode { get; internal set; }

    /// <summary>Debug/QA only — do not ship in production builds. Whether an unconfirmed member must type a nickname before their first contribution. False when absent.</summary>
    public bool ForceNicknameChoice { get; internal set; }

    /// <summary>Debug/QA only — do not ship in production builds. Effective community consent wordings, or null when absent.</summary>
    public OctopusConsentTexts ConsentTexts { get; internal set; }

    internal OctopusCommunityConfig() { }
}
