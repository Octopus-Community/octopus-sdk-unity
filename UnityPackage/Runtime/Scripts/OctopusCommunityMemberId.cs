using System;

/// <summary>Identifies a community member by exactly one Octopus profile id or host client user id.</summary>
public sealed class OctopusCommunityMemberId
{
    /// <summary>The Octopus profile id, or null when this identifier uses ClientUserId.</summary>
    public string ProfileId { get; private set; }

    /// <summary>The host's user id, or null when this identifier uses ProfileId.
    /// Looking up this id requires the community to expose client user ids.</summary>
    public string ClientUserId { get; private set; }

    private OctopusCommunityMemberId(string profileId, string clientUserId)
    {
        ProfileId = profileId;
        ClientUserId = clientUserId;
    }

    /// <summary>Identifies a member by their Octopus profile id.</summary>
    /// <exception cref="ArgumentException">profileId is null or empty.</exception>
    public static OctopusCommunityMemberId FromProfileId(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) throw new ArgumentException("A profile id is required.", "profileId");
        return new OctopusCommunityMemberId(profileId, null);
    }

    /// <summary>Identifies a member by the host's user id. Requires exposed client user ids.</summary>
    /// <exception cref="ArgumentException">clientUserId is null or empty.</exception>
    public static OctopusCommunityMemberId FromClientUserId(string clientUserId)
    {
        if (string.IsNullOrEmpty(clientUserId)) throw new ArgumentException("A client user id is required.", "clientUserId");
        return new OctopusCommunityMemberId(null, clientUserId);
    }

    internal string Key { get { return ProfileId != null ? "profile:" + ProfileId : "client:" + ClientUserId; } }
}
