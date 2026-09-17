using System;
using System.Collections.Generic;

public sealed class InitialScreenScenario : OctopusScenarioPilot
{
    public const string DefaultText = "A long enough prefill text from the host app demonstration scenario.";
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("preset", "Destination (preset number 1–11)"),
        new OctopusScenarioField("postId", "Post id (presets 2 & 5; auto uses the last Bridge post)"),
        new OctopusScenarioField("groupId", "Group (presets 3, 4 & 6; auto selects General / first, none skips)"),
        new OctopusScenarioField("text", "Prefill text (10–5000 characters)"),
        new OctopusScenarioField("ctaLabel", "CTA label (optional; none skips)"),
        new OctopusScenarioField("ctaUrl", "CTA url (optional; none skips)"),
        new OctopusScenarioField("clientUserId", "Member clientUserId (preset 10 only; current uses connected / configured user)"));
    private readonly List<OctopusScenarioPreset> _presets = new List<OctopusScenarioPreset>();
    private int _generation;

    public InitialScreenScenario() : base("initialScreen")
    {
        var labels = new[]
        {
            "Open main feed", "Open post (bridge mode)", "Open group (bridge mode)",
            "Open createPost (prefilled)", "Open standalone OctopusPostDetailsScreen",
            "Standalone editor + signed image share",
            "Open via showOctopusHomeScreen helper (auto inset)",
            "Open member activity (by clientUserId)", "Open member activity (by profileId)",
            "Open member profile (OctopusProfileScreen)", "Open my own profile (no id)"
        };
        for (var i = 0; i < labels.Length; i++)
        {
            var number = i + 1;
            _presets.Add(new OctopusScenarioPreset(PresetTestId(number), PresetLabel(number, labels[i]), fields =>
            {
                fields.Set("preset", number.ToString());
                fields.Set("postId", "auto");
                fields.Set("groupId", "auto");
                fields.Set("text", DefaultText);
                fields.Set("ctaLabel", "none");
                fields.Set("ctaUrl", "none");
                fields.Set("clientUserId", "current");
            }, Run));
        }
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override bool CanCustomize { get { return true; } }
    public override void RunCustom() { Run(Fields); }
    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "Open", "OpenPost", "OpenGroup", "OpenCreatePost", "OpenProfile", "FetchGroups", "CurrentProfile" }; } }
    public override string ParameterNotice
    {
        get { return "Unity opens native screens. Presets 5 and 7 use OpenPost / Open equivalents; there are no Flutter widgets or inset parameters. " +
            "Use Customize for ids and prefill values; group selection uses a text field. Presets 8 and 9 need member activity APIs Unity does not expose. " +
            "Preset 6 uses the bundled image. " + OctopusScenarioSdk.BridgeSigningNotice + " Only the member can publish."; }
    }

    private void Run(OctopusScenarioFields fields)
    {
        var generation = ++_generation;
        int preset;
        if (!int.TryParse(fields.Get("preset"), out preset) || preset < 1 || preset > 11)
        { Report("No call made: destination must be a preset number from 1 to 11."); return; }
        if (preset == 8 || preset == 9)
        {
            Report("Unavailable: Unity OpenActivity only opens the connected user's activity and accepts no " +
                (preset == 8 ? "clientUserId" : "profileId") + "; no SDK call made.");
            return;
        }
        try
        {
            string reason;
            var profile = OctopusScenarioSdk.EnsurePilotInitialized(out reason);
            if (profile == null) { Report(reason); return; }
            var sdk = OctopusScenarioSdk.Current;
            if (preset == 1 || preset == 7)
            {
                Open("Open", () => sdk.Open(), preset == 1
                    ? "Opening the default initial screen (main feed). Close the SDK to return."
                    : "Opening the community via OctopusSDK.Open(), the same entry point as preset 1 in Unity. No separate home-screen helper or inset parameter is exercised. Close the SDK to return.");
                return;
            }
            if (preset == 2 || preset == 5)
            {
                var postId = fields.Get("postId").Trim();
                if (postId == "auto") postId = OctopusScenarioSdk.LatestBridgePostId;
                if (string.IsNullOrWhiteSpace(postId))
                { Report("Post id is empty — fill the Post id field in Customize, or run the Bridge scenario first."); return; }
                Open("OpenPost", () => sdk.OpenPost(postId), preset == 5
                    ? "Opening post via OctopusSDK.OpenPost, the same entry point as preset 2 in Unity: " + postId + ". No standalone post-details screen is exercised. Close the SDK to return."
                    : "Opening post bridge mode via OctopusSDK.OpenPost: " + postId + ". Close the SDK to return.");
                return;
            }
            if (preset == 10 || preset == 11)
            {
                string memberId = null;
                if (preset == 10)
                {
                    memberId = fields.Get("clientUserId").Trim();
                    if (memberId == "current" || memberId.Length == 0)
                    {
                        Announce("CurrentProfile");
                        var current = sdk.CurrentProfile;
                        memberId = current == null || string.IsNullOrWhiteSpace(current.ClientUserId)
                            ? profile.userId : current.ClientUserId;
                    }
                    if (string.IsNullOrWhiteSpace(memberId))
                    { Report("No member clientUserId — fill the Member clientUserId field in Customize, or connect a user."); return; }
                    memberId = memberId.Trim();
                }
                Open("OpenProfile", () => sdk.OpenProfile(memberId), preset == 11
                    ? "Opening my own profile with no clientUserId, including its edit affordances. Without a connected user the SDK shows its unavailable state."
                    : "Opening the member profile via OctopusSDK.OpenProfile: " + memberId + ". Client user ids must be exposed by the community; unknown ids show unavailable UI.");
                return;
            }
            OctopusPrefilledPost prefill = null;
            if (preset == 4 || preset == 6)
            {
                var text = fields.Get("text");
                if (text.Length < 10 || text.Length > 5000)
                { Report("OctopusPrefilledPost rejected: text must contain 10–5000 characters. The editor was not opened."); return; }
                var label = Optional(fields.Get("ctaLabel"));
                var url = Optional(fields.Get("ctaUrl"));
                Uri parsed;
                if ((label != null || url != null) && (label == null || url == null ||
                    !Uri.TryCreate(url, UriKind.Absolute, out parsed) || (parsed.Scheme != "http" && parsed.Scheme != "https")))
                { Report("OctopusPrefilledPost rejected: CTA needs a label and an absolute HTTP(S) URL, or both fields empty / none."); return; }
                prefill = new OctopusPrefilledPost { Text = text, CtaLabel = label, CtaUrl = url };
                if (preset == 6)
                {
                    prefill.ImagePath = sdk.PrepareBundledShareImage();
                    prefill.SignBridgeShare = sdk.SignBridgeShare;
                }
            }
            var groupId = fields.Get("groupId").Trim();
            if (groupId == "auto")
            {
                ReportRunning("Fetching target groups…");
                Announce("FetchGroups");
                sdk.FetchGroups(groups =>
                {
                    if (generation != _generation) return;
                    string selected = groups.Count == 0 ? null : groups[0].Id;
                    foreach (var group in groups)
                        if (group.Name == "General") { selected = group.Id; break; }
                    OpenGrouped(sdk, preset, selected, prefill);
                }, error => { if (generation == _generation) Report("FetchGroups failed: " + error); });
            }
            else OpenGrouped(sdk, preset, Optional(groupId), prefill);
        }
        catch (Exception error) { Report("Failed to open initial screen: " + error.Message); }
    }

    private void OpenGrouped(IOctopusScenarioSdk sdk, int preset, string groupId, OctopusPrefilledPost prefill)
    {
        if (preset == 3)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            { Report("No group available — fetch groups in the Community tab or enter a Group id in Customize."); return; }
            Open("OpenGroup", () => sdk.OpenGroup(groupId), "Opening group bridge mode via OctopusSDK.OpenGroup: " + groupId + ". Close the SDK to return.");
        }
        else
        {
            if (prefill == null)
            { Report("No call made: post editor prefill is missing."); return; }
            prefill.TopicId = groupId;
            Open("OpenCreatePost", () => sdk.OpenCreatePost(prefill),
                "Opening the native post editor using the prefill inputs; group: " + (groupId ?? "(member picks)") + ". " +
                (preset == 6 ? "Bundled image share. " + OctopusScenarioSdk.BridgeSigningNotice + " " : "") +
                "Only the member decides whether to publish; close the editor to return.");
        }
    }

    private void Open(string method, Action action, string result)
    {
        try { Announce(method); action(); Report(result); }
        catch (Exception error) { Report("Failed to open initial screen: " + error.Message); }
    }
    private static void Announce(string method) { OctopusSampleLog.Current.LogApiCall("OctopusSDK." + method); }
    private static string Optional(string value)
    { var trimmed = value.Trim(); return trimmed.Length == 0 || trimmed == "none" ? null : trimmed; }
    public override void Dispose() { ++_generation; base.Dispose(); }
}
