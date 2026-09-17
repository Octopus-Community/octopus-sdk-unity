using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Four explicit launchers into the native SDK UI. Building the tab makes no SDK call.
/// A single band reports the strongest known condition; a completed connection call alone
/// is not evidence of authentication. The SDK exposes no presentation completion callback.
/// </summary>
public class OctopusSampleCommunityView : MonoBehaviour
{
    /// <summary>The main door: the community's own feed.</summary>
    public const string OpenId = "community-open-button";

    /// <summary>The group id the group door will use.</summary>
    public const string GroupFieldId = "community-group-field";

    /// <summary>Opens the community on one group's feed.</summary>
    public const string OpenGroupId = "community-open-group-button";

    /// <summary>The post id the post door will use.</summary>
    public const string PostFieldId = "community-post-field";

    /// <summary>Opens the community on one post's detail screen.</summary>
    public const string OpenPostId = "community-open-post-button";

    /// <summary>The body the post editor opens prefilled with, empty for a blank editor.</summary>
    public const string PrefillFieldId = "community-prefill-field";

    /// <summary>Opens the community's post editor.</summary>
    public const string CreatePostId = "community-create-post-button";

    /// <summary>
    /// The band shown when no door can work at all: the gitignored config asset is missing, so
    /// there is no API key to initialise with. Named after Flutter's `community-band-blocked`
    /// rather than Android's `community-band-unreachable`, because nothing here probes the
    /// network — this package has no cheap reachability call the tab could make without first
    /// initialising, which is precisely what arriving on a tab must not do.
    /// </summary>
    public const string BlockedBandId = "community-band-blocked";

    /// <summary>
    /// The band shown when no connect call has completed and access has not been reported. Android and Flutter both call it `community-band-readonly`.
    /// </summary>
    public const string ReadOnlyBandId = "community-band-readonly";

    /// <summary>The card mirroring what the SDK has published about the community.</summary>
    public const string StateCardId = "community-state-card";

    /// <summary>The one line saying what the last tap did, or why it did nothing.</summary>
    public const string ResultId = "community-result";

    public const string GrantedBandId = "community-band-granted";
    public const string DestinationToggleId = "community-destination-toggle";
    public const string ComposerToggleId = "community-composer-toggle";

    private static float DockHeight { get { return OctopusSampleBranding.Dp(80f); } }
    private GameObject _grantedBand;
    private RectTransform _destinations;
    private RectTransform _composer;
    private bool _nativeRequested;
    private bool _leftPlayer;

    private GameObject _blockedBand;
    private GameObject _readOnlyBand;
    private TMP_Text _result;
    private TMP_Text _initValue;
    private TMP_Text _sessionValue;
    private TMP_Text _accessValue;
    private TMP_Text _unseenValue;
    private TMP_InputField _group;
    private TMP_InputField _post;
    private TMP_InputField _prefill;

    private bool _listening;

    /// <summary>Builds the tab into the shell's content area and returns it.</summary>
    public static OctopusSampleCommunityView BuildInto(RectTransform content)
    {
        // Same reason as the dashboard: the two values the SDK pushes are lost if nothing is
        // mirroring them by the time it pushes, and this tab reports both.
        OctopusSampleState.EnsureObserving();

        var host = SampleUi.Panel("CommunityTab", content, OctopusSampleBranding.Clear);
        SampleUi.Stretch(host, Vector2.zero, Vector2.one);
        var view = host.gameObject.AddComponent<OctopusSampleCommunityView>();
        view.Build(host);
        return view;
    }

    /// <summary>
    /// Opens the community's main feed, initialising the SDK first if no scenario has yet.
    /// Public because the tests drive it, and because the docked button is the same call.
    /// </summary>
    public void OpenCommunity()
    {
        RequestNative("OctopusSDK.Open", null, () => OctopusScenarioSdk.Current.Open(),
            "Community opening requested. Close the native screen to return here.");
    }

    /// <summary>Opens the community on the group named in the group field.</summary>
    public void OpenGroup()
    {
        var groupId = Value(_group);
        if (groupId.Length == 0)
        {
            SampleUi.SetFieldError(_group, "Enter a group id.");
            Report("Enter a group id first.");
            return;
        }

        SampleUi.SetFieldError(_group, null);
        RequestNative("OctopusSDK.OpenGroup", "groupId=" + groupId,
            () => OctopusScenarioSdk.Current.OpenGroup(groupId),
            "Group opening requested: " + groupId + ". Close the native screen to return here.");
    }

    /// <summary>Opens the community on the post named in the post field.</summary>
    public void OpenPost()
    {
        var postId = Value(_post);
        if (postId.Length == 0)
        {
            SampleUi.SetFieldError(_post, "Enter a post id.");
            Report("Enter a post id first.");
            return;
        }

        SampleUi.SetFieldError(_post, null);
        RequestNative("OctopusSDK.OpenPost", "postId=" + postId,
            () => OctopusScenarioSdk.Current.OpenPost(postId),
            "Post opening requested: " + postId + ". Close the native screen to return here.");
    }

    /// <summary>
    /// Opens the community's post editor, prefilled with the body in the prefill field when there
    /// is one. Nothing is published: the editor opens, and the user decides.
    /// </summary>
    public void OpenCreatePost()
    {
        var text = Value(_prefill);
        var prefilled = text.Length == 0 ? null : new OctopusPrefilledPost { Text = text };
        RequestNative("OctopusSDK.OpenCreatePost", prefilled == null ? "blank" : "prefilled",
            () => OctopusScenarioSdk.Current.OpenCreatePost(prefilled),
            "Post composer opening requested. Nothing is published until you choose to send.");
    }

    private void RequestNative(string method, string detail, System.Action open, string message)
    {
        try
        {
            string reason;
            if (!Ready(out reason)) { Refresh(); Report(reason); return; }
            OctopusSampleLog.Current.LogApiCall(method, detail);
            _nativeRequested = true;
            _leftPlayer = false;
            Report(message);
            open();
        }
        catch (System.Exception exception)
        {
            OctopusSampleLog.Current.LogStateChange("Native screen request failed", exception.Message);
            _nativeRequested = false;
            Report("Could not request the native screen. Check your configuration and try again.");
        }
    }

    /// <summary>
    /// Handles the player's pause/resume notification. Public so EditMode tests can call the
    /// same handler directly without SendMessage dispatching a non-running Unity behaviour.
    /// </summary>
    public void OnApplicationPause(bool paused)
    {
        if (!_nativeRequested) return;
        if (paused) _leftPlayer = true;
        else if (_leftPlayer)
        {
            _nativeRequested = false;
            _leftPlayer = false;
            Report("Returned to the sample. You can open another destination.");
            Refresh();
        }
    }

    /// <summary>
    /// The visible band id, or an empty string when no condition is known. Inactive bands keep their ids.
    /// </summary>
    public string VisibleBand
    {
        get
        {
            if (_blockedBand != null && _blockedBand.activeSelf) return BlockedBandId;
            if (_readOnlyBand != null && _readOnlyBand.activeSelf) return ReadOnlyBandId;
            if (_grantedBand != null && _grantedBand.activeSelf) return GrantedBandId;
            return string.Empty;
        }
    }

    /// <summary>The line the last action left on screen, or the initial idle message.</summary>
    public string Result { get { return _result == null ? string.Empty : _result.text; } }

    /// <summary>
    /// What the state card says right now, as one line — the four values it shows, in order.
    /// Read off the labels themselves rather than recomputed, so a card that stopped refreshing
    /// cannot report the fresh values here.
    /// </summary>
    public string StateSummary
    {
        get
        {
            return Read(_initValue) + " | " + Read(_sessionValue) + " | " + Read(_accessValue) +
                   " | " + Read(_unseenValue);
        }
    }

    /// <summary>
    /// Writes the group field, as typing into it would.
    ///
    /// It exists because the EditMode assembly cannot name an `InputField` — it overrides its
    /// references down to nunit — so this is how a test puts a value where the door reads it. It
    /// writes the real field rather than a shadow copy, so the read path under test is the one the
    /// button uses; what it cannot cover is the on-screen typing itself, which the on-device pass
    /// does.
    /// </summary>
    public void SetGroupId(string groupId) { Fill(_group, groupId); }

    /// <summary>Writes the post field. Same reason as <see cref="SetGroupId"/>.</summary>
    public void SetPostId(string postId) { Fill(_post, postId); }

    /// <summary>Writes the prefilled-body field. Same reason as <see cref="SetGroupId"/>.</summary>
    public void SetPrefillText(string text) { Fill(_prefill, text); }

    private void Build(RectTransform host)
    {
        BuildDock(host);

        var scrollHost = SampleUi.Panel("Scroll", host, OctopusSampleBranding.Clear);
        SampleUi.Stretch(scrollHost, Vector2.zero, Vector2.one);
        scrollHost.offsetMin = new Vector2(0f, DockHeight);
        var cards = SampleUi.VerticalScroll(scrollHost, SampleUi.ContentPadding());
        cards.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(16f);

        BuildBands(cards);
        BuildPlatformCard(cards);
        BuildStateCard(cards);
        BuildDeepLinkCard(cards);
        BuildCreatePostCard(cards);
        BuildResultCard(cards);

        Listen();
        Refresh();
    }

    private void BuildDock(RectTransform host)
    {
        // Docked rather than in the scroll, for the same reason the dashboard docks its own: the
        // one action a reader came to this tab for should not need a scroll to be found.
        var dock = SampleUi.Panel("Dock", host, SampleUi.RowBackground);
        dock.anchorMin = Vector2.zero;
        dock.anchorMax = new Vector2(1f, 0f);
        dock.pivot = new Vector2(0.5f, 0f);
        dock.sizeDelta = new Vector2(0f, DockHeight);
        dock.anchoredPosition = Vector2.zero;
        SampleUi.VerticalStack(dock, 0f, new RectOffset(48, 48, 36, 36), false);

        SampleUi.Button(OpenId, dock, "Open community", OpenCommunity);
    }

    /// <summary>
    /// The state bands, built once and shown by <see cref="Refresh"/>. At most one is visible
    /// at a time and the blocking one wins, exactly as on Android, Flutter and React Native: a
    /// reader who cannot open anything should not also be told they will be read-only.
    /// </summary>
    private void BuildBands(RectTransform parent)
    {
        _blockedBand = BuildBand(parent, BlockedBandId, SampleUi.Attention,
            "Configuration unavailable. Set up OctopusExampleConfig in the Editor to open the community.",
            null, null);
        _readOnlyBand = BuildBand(parent, ReadOnlyBandId, SampleUi.TitleColor,
            "No connect call completed. Try Connection in Scenarios for authenticated access.",
            "Scenarios", GoToScenarios);
        _grantedBand = BuildBand(parent, GrantedBandId, SampleUi.TitleColor,
            "Community access granted — reported by the SDK.", null, null);
    }

    private static GameObject BuildBand(RectTransform parent, string id, Color tone, string message,
                                        string actionLabel, UnityEngine.Events.UnityAction action)
    {
        var band = SampleUi.Panel(id, parent, id != BlockedBandId ? SampleUi.RowBackground
            : OctopusSampleBranding.Palette.WarningSurface, OctopusSampleBranding.FieldRadius,
            OctopusSampleBranding.Clear, 0f);
        SampleUi.VerticalStack(band, OctopusSampleBranding.Dp(8f),
            new RectOffset(48, 48, 36, 36), false);
        SampleUi.FlexibleLabel(band, message, SampleUi.TextCaption, tone);
        if (action != null)
            SampleUi.Button(id + "-action", band, actionLabel, SampleUiButtonVariant.Tertiary, action);
        return band.gameObject;
    }

    public void GoToScenarios()
    {
        var shell = GetComponentInParent<OctopusSampleShell>();
        if (shell != null) shell.Select(OctopusSampleTab.Scenarios);
    }

    private static void BuildPlatformCard(RectTransform parent)
    {
        var card = SampleUi.Card("community-platform-card", parent);
        SampleUi.FlexibleLabel(card, "Your community, one tap away.", SampleUi.TextTitleXl,
            SampleUi.TitleColor);
        SampleUi.FlexibleLabel(card, "The community opens in a native screen. Close it to return to this sample.",
            SampleUi.TextBody, SampleUi.Muted);
    }

    private void BuildStateCard(RectTransform parent)
    {
        var card = SampleUi.Card(StateCardId, parent);
        SampleUi.FlexibleLabel(card, "Before you open", SampleUi.TextTitle, SampleUi.TitleColor);
        _initValue = ValueRow(card, "SDK");
        _sessionValue = ValueRow(card, "Last connection call");
        _accessValue = ValueRow(card, "Access");
        _unseenValue = ValueRow(card, "Unseen notifications");
        SampleUi.FlexibleLabel(card, "A completed call does not confirm authentication.",
            SampleUi.TextCaption, SampleUi.Muted);
    }

    private void BuildDeepLinkCard(RectTransform parent)
    {
        var card = SampleUi.Card("community-deeplink-card", parent);
        SampleUi.ListRow(DestinationToggleId, card, "Open a specific destination", null, ToggleDestinations);
        _destinations = SampleUi.Panel("Destinations", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(_destinations, OctopusSampleBranding.Dp(8f), new RectOffset(), false);
        _group = SampleUi.LabeledField(GroupFieldId, _destinations, "Group id", string.Empty);
        SampleUi.Button(OpenGroupId, _destinations, "Open group", SampleUiButtonVariant.Secondary, OpenGroup);
        _post = SampleUi.LabeledField(PostFieldId, _destinations, "Post id", string.Empty);
        SampleUi.Button(OpenPostId, _destinations, "Open post", SampleUiButtonVariant.Secondary, OpenPost);
        SampleUi.FlexibleLabel(_destinations, "Use IDs from your demo community.", SampleUi.TextCaption, SampleUi.Muted);
        _destinations.gameObject.SetActive(false);
    }

    public void ToggleDestinations()
    {
        if (_destinations != null) _destinations.gameObject.SetActive(!_destinations.gameObject.activeSelf);
    }

    private void BuildCreatePostCard(RectTransform parent)
    {
        var card = SampleUi.Card("community-create-post-card", parent);
        SampleUi.ListRow(ComposerToggleId, card, "Create a post", null, ToggleComposer);
        _composer = SampleUi.Panel("Composer", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(_composer, OctopusSampleBranding.Dp(8f), new RectOffset(), false);
        _prefill = SampleUi.LabeledField(PrefillFieldId, _composer, "Prefilled body (optional)", string.Empty, true);
        SampleUi.Button(CreatePostId, _composer, "Open post composer", SampleUiButtonVariant.Secondary, OpenCreatePost);
        SampleUi.FlexibleLabel(_composer, "Opens the composer. Publishing is a separate action.",
            SampleUi.TextCaption, SampleUi.Muted);
        _composer.gameObject.SetActive(false);
    }

    public void ToggleComposer()
    {
        if (_composer != null) _composer.gameObject.SetActive(!_composer.gameObject.activeSelf);
    }

    private void BuildResultCard(RectTransform parent)
    {
        var card = SampleUi.Card("community-result-card", parent);
        _result = SampleUi.FlexibleLabel(card, "No destination opened yet.", SampleUi.TextCaption, SampleUi.Muted);
        _result.richText = false;
        _result.gameObject.name = ResultId;
    }

    private static TMP_Text ValueRow(RectTransform card, string label)
    {
        var row = SampleUi.Panel(label, card, OctopusSampleBranding.Clear);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = OctopusSampleBranding.Dp(16f);
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        row.gameObject.AddComponent<LayoutElement>();

        SampleUi.FlexibleLabel(row, label, SampleUi.TextCaption, SampleUi.Muted);
        var value = SampleUi.FlexibleLabel(row, "—", SampleUi.TextCaption, SampleUi.TitleColor);
        value.alignment = TextAlignmentOptions.TopRight;
        return value;
    }

    /// <summary>
    /// Initialises the SDK if nothing has yet, and reports why a door cannot open when the
    /// gitignored config asset is missing — the one state where there is no API key to pass.
    /// </summary>
    private static bool Ready(out string reason)
    {
        string reported;
        var profile = OctopusScenarioSdk.EnsureInitialized(OctopusScenarioSdk.PilotMode(),
                                                           OctopusScenarioSdk.PilotModeLabel,
                                                           out reported);
        if (profile == null)
        {
            reason = reported;
            return false;
        }

        reason = null;
        return true;
    }

    private static string Value(TMP_InputField field)
    {
        return field == null || field.text == null ? string.Empty : field.text.Trim();
    }

    private static string Read(TMP_Text label)
    {
        return label == null || label.text == null ? string.Empty : label.text;
    }

    private static void Fill(TMP_InputField field, string value)
    {
        if (field != null) field.text = value ?? string.Empty;
    }

    private void Report(string message)
    {
        if (_result != null) _result.text = message;
    }

    private void OnEnable() { Listen(); }

    private void OnDisable() { StopListening(); }

    private void OnDestroy() { StopListening(); }

    // A destroyed view whose handler is still on the static event: unhook it from inside the
    // callback, the one place that still runs. Edit mode delivers neither OnDisable nor OnDestroy to
    // a plain MonoBehaviour, so this is not belt-and-braces there — it is the only path. Home and
    // the shell carry the same guard, for the same reason.
    private bool Gone()
    {
        if (this != null) return false;
        StopListening();
        return true;
    }

    // Subscribed from the build path as well, because edit mode never calls OnEnable and a test
    // that changes the state after building would watch a tab that never hears about it.
    private void Listen()
    {
        if (_listening) return;
        OctopusSampleState.Changed += OnStateChanged;
        _listening = true;
    }

    private void StopListening()
    {
        if (!_listening) return;
        OctopusSampleState.Changed -= OnStateChanged;
        _listening = false;
    }

    /// <summary>
    /// How many times the static state event has reached this view. Test-only observable, and the
    /// only one there is: a handler left behind on a destroyed view returns quietly from
    /// <see cref="OnStateChanged"/>, so without a count a test cannot tell "unsubscribed" from
    /// "still subscribed and silent" — and it is the leak, not the noise, that the app pays for.
    /// </summary>
    public int StateEventCount { get; private set; }

    private void OnStateChanged()
    {
        StateEventCount++;
        if (Gone()) return;
        Refresh();
    }

    private void Refresh()
    {
        // The same guard the doors use, so the band cannot say a door will work when it will not:
        // reading it initialises nothing, it just opens the config asset and checks the key.
        string refusal;
        var blocked = OctopusScenarioSdk.UsableProfile(out refusal) == null;
        if (_blockedBand != null) _blockedBand.SetActive(blocked);
        if (_readOnlyBand != null)
        {
            _readOnlyBand.SetActive(
                !blocked && !OctopusSampleState.HasCommunityAccess &&
                OctopusSampleState.ConnectionSession != OctopusSampleState.Session.ConnectCompleted);
        }

        if (_grantedBand != null) _grantedBand.SetActive(!blocked && OctopusSampleState.HasCommunityAccess);

        if (_initValue != null)
        {
            _initValue.text = OctopusSampleState.IsInitialized
                ? "initialised (" + OctopusSampleState.ModeLabel + ")"
                : "not initialised yet";
        }

        // Home's vocabulary, called rather than copied: two screens naming the same fact differently
        // is how a reader ends up believing they disagree.
        if (_sessionValue != null)
        {
            _sessionValue.text = OctopusSampleHomeView.ConnectionStatusLabel();
        }

        if (_accessValue != null)
        {
            _accessValue.text = OctopusSampleState.HasCommunityAccess ? "granted" : "not granted";
        }

        if (_unseenValue != null)
        {
            // Negative is the "the SDK has not published a count yet" sentinel, and the sample never
            // asks for one. Rendered as Home renders it: a raw -1 here reads as a broken counter.
            var unseen = OctopusSampleState.UnseenNotifications;
            _unseenValue.text = unseen < 0 ? "—" : unseen.ToString();
        }
    }
}
