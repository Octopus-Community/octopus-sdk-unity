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

    /// <summary>Debug/QA only — do not ship in production builds. Create options with native defaults; omitted content flags are enabled.</summary>
    public OctopusPostOptions(bool enablePictures = true, bool enablePolls = true)
    {
        EnablePictures = enablePictures;
        EnablePolls = enablePolls;
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

    internal OctopusCommunityConfig() { }
}
