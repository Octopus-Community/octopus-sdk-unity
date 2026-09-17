using System;
using System.Collections.Generic;

public sealed class CreatePostScenario : CommunityScenarioPilot
{
    public const string DefaultText = "Bridge Share preset — a host-supplied prefilled post from the Unity example app.";

    public CreatePostScenario() : base("createPost",
        new OctopusScenarioField("withText", "Include text (true or false)"),
        new OctopusScenarioField("withCta", "Include CTA (true or false)"),
        new OctopusScenarioField("withImage", "Include bundled image (true or false)"),
        new OctopusScenarioField("text", "Prefilled text (10–5000 characters when included)"),
        new OctopusScenarioField("ctaLabel", "CTA label"),
        new OctopusScenarioField("ctaUrl", "CTA URL"),
        new OctopusScenarioField("groupId", "Target group id (auto selects General / first, none lets member choose)"))
    {
        Add(1, PresetLabel(1, "Text only"), "true", "false", "false", DefaultText, "Open", "https://octopuscommunity.com/preset", "auto");
        Add(2, PresetLabel(2, "Text + CTA"), "true", "true", "false", DefaultText, "Open", "https://octopuscommunity.com/preset", "auto");
        Add(3, PresetLabel(3, "Text + bundled image"), "true", "false", "true", DefaultText, "Open", "https://octopuscommunity.com/preset", "auto");
        Add(4, PresetLabel(4, "Full (text + CTA + bundled image)"), "true", "true", "true", DefaultText, "Open", "https://octopuscommunity.com/preset", "auto");
        Add(5, PresetLabel(5, "Image only (bundled)"), "false", "false", "true", DefaultText, "Open", "https://octopuscommunity.com/preset", "auto");
    }

    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "FetchGroups", "OpenCreatePost" }; } }
    public override string ParameterNotice
    { get { return "Opens the editor; the member decides whether to publish. The CTA appears on the published post. Images use the bundled sample logo. " + OctopusScenarioSdk.BridgeSigningNotice + " On this screen, signing is requested only when the share includes an image and the community restricts member pictures."; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        bool withText, withCta, withImage;
        if (!bool.TryParse(fields.Get("withText"), out withText) ||
            !bool.TryParse(fields.Get("withCta"), out withCta) ||
            !bool.TryParse(fields.Get("withImage"), out withImage))
        { Report("No call made: include flags must be true or false."); return; }
        var text = withText ? fields.Get("text").Trim() : null;
        if (withText && (text.Length < 10 || text.Length > 5000))
        { Report("Prefilled post rejected: text must contain 10–5000 characters. The editor was not opened."); return; }
        var label = withCta ? fields.Get("ctaLabel").Trim() : null;
        var url = withCta ? fields.Get("ctaUrl").Trim() : null;
        Uri parsed;
        if (withCta && (string.IsNullOrEmpty(label) || !Uri.TryCreate(url, UriKind.Absolute, out parsed) ||
            (parsed.Scheme != "https" && parsed.Scheme != "http")))
        { Report("Prefilled post rejected: CTA needs a label and an absolute HTTP(S) URL. The editor was not opened."); return; }
        var sdk = OctopusScenarioSdk.Current;
        var prefill = new OctopusPrefilledPost
        {
            Text = text, CtaLabel = label, CtaUrl = url,
            ImagePath = withImage ? sdk.PrepareBundledShareImage() : null,
            SignBridgeShare = withImage ? sdk.SignBridgeShare : (Func<string, System.Threading.Tasks.Task<string>>)null
        };
        var groupId = fields.Get("groupId").Trim();
        if (groupId == "auto")
        {
            ReportRunning("Fetching target groups…");
            Announce("FetchGroups");
            sdk.FetchGroups(groups =>
            {
                prefill.TopicId = groups.Count == 0 ? null : groups[0].Id;
                foreach (var group in groups)
                    if (group.Name == "General") { prefill.TopicId = group.Id; break; }
                Open(sdk, prefill);
            }, error => Report("FetchGroups failed: " + error));
        }
        else
        {
            prefill.TopicId = groupId.Length == 0 || groupId == "none" ? null : groupId;
            Open(sdk, prefill);
        }
    }

    private void Open(IOctopusScenarioSdk sdk, OctopusPrefilledPost prefill)
    {
        try
        {
            var summary = "text: " + (prefill.Text == null ? "(none)" : prefill.Text.Length + " chars") +
                " · image: " + (prefill.ImagePath == null ? "(none)" : "bundled sample logo") +
                " · group: " + (prefill.TopicId ?? "(member picks)") +
                " · CTA: " + (prefill.CtaLabel == null ? "(none)" : prefill.CtaLabel + " → " + prefill.CtaUrl);
            Announce("OpenCreatePost", summary);
            sdk.OpenCreatePost(prefill);
            Report("Opening the post editor prefilled with — " + summary +
                   ". Publishing requires a connected member; close the editor to come back here.");
        }
        catch (Exception error) { Report("Failed to open the post editor: " + error.Message); }
    }
}
