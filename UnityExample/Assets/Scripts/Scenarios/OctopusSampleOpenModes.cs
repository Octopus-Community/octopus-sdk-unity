/// <summary>
/// The four containers the Android sample's "Open the community" card offers, in its order.
/// </summary>
public enum OctopusSampleOpenMode
{
    Tab,
    Modal,
    Sheet,
    FullScreen,
}

/// <summary>
/// The wording, QA ids and availability of the Scenarios tab's "Open the community" card.
///
/// Android lays the four containers out as a 2x2 grid of pills (`OpenCommunityCard`). The Unity
/// package opens the community one way only — <c>OctopusSDK.Open()</c>, a full-screen native
/// activity or view controller — so Full screen is the one live button. The three others stay on
/// screen, disabled, with a caption saying why: a hidden button reads as a missing port, a live
/// one that silently opens full screen would claim a container the SDK does not give.
/// </summary>
public static class OctopusSampleOpenModes
{
    public const string CardId = "scenarios-open-community-card";
    public const string Title = "Open the community";
    public const string Caption = "The same community, in the container your app gives it.";
    public const string UnavailableCaptionId = "open-community-unavailable-caption";
    /// <summary>One line: Tab, Modal and Sheet need a host container the Unity SDK does not offer.</summary>
    public const string UnavailableCaption = "Unity opens Full screen only (OctopusSDK.Open).";

    /// <summary>The line under the grid that says why the last tap opened nothing; hidden otherwise.</summary>
    public const string StatusId = "open-community-status";

    /// <summary>The grid's order: Tab, Modal on the first row, Sheet, Full screen on the second.</summary>
    public static readonly OctopusSampleOpenMode[] Order =
    {
        OctopusSampleOpenMode.Tab, OctopusSampleOpenMode.Modal,
        OctopusSampleOpenMode.Sheet, OctopusSampleOpenMode.FullScreen,
    };

    /// <summary>Whether the Unity SDK can open the community in <paramref name="mode"/>.</summary>
    public static bool IsAvailable(OctopusSampleOpenMode mode)
    {
        return mode == OctopusSampleOpenMode.FullScreen;
    }

    /// <summary>
    /// The first mode of <see cref="Order"/> the Unity SDK can open: it wears the Primary style, as
    /// Android's first tile does, so the filled pill is never a disabled one.
    /// </summary>
    public static OctopusSampleOpenMode FirstAvailable()
    {
        foreach (var mode in Order)
            if (IsAvailable(mode)) return mode;
        return OctopusSampleOpenMode.FullScreen;
    }

    /// <summary>The button's text, Android's wording.</summary>
    public static string Label(OctopusSampleOpenMode mode)
    {
        switch (mode)
        {
            case OctopusSampleOpenMode.Tab: return "Tab";
            case OctopusSampleOpenMode.Modal: return "Modal";
            case OctopusSampleOpenMode.Sheet: return "Sheet";
            default: return "Full screen";
        }
    }

    /// <summary>The button's QA id, the shared catalogue's.</summary>
    public static string Id(OctopusSampleOpenMode mode)
    {
        switch (mode)
        {
            case OctopusSampleOpenMode.Tab: return "open-community-tab-button";
            case OctopusSampleOpenMode.Modal: return "open-community-modal-button";
            case OctopusSampleOpenMode.Sheet: return "open-community-sheet-button";
            default: return "open-community-fullscreen-button";
        }
    }
}
