using System;
using System.Collections.Generic;

public sealed class ReactionsScenario : CommunityScenarioPilot
{

    public ReactionsScenario() : base("reactions",
        new OctopusScenarioField("postId", "Post id"),
        new OctopusScenarioField("reaction", "Reaction (heart, joy, null, mouthOpen, clap, cry, rage)"))
    {
        Add(1, PresetLabel(1, "React heart ❤️"), OctopusSampleFixtures.PostReactionStackId ?? string.Empty, "heart");
        Add(2, PresetLabel(2, "Change reaction to joy 😂"), OctopusSampleFixtures.PostReactionStackId ?? string.Empty, "joy");
        Add(3, PresetLabel(3, "Unreact (null)"), OctopusSampleFixtures.PostReactionStackId ?? string.Empty, "null");
        Add(4, PresetLabel(4, "React mouthOpen 😮"), OctopusSampleFixtures.PostReactionStackId ?? string.Empty, "mouthOpen");
        Add(5, PresetLabel(5, "React clap 👏"), OctopusSampleFixtures.PostReactionStackId ?? string.Empty, "clap");
        Add(6, PresetLabel(6, "React cry 😢"), OctopusSampleFixtures.PostReactionStackId ?? string.Empty, "cry");
        Add(7, PresetLabel(7, "React rage 😡"), OctopusSampleFixtures.PostReactionStackId ?? string.Empty, "rage");
    }

    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "SetReaction" }; } }
    public override string ParameterNotice
    { get { return "Presets use the shared post.reactionStack fixture. Customize can target another post. Connection and authentication failures use ReactionError in Unity."; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var postId = fields.Get("postId").Trim();
        var label = fields.Get("reaction").Trim();
        OctopusReactionKind? kind;
        if (!TryReaction(label, out kind) || postId.Length == 0)
        { Report("No call made: supply a post id and a supported reaction, or null to unreact."); return; }
        ReportRunning("setReaction(" + label + ")…");
        Announce("SetReaction", "postId=" + postId + ", reaction=" + label);
        OctopusScenarioSdk.Current.SetReaction(postId, kind,
            () => Report("setReaction(" + label + ") → success. postId=" + postId),
            error => Report("setReaction(" + label + ") → typed error: " + error.Code + " — " + error.Message));
    }

    internal static bool TryReaction(string value, out OctopusReactionKind? kind)
    {
        kind = null;
        switch (value.ToLowerInvariant())
        {
            case "null": case "none": return true;
            case "heart": kind = OctopusReactionKind.Heart; return true;
            case "joy": kind = OctopusReactionKind.Joy; return true;
            case "mouthopen": kind = OctopusReactionKind.MouthOpen; return true;
            case "clap": kind = OctopusReactionKind.Clap; return true;
            case "cry": kind = OctopusReactionKind.Cry; return true;
            case "rage": kind = OctopusReactionKind.Rage; return true;
            default: return false;
        }
    }
}
