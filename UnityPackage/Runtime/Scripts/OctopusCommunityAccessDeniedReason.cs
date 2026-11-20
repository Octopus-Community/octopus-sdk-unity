/// <summary>
/// Why the current user cannot access the community. Read it from
/// <see cref="OctopusSDK.CommunityAccessDeniedReason"/>, next to <see cref="OctopusSDK.HasAccessToCommunity"/>.
/// When the access is denied, the Octopus UI displays a dedicated screen for this reason in place of the
/// community: your app has nothing to display. Mirrors the native iOS <c>OctopusCommunityAccessDeniedReason</c>
/// and Android <c>CommunityAccessDeniedReason</c>.
/// </summary>
public enum OctopusCommunityAccessDeniedReason
{
    /// <summary>The user is not part of the audience the community is opened to (for example, they are out of
    /// the A/B test cohort).</summary>
    NotInCohort,
    /// <summary>The community is disabled for a while. It is expected to come back.</summary>
    CommunityTemporarilyDisabled,
    /// <summary>The community is closed.</summary>
    CommunityClosed,
    /// <summary>The member is excluded from the community.</summary>
    MemberExcluded,
    /// <summary>The account of the member is banned. The details of the ban (the policy that was broken, who to
    /// contact) are displayed by the Octopus UI and are not exposed. <c>ConnectUser</c> keeps reporting a banned
    /// member with <see cref="OctopusClientUserErrorCode.UserBanned"/>.</summary>
    AccountBanned,
    /// <summary>The account of the member has been deleted. Your app can disconnect the member on its side, with
    /// <see cref="OctopusSDK.DisconnectUser"/>. The Octopus UI lets the member access the community again from
    /// its screen.</summary>
    AccountDeleted,
    /// <summary>The server gives no reason, or gives one that this version of the SDK does not know.</summary>
    Unknown
}
