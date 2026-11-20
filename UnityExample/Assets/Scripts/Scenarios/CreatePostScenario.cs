using System;
using System.Collections.Generic;

public sealed class CreatePostScenario : CommunityScenarioPilot
{
    public const string DefaultText = "Bridge Share preset — a host-supplied prefilled post from the Unity example app.";
    /// <summary>The other languages of the CTA, used once Per-language label is set to true.</summary>
    public const string Translations = "fr=Ouvrir; es=Abrir";

    public CreatePostScenario() : base("createPost",
        new OctopusScenarioField("withText", "Include text (true or false)"),
        new OctopusScenarioField("withCta", "Include CTA (true or false)"),
        new OctopusScenarioField("withImage", "Include bundled image (true or false)"),
        new OctopusScenarioField("imageCount", "Bundled image copies when included (1–10; 11 shows the too-many-images refusal)"),
        new OctopusScenarioField("text", "Prefilled text (up to 5000 characters when included)"),
        new OctopusScenarioField("ctaLabel", "CTA label"),
        new OctopusScenarioField("ctaUrl", "CTA URL"),
        new OctopusScenarioField("groupId", "Target group id (auto selects General / first, none lets member choose)"),
        new OctopusScenarioField("ctaPerLanguage", OctopusSampleLocalizedLabel.PerLanguageFieldLabel),
        new OctopusScenarioField("ctaLanguage", OctopusSampleLocalizedLabel.LanguageFieldLabel),
        new OctopusScenarioField("ctaTranslations", OctopusSampleLocalizedLabel.TranslationsFieldLabel))
    {
        Add(1, PresetLabel(1, "Text only"), "true", "false", "false", "1", DefaultText, "Open", "https://octopuscommunity.com/preset", OctopusSampleFixtures.DefaultTopicId ?? string.Empty, "false", "en", Translations);
        Add(2, PresetLabel(2, "Text + CTA"), "true", "true", "false", "1", DefaultText, "Open", "https://octopuscommunity.com/preset", OctopusSampleFixtures.DefaultTopicId ?? string.Empty, "false", "en", Translations);
        Add(3, PresetLabel(3, "Text + bundled image"), "true", "false", "true", "1", DefaultText, "Open", "https://octopuscommunity.com/preset", OctopusSampleFixtures.DefaultTopicId ?? string.Empty, "false", "en", Translations);
        Add(4, PresetLabel(4, "Full (text + CTA + bundled image)"), "true", "true", "true", "1", DefaultText, "Open", "https://octopuscommunity.com/preset", OctopusSampleFixtures.DefaultTopicId ?? string.Empty, "false", "en", Translations);
        Add(5, PresetLabel(5, "Image only (bundled)"), "false", "false", "true", "1", DefaultText, "Open", "https://octopuscommunity.com/preset", OctopusSampleFixtures.DefaultTopicId ?? string.Empty, "false", "en", Translations);
    }

    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "FetchGroups", "OpenCreatePost" }; } }
    public override string ParameterNotice
    { get { return "Opens the editor; the member decides whether to publish. The CTA appears on the published post. Images use the bundled sample logo, repeated Image copies times (a post carries up to " + OctopusPrefilledPost.MaxImageCount + "). " + OctopusScenarioSdk.BridgeSigningNotice + " On this screen, signing is requested only when the share includes an image and the community restricts member pictures."; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        bool withText, withCta, withImage;
        if (!bool.TryParse(fields.Get("withText"), out withText) ||
            !bool.TryParse(fields.Get("withCta"), out withCta) ||
            !bool.TryParse(fields.Get("withImage"), out withImage))
        { Report("No call made: include flags must be true or false."); return; }
        int imageCount = 0;
        if (withImage && (!int.TryParse(fields.Get("imageCount").Trim(), out imageCount) || imageCount < 1))
        { Report("No call made: image copies must be a whole number from 1."); return; }
        var text = withText ? fields.Get("text").Trim() : null;
        if (withText && text.Length > 5000)
        { Report("Prefilled post rejected: text must contain at most 5000 characters. The editor was not opened."); return; }
        var label = withCta ? fields.Get("ctaLabel").Trim() : null;
        var url = withCta ? fields.Get("ctaUrl").Trim() : null;
        Uri parsed;
        if (withCta && (string.IsNullOrEmpty(label) || !Uri.TryCreate(url, UriKind.Absolute, out parsed) ||
            (parsed.Scheme != "https" && parsed.Scheme != "http")))
        { Report("Prefilled post rejected: CTA needs a label and an absolute HTTP(S) URL. The editor was not opened."); return; }
        OctopusLocalizedText localizedLabel = null;
        if (withCta)
        {
            string labelError;
            localizedLabel = OctopusSampleLocalizedLabel.Build(fields.Get("ctaPerLanguage"), label,
                fields.Get("ctaLanguage"), fields.Get("ctaTranslations"), out labelError);
            if (labelError != null)
            { Report("Prefilled post rejected: CTA label refused — " + labelError + ". The editor was not opened."); return; }
        }
        var sdk = OctopusScenarioSdk.Current;
        var prefill = new OctopusPrefilledPost
        {
            Text = text, CtaLocalizedLabel = localizedLabel, CtaUrl = url,
            SignBridgeShare = withImage ? sdk.SignBridgeShare : (Func<string, System.Threading.Tasks.Task<string>>)null
        };
        if (withImage)
        {
            var image = sdk.PrepareBundledShareImage();
            // Keep one excess image so the SDK can still demonstrate its refusal.
            var copies = Math.Min(imageCount, OctopusPrefilledPost.MaxImageCount + 1);
            for (int i = 0; i < copies; i++) prefill.ImagePaths.Add(image);
        }
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
                " · images: " + (prefill.ImagePaths.Count == 0 ? "(none)" : prefill.ImagePaths.Count + " × bundled sample logo") +
                " · group: " + (prefill.TopicId ?? "(member picks)") +
                " · CTA: " + (prefill.CtaLocalizedLabel == null ? "(none)"
                    : OctopusSampleLocalizedLabel.Describe(prefill.CtaLocalizedLabel) + " → " + prefill.CtaUrl);
            Announce("OpenCreatePost", summary);
            sdk.OpenCreatePost(prefill);
            Report("Opening the post editor prefilled with — " + summary +
                   ". Publishing requires a connected member; close the editor to come back here.");
        }
        catch (OctopusPrefilledPostTooManyImagesException error)
        {
            Report("OctopusPrefilledPost rejected: " + error.Count + " images, at most " + error.Max +
                   " accepted (OctopusPrefilledPostTooManyImagesException). The editor was not opened.");
        }
        catch (Exception error) { Report("Failed to open the post editor: " + error.Message); }
    }
}
