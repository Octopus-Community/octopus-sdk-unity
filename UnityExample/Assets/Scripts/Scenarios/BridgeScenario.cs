using System;
using System.Collections.Generic;

public sealed class BridgeScenario : CommunityScenarioPilot
{
    private const string ImageUrl = "https://media-content-demo.octocdn.net/misc/macarons.jpeg";
    /// <summary>The other languages of the button, used once Per-language label is set to true.</summary>
    public const string Translations = "fr=Voir la recette; es=Ver la receta";
    private bool _observing;
    private string _outcome = "Bridge state";

    public BridgeScenario() : base("bridge",
        new OctopusScenarioField("action", "Action (fetch, heart, unreact)"),
        new OctopusScenarioField("objectId", "Object id (random generates a fresh id at Run)"),
        new OctopusScenarioField("text", "Post text"),
        new OctopusScenarioField("catchPhrase", "Catch phrase"),
        new OctopusScenarioField("buttonText", "View object button text"),
        new OctopusScenarioField("imageUrl", "Remote image URL (first photo; the bundled sample logo follows)"),
        new OctopusScenarioField("groupName", "Group name (default uses the community default)"),
        new OctopusScenarioField("buttonPerLanguage", OctopusSampleLocalizedLabel.PerLanguageFieldLabel),
        new OctopusScenarioField("buttonLanguage", OctopusSampleLocalizedLabel.LanguageFieldLabel),
        new OctopusScenarioField("buttonTranslations", OctopusSampleLocalizedLabel.TranslationsFieldLabel))
    {
        Add(1, PresetLabel(1, "Stable recipe (objectId recipe-1)"), "fetch", "recipe-1",
            "The perfects Cannelés (Bordeaux specialty) — Unity demo bridge post linked to the stable recipe object. Tap View recipe inside the SDK feed to fire navigateToClientObject back to this scenario.",
            "A delicious recipe to test the bridge", "View recipe", ImageUrl, "default", "false", "en", Translations);
        Add(2, PresetLabel(2, "Random recipe (fresh objectId each tap)"), "fetch", "random",
            "French macaroons — Unity demo bridge post with a randomised objectId so every tap produces a brand-new post on the backend. Useful to exercise create-path validation end-to-end.",
            "Once baked and eaten, tell us what you think.", "View recipe", ImageUrl, "Gourmands", "false", "en", Translations);
        Add(3, PresetLabel(3, "Heart-react on latest post"), "heart", "latest", "unused", "unused", "unused", "unused", "unused", "unused", "unused", "unused");
        Add(4, PresetLabel(4, "Unreact on latest post"), "unreact", "latest", "unused", "unused", "unused", "unused", "unused", "unused", "unused", "unused");
    }

    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "FetchOrCreateClientObjectRelatedPost", "StartObservingClientObjectRelatedPost", "StopObservingClientObjectRelatedPost", "OnClientObjectRelatedPostChanged", "OnNavigateToClientObject", "SetReaction" }; } }
    public override string ParameterNotice
    { get { return OctopusScenarioSdk.BridgeSigningNotice + " " + NavigationSummary(); } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var action = fields.Get("action").Trim().ToLowerInvariant();
        var sdk = OctopusScenarioSdk.Current;
        if (action != "fetch" && action != "heart" && action != "unreact")
        { Report("No call made: action must be fetch, heart or unreact."); return; }
        OctopusScenarioSdk.EnsureBridgeObserving();
        if (!_observing)
        {
            Observe<bool>(h => OctopusScenarioSdk.BridgeStateChanged += h,
                h => OctopusScenarioSdk.BridgeStateChanged -= h,
                (owner, unused) => ((BridgeScenario)owner).Show());
            _observing = true;
        }
        if (action != "fetch")
        {
            var postId = OctopusScenarioSdk.LatestBridgePostId;
            if (string.IsNullOrEmpty(postId))
            { Report("No post yet — run Preset 1 (stable) or Preset 2 (random) first to fetch-or-create the bridge post."); return; }
            OctopusReactionKind? kind = action == "heart" ? (OctopusReactionKind?)OctopusReactionKind.Heart : null;
            ReportRunning("setReaction(" + action + ")…");
            Announce("SetReaction", "postId=" + postId + ", reaction=" + action);
            sdk.SetReaction(postId, kind,
                () => Finish("setReaction(" + action + ") succeeded on post " + postId + ". Counts update on the post stream."),
                error => Finish("setReaction typed error: " + error.Code + " — " + error.Message));
            return;
        }
        string labelError;
        var button = OctopusSampleLocalizedLabel.Build(fields.Get("buttonPerLanguage"), fields.Get("buttonText"),
            fields.Get("buttonLanguage"), fields.Get("buttonTranslations"), out labelError);
        if (labelError != null) { Report("No call made: view object button refused — " + labelError + "."); return; }
        var objectId = fields.Get("objectId").Trim();
        if (objectId == "random") objectId = "recipe-" + Guid.NewGuid().ToString("N");
        var clientObject = new OctopusClientObject
        {
            ObjectId = objectId, Text = fields.Get("text"), CatchPhrase = fields.Get("catchPhrase"),
            ViewObjectButton = button, SignBridgeShare = sdk.SignBridgeShare
        };
        // Two photos, one remote and one local, in that order: a post carries up to
        // OctopusPrefilledPost.MaxImageCount.
        var imageUrl = fields.Get("imageUrl").Trim();
        if (imageUrl.Length > 0) clientObject.Medias.Add(OctopusClientObjectMedia.FromUrl(imageUrl));
        clientObject.Medias.Add(OctopusClientObjectMedia.FromPath(sdk.PrepareBundledShareImage()));
        var groupName = fields.Get("groupName").Trim();
        ReportRunning("Fetching bridge post…");
        if (groupName == "default") Fetch(sdk, clientObject);
        else
        {
            Announce("FetchGroups");
            sdk.FetchGroups(groups =>
            {
                foreach (var group in groups)
                    if (group.Name == groupName) { clientObject.GroupId = group.Id; break; }
                Fetch(sdk, clientObject);
            }, error => Finish("FetchGroups failed: " + error));
        }
    }

    private void Fetch(IOctopusScenarioSdk sdk, OctopusClientObject clientObject)
    {
        try
        {
            Announce("FetchOrCreateClientObjectRelatedPost", "objectId=" + clientObject.ObjectId +
                ", groupId=" + (clientObject.GroupId ?? "default") + ", medias=" + clientObject.Medias.Count +
                ", button=" + OctopusSampleLocalizedLabel.Describe(clientObject.ViewObjectButton) + ", tokenProvider=set");
            sdk.FetchOrCreateClientObjectRelatedPost(clientObject, postId =>
            {
                try
                {
                    OctopusScenarioSdk.SetBridgePost(clientObject.ObjectId, postId);
                    Finish("Bridge post ready. objectId=" + clientObject.ObjectId + ", postId=" + postId);
                }
                catch (Exception error) { Finish("Post ready, observation failed: " + error.Message); }
            }, error => Finish("Bridge typed error: " + error.Code +
                (error.LocalizedTextErrorKind.HasValue ? " " + error.LocalizedTextErrorKind.Value +
                    (error.LanguageCode == null ? "" : " (" + error.LanguageCode + ")") : "") +
                " — " + error.Message));
        }
        catch (Exception error) { Finish("Bridge failed: " + error.Message); }
    }

    private void Finish(string outcome) { _outcome = outcome; Report(Summary()); }
    private void Show() { if (IsRunning) ReportRunning(Summary()); else Report(Summary()); }
    private string Summary()
    {
        var post = OctopusScenarioSdk.LatestBridgePost;
        var line = _outcome + "\nLatest postId: " + (OctopusScenarioSdk.LatestBridgePostId ?? "—");
        if (post == null) line += "\nPost counts: awaiting snapshot";
        else
        {
            var reactions = 0;
            foreach (var count in post.Reactions) reactions += count.Count;
            line += "\ncomments=" + post.CommentCount + ", reactions=" + reactions +
                    ", userReaction=" + (post.UserReactionKind.HasValue ? post.UserReactionKind.Value.ToString() : "none");
        }
        return line + "\n" + NavigationSummary();
    }

    private static string NavigationSummary()
    {
        return "Navigate-to-client-object fires: " + OctopusScenarioSdk.NavigateFireCount +
               "; Last navigate objectId: " + (OctopusScenarioSdk.LastNavigateObjectId ?? "—");
    }
}
