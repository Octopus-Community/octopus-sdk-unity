using System;
using System.Globalization;
using System.Threading.Tasks;

/// <summary>
/// The two-way mapping between a finished run and the SDK's client object.
///
/// Sharing a score is the sample's honest use of the client-object bridge: the run is the host's
/// own object, the post is the community's mirror of it, and `ObjectId` is the only thing the two
/// sides agree on. So the id has to carry everything the game needs to rebuild the run when the
/// SDK hands it back through `OctopusSDK.OnNavigateToClientObject` — here, the run id and the
/// score — and it has to survive a round trip through a backend that treats it as an opaque key.
///
/// Hence a strict, printable, single-line format, parsed back with no allocation surprises:
/// <c>reef-run-&lt;8 lowercase hex&gt;-&lt;score&gt;</c>. It satisfies the SDK's own precondition on
/// `ObjectId` (non-empty, no newline or carriage return) by construction, and
/// <see cref="TryParse"/> rejects anything else — including the ids of the `bridge` scenario,
/// which shares the same callback.
///
/// Nothing here touches the SDK: it builds the value object and reads it back, so both directions
/// are covered by EditMode tests.
/// </summary>
public static class OctopusReefRunShare
{
    /// <summary>Marks the ids this game owns among every client object the community may hold.</summary>
    public const string ObjectIdPrefix = "reef-run-";

    /// <summary>Lowercase hex characters in a run id.</summary>
    public const int RunIdLength = 8;

    /// <summary>The CTA the SDK renders on the shared post; tapping it raises OnNavigateToClientObject.</summary>
    public const string ViewObjectButtonText = "Take the challenge";

    /// <summary>A fresh run id. Random rather than sequential: two devices must not collide.</summary>
    public static string NewRunId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, RunIdLength);
    }

    /// <summary>The client object id for a run. Throws on a run id this class did not mint.</summary>
    public static string ObjectId(string runId, int score)
    {
        if (!IsRunId(runId))
            throw new ArgumentException("A run id is " + RunIdLength + " lowercase hex characters.", "runId");
        if (score < 0) throw new ArgumentOutOfRangeException("score", "A score is never negative.");
        return ObjectIdPrefix + runId + "-" + score.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads back an id produced by <see cref="ObjectId"/>. False — with both outputs cleared —
    /// for every other string, so the navigate callback can be shared with other client objects.
    /// </summary>
    public static bool TryParse(string objectId, out string runId, out int score)
    {
        runId = null;
        score = 0;
        if (string.IsNullOrEmpty(objectId)) return false;
        if (!objectId.StartsWith(ObjectIdPrefix, StringComparison.Ordinal)) return false;
        var rest = objectId.Substring(ObjectIdPrefix.Length);
        if (rest.Length < RunIdLength + 2) return false;
        if (rest[RunIdLength] != '-') return false;
        var id = rest.Substring(0, RunIdLength);
        if (!IsRunId(id)) return false;
        var digits = rest.Substring(RunIdLength + 1);
        int parsed;
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)) return false;
        // "007" and "7" would be two ids for one run; only the canonical spelling is accepted.
        if (parsed.ToString(CultureInfo.InvariantCulture) != digits) return false;
        runId = id;
        score = parsed;
        return true;
    }

    /// <summary>The post body the SDK creates the first time a run is shared.</summary>
    public static string PostText(int score)
    {
        return "I cleared " + Reefs(score) + " in Reef Run, the mini-game built into the Octopus " +
               "SDK sample for Unity. One tap keeps the octopus swimming — tap the button below " +
               "to take the same challenge and try to beat me.";
    }

    /// <summary>The line the SDK shows under the post body.</summary>
    public static string CatchPhrase(int score)
    {
        return "Reef Run · " + Reefs(score) + " to beat";
    }

    /// <summary>"1 reef" / "7 reefs", used by every string above and by the screen itself.</summary>
    public static string Reefs(int score)
    {
        return score.ToString(CultureInfo.InvariantCulture) + (score == 1 ? " reef" : " reefs");
    }

    /// <summary>
    /// The value handed to `OctopusSDK.FetchOrCreateClientObjectRelatedPost`. `GroupId` is left
    /// null on purpose — the community's configured default group is the right home for a score,
    /// and the sample has no reason to guess a group name here.
    /// </summary>
    public static OctopusClientObject ClientObject(string runId, int score, string imagePath,
                                                   Func<string, Task<string>> signBridgeShare)
    {
        return new OctopusClientObject
        {
            ObjectId = ObjectId(runId, score),
            Text = PostText(score),
            CatchPhrase = CatchPhrase(score),
            ViewObjectButtonText = ViewObjectButtonText,
            ImagePath = imagePath,
            SignBridgeShare = signBridgeShare,
        };
    }

    private static bool IsRunId(string value)
    {
        if (value == null || value.Length != RunIdLength) return false;
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if ((c < '0' || c > '9') && (c < 'a' || c > 'f')) return false;
        }
        return true;
    }
}
