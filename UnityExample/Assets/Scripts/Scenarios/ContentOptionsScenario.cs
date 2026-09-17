using System.Collections.Generic;

public sealed class ContentOptionsScenario : CommunityScenarioPilot
{
    public ContentOptionsScenario() : base("contentOptions",
        new OctopusScenarioField("action", "Action (apply or clear)"),
        new OctopusScenarioField("postPictures", "Post: enable pictures"),
        new OctopusScenarioField("postPolls", "Post: enable polls"),
        new OctopusScenarioField("commentPictures", "Comment: enable pictures"),
        new OctopusScenarioField("replyPictures", "Reply: enable pictures"))
    {
        Add(1, PresetLabel(1, "All enabled (default / no-op)"), "apply", "true", "true", "true", "true");
        Add(2, PresetLabel(2, "Post: pictures off"), "apply", "false", "true", "true", "true");
        Add(3, PresetLabel(3, "Post: polls off"), "apply", "true", "false", "true", "true");
        Add(4, PresetLabel(4, "Post: pictures + polls off"), "apply", "false", "false", "true", "true");
        Add(5, PresetLabel(5, "Comment: pictures off"), "apply", "true", "true", "false", "true");
        Add(6, PresetLabel(6, "Reply: pictures off"), "apply", "true", "true", "true", "false");
        Add(7, "Clear override (backend default)", "clear", "backend", "backend", "backend", "backend");
    }

    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "DebugOverrideContentOptions" }; } }
    public override string ParameterNotice
    { get { return "Debug/QA only. Controls creation affordances, not existing content. Clear restores backend configuration. The SDK call has no completion acknowledgement. Last override sent: " + OctopusScenarioSdk.ContentOptionsSummary; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var action = fields.Get("action").Trim().ToLowerInvariant();
        OctopusContentOptions options = null;
        if (action == "apply")
        {
            bool postPictures, postPolls, commentPictures, replyPictures;
            if (!bool.TryParse(fields.Get("postPictures"), out postPictures) ||
                !bool.TryParse(fields.Get("postPolls"), out postPolls) ||
                !bool.TryParse(fields.Get("commentPictures"), out commentPictures) ||
                !bool.TryParse(fields.Get("replyPictures"), out replyPictures))
            { Report("No call made: content flags must be true or false."); return; }
            options = new OctopusContentOptions(new OctopusPostOptions(postPictures, postPolls),
                new OctopusCommentOptions(commentPictures), new OctopusReplyOptions(replyPictures));
        }
        else if (action != "clear")
        { Report("No call made: action must be apply or clear."); return; }
        var summary = options == null ? "none (backend default)" :
            "post.enablePictures=" + options.Post.EnablePictures + ", post.enablePolls=" + options.Post.EnablePolls +
            ", comment.enablePictures=" + options.Comment.EnablePictures + ", reply.enablePictures=" + options.Reply.EnablePictures;
        Announce("DebugOverrideContentOptions", summary);
        OctopusScenarioSdk.Current.DebugOverrideContentOptions(options);
        OctopusScenarioSdk.ContentOptionsSummary = summary;
        Report("Applied: " + summary + ". Override call sent; open Community and start a post (or comment / reply) to verify the creation options.");
    }
}
