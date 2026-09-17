/// <summary>A read-only snapshot of a member's public community activity.</summary>
public sealed class OctopusCommunityData
{
    /// <summary>The member's Octopus profile id (profileId on the wire).</summary>
    public string ProfileId { get; private set; }

    /// <summary>Total posts, comments and replies, or null when unavailable.</summary>
    public int? MessageCount { get; private set; }

    /// <summary>The member's standing, or null when gamification is unavailable.</summary>
    public OctopusGamification Gamification { get; private set; }

    /// <summary>Creates an immutable community-data snapshot.</summary>
    public OctopusCommunityData(string profileId, int? messageCount = null, OctopusGamification gamification = null)
    {
        ProfileId = profileId ?? "";
        MessageCount = messageCount;
        Gamification = gamification;
    }
}

/// <summary>A member's read-only gamification standing.</summary>
public sealed class OctopusGamification
{
    /// <summary>The community's level index.</summary>
    public int Level { get; private set; }

    /// <summary>Points, or null when unavailable. The pinned native SDKs do not expose scores on public profiles.</summary>
    public int? Score { get; private set; }

    /// <summary>Creates an immutable gamification snapshot.</summary>
    public OctopusGamification(int level, int? score = null)
    {
        Level = level;
        Score = score;
    }
}
