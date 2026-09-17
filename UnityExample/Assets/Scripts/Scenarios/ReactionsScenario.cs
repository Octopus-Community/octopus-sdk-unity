using System;
using System.Collections.Generic;

public sealed class ReactionsScenario : CommunityScenarioPilot
{
    public const string FallbackPostId = "unity-demo-fake-post-id";

    public ReactionsScenario() : base("reactions",
        new OctopusScenarioField("postId", "Post id"),
        new OctopusScenarioField("reaction", "Reaction (heart, joy, null, mouthOpen, clap, cry, rage)"))
    {
        Add(1, PresetLabel(1, "React heart ❤️"), FallbackPostId, "heart");
        Add(2, PresetLabel(2, "Change reaction to joy 😂"), FallbackPostId, "joy");
        Add(3, PresetLabel(3, "Unreact (null)"), FallbackPostId, "null");
        Add(4, PresetLabel(4, "React mouthOpen 😮"), FallbackPostId, "mouthOpen");
        Add(5, PresetLabel(5, "React clap 👏"), FallbackPostId, "clap");
        Add(6, PresetLabel(6, "React cry 😢"), FallbackPostId, "cry");
        Add(7, PresetLabel(7, "React rage 😡"), FallbackPostId, "rage");
    }

    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "SetReaction" }; } }
    public override string ParameterNotice
    { get { return "Presets use an intentionally fake post id to exercise PostNotFound. Customize can target a real post. Connection and authentication failures use ReactionError in Unity."; } }

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
