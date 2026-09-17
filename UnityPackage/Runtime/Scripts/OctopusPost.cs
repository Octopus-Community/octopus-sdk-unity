using System.Collections.Generic;

/// <summary>A read-only count for one reaction kind on a post. An absent kind has count zero.</summary>
public sealed class OctopusReactionCount
{
    /// <summary>The reaction kind; unfamiliar native kinds become Unknown.</summary>
    public OctopusReactionKind ReactionKind { get; private set; }
    /// <summary>Number of reactions of this kind.</summary>
    public int Count { get; private set; }
    /// <summary>Creates an immutable reaction count.</summary>
    public OctopusReactionCount(OctopusReactionKind reactionKind, int count)
    {
        ReactionKind = reactionKind;
        Count = count;
    }
}

/// <summary>The public post snapshot shared by Android 1.13.4 and iOS 1.13.2.
/// These native interfaces do not expose text, author or creation date.</summary>
public sealed class OctopusPost
{
    /// <summary>Post identifier, suitable for OctopusSDK.OpenPost.</summary>
    public string Id { get; private set; }
    /// <summary>Reaction counts. A kind absent from this list has count zero.</summary>
    public IReadOnlyList<OctopusReactionCount> Reactions { get; private set; }
    /// <summary>Total number of comments and replies.</summary>
    public int CommentCount { get; private set; }
    /// <summary>Number of views.</summary>
    public int ViewCount { get; private set; }
    /// <summary>The current user's reaction, or null when they have not reacted.</summary>
    public OctopusReactionKind? UserReactionKind { get; private set; }
    /// <summary>Creates an immutable post snapshot, copying the supplied reaction collection.</summary>
    public OctopusPost(string id, IEnumerable<OctopusReactionCount> reactions = null,
        int commentCount = 0, int viewCount = 0, OctopusReactionKind? userReactionKind = null)
    {
        Id = id ?? "";
        Reactions = new List<OctopusReactionCount>(reactions ?? new OctopusReactionCount[0]).AsReadOnly();
        CommentCount = commentCount;
        ViewCount = viewCount;
        UserReactionKind = userReactionKind;
    }
}
