using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The former Home read-out, now a group of read-only cards in Settings. It shows sample
/// observations without SDK calls. Status semantics stay unchanged until the Settings phase.
/// Existing information/disclosure QA names remain stable after the move.
/// </summary>
public class OctopusSampleHomeView : MonoBehaviour
{
    /// <summary>The platform chip of TOKENS §7: plain text, the slot hue, never a logo.</summary>
    public const string PlatformChipId = "home-platform-chip";

    /// <summary>The initialisation card.</summary>
    public const string SdkStatusCardId = "home-sdk-status-card";

    /// <summary>Its initialized / not initialized marker.</summary>
    public const string SdkStatusId = "home-sdk-status";

    /// <summary>The framed "Current configuration" block.</summary>
    public const string ConfigurationBlockId = "home-configuration-block";

    /// <summary>The connection card.</summary>
    public const string ConnectionCardId = "home-connection-card";

    /// <summary>Its CONNECT OK / DISCONNECT OK / CALL FAILED / NO CALL marker.</summary>
    public const string ConnectionStatusId = "home-connection-status";

    /// <summary>The connection card's link to the Connection scenario, where a call is made.</summary>
    public const string ConnectionScenarioLinkId = "home-connection-scenario-link";

    /// <summary>The catalogue id the link opens.</summary>
    public const string ConnectionScenarioId = "connection";

    /// <summary>Community access and the unseen-notification count — the two live SDK values.</summary>
    public const string CommunityAccessCardId = "home-community-access-card";

    private TMP_Text _sdkTitle;
    private TMP_Text _configurationSummary;
    private TMP_Text _sdkDetail;
    private RectTransform _sdkDotHost;
    private TMP_Text _connectionDetail;
    private RectTransform _connectionDotHost;
    private TMP_Text _accessValue;
    private TMP_Text _unseenValue;
    private readonly List<TMP_Text> _configValues = new List<TMP_Text>();
    private RectTransform _cards;

    private bool _listening;

    public static OctopusSampleHomeView BuildInto(RectTransform parent)
    {
        OctopusSampleState.EnsureObserving();
        var host = SampleUi.Panel("SampleOverview", parent, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(host, OctopusSampleBranding.Dp(OctopusSampleBranding.GroupGap), new RectOffset(), false);
        var view = host.gameObject.AddComponent<OctopusSampleHomeView>();
        view._cards = host;
        SampleUi.FlexibleLabel(host, OctopusSampleBranding.PlatformLabel, SampleUi.TextBody,
            OctopusSampleBranding.Palette.PlatformSlot).gameObject.name = PlatformChipId;
        view.BuildSdkStatusCard(host);
        view.BuildConfigurationBlock(host);
        view.BuildConnectionCard(host);
        view.BuildCommunityAccessCard(host);
        view.Listen();
        view.Refresh();
        return view;
    }

    // Explicit for retained tabs: EditMode does not send the MonoBehaviour activation callbacks.
    public void SetTabActive(bool active)
    {
        if (!active) { StopListening(); return; }
        Listen();
        Refresh();
    }

    private void OnEnable() { Listen(); }

    private void OnDisable() { StopListening(); }

    private void OnDestroy()
    {
        // Edit mode delivers no OnDisable either, and a handler left on a static event would fire
        // on a destroyed view in the next test.
        StopListening();
    }

    // One subscription, not three: the two values the SDK publishes are mirrored by
    // OctopusSampleState, which subscribes once per process and therefore keeps them across the
    // rebuilds this view goes through on every tab switch and theme change.
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

    private void OnStateChanged()
    {
        if (Gone()) return;
        Refresh();
    }

    // A destroyed view whose handler is still on a static event: unhook it from inside the
    // callback, the one place that still runs. The shell needs the same guard, for the same reason.
    private bool Gone()
    {
        if (this != null) return false;
        StopListening();
        return true;
    }

    /// <summary>
    /// Re-reads every value and repaints the labels. Deliberately not a rebuild: the blocks are
    /// fixed, and rebuilding on every SDK event would drop the reader's scroll position.
    /// </summary>
    public void Refresh()
    {
        var palette = OctopusSampleBranding.Palette;
        var initialized = OctopusSampleState.IsInitialized;

        _sdkTitle.text = "SDK status";
        _sdkDetail.text = initialized
            ? "Session mode: " + OctopusSampleState.ModeLabel
            : "No scenario has initialized the SDK in this session yet.";
        Repaint(_sdkDotHost, SdkStatusId, initialized ? palette.Positive : palette.Muted,
                SdkStatusLabel());

        _configurationSummary.text = OctopusScenarioSdk.Current.Profile == null
            ? "No configuration loaded" : "Configuration loaded";
        var lines = ConfigurationLines();
        for (var i = 0; i < _configValues.Count && i < lines.Count; i++)
        {
            _configValues[i].text = lines[i].Value;
        }

        // A returned call is neutral: it does not confirm an authenticated session.
        // Only a reported failure uses the error role.
        var session = OctopusSampleState.ConnectionSession;
        Color connectionColor;
        if (session == OctopusSampleState.Session.Failed ||
            session == OctopusSampleState.Session.StartupFailed) connectionColor = palette.Negative;
        else connectionColor = palette.Muted;

        _connectionDetail.text = ConnectionDetail();
        Repaint(_connectionDotHost, ConnectionStatusId, connectionColor, ConnectionStatusLabel());

        var unseen = OctopusSampleState.UnseenNotifications;
        _accessValue.text = OctopusSampleState.HasCommunityAccess ? "Granted" : "Not granted";
        _unseenValue.text = unseen < 0 ? "—" : unseen.ToString();
    }

    /// <summary>
    /// READY after initialization; a neutral Not initialized before that.
    ///
    /// Public and static so an EditMode test can assert the mapping: the test assembly overrides its
    /// references down to NUnit and cannot name a `TMP_Text` to read the label off the
    /// screen. <see cref="Refresh"/> paints this exact string, so asserting it here is asserting
    /// what the dot says.
    /// </summary>
    public static string SdkStatusLabel()
    {
        return OctopusSampleState.IsInitialized ? "READY" : "Not initialized";
    }

    /// <summary>
    /// What the last connection *call* did — never what state the session is in.
    ///
    /// The distinction is the whole card. Android reads the SDK's own `connectionState` and can say
    /// CONNECTED; nothing of the kind reaches this side, so anything this sample writes about a
    /// session is an inference from its own call returning. Two ways that inference is wrong, both
    /// reachable today: the iOS bridge catches an authentication failure and still reports the
    /// connect as complete, so a green CONNECTED can sit over a session that was refused; and a
    /// *disconnect* that throws leaves the previous session very much alive, so labelling it OFF
    /// tells the reader the opposite of what happened.
    ///
    /// So the marker names the operation and its outcome — CONNECT OK / DISCONNECT OK / CALL FAILED
    /// / NO CALL — which is exactly what the sample observed, and the detail line below says the
    /// session itself is not observable from here. A reader debugging "why can't I post" is then
    /// looking at a fact rather than at a guess.
    /// </summary>
    public static string ConnectionStatusLabel()
    {
        switch (OctopusSampleState.ConnectionSession)
        {
            case OctopusSampleState.Session.ConnectCompleted: return "CONNECT OK";
            case OctopusSampleState.Session.Disconnected: return "DISCONNECT OK";
            case OctopusSampleState.Session.Failed: return "CALL FAILED";
            case OctopusSampleState.Session.StartupFailed: return "STARTUP FAILED";
            default: return "NO CALL";
        }
    }

    internal static string ConnectionDetail()
    {
        var session = OctopusSampleState.ConnectionSession;
        var detail = OctopusSampleState.SessionDetail;

        if (session == OctopusSampleState.Session.None || string.IsNullOrEmpty(detail))
        {
            return "No connection call yet. The Connection scenario, linked below, makes one.";
        }

        if (session == OctopusSampleState.Session.StartupFailed)
        {
            // Not a connection call at all: the saved profile could not be replayed at launch.
            return detail + " — no connection call was made. Choose a profile in Configuration " +
                   "to start the SDK again.";
        }

        if (session == OctopusSampleState.Session.Failed)
        {
            // Emphatically not "the call returned": it threw. And a disconnect that throws leaves
            // whatever session existed before it untouched, which is the case a reader is most
            // likely to misread.
            return detail + " — the call did not complete, so it changed nothing the sample can " +
                   "vouch for.";
        }

        return detail + " — that is the call completing, not a session: this package publishes no " +
               "connection state, so whether the SDK accepted it cannot be read back here.";
    }

    /// <summary>
    /// The lines of the framed configuration block, in the Android sample's order. Public so the
    /// EditMode tests can assert on the values without naming a uGUI type.
    /// </summary>
    public static List<KeyValuePair<string, string>> ConfigurationLines()
    {
        var profile = OctopusScenarioSdk.Current.Profile;
        var missing = profile == null;
        var lines = new List<KeyValuePair<string, string>>();

        // The pilots always pass a host (the demo backend unless the asset overrides it): the one
        // the running SDK was initialised or last switched onto, else the one Initialize will get.
        lines.Add(Line("Server environment", OctopusScenarioSdk.RunningServerHost ??
            OctopusScenarioSdk.ServerHostFor(missing ? null : profile)));
        // Never the key itself, and never a client name: the community is identified by the key,
        // which is a secret the mirror-export guard scans for. Its source is what a reader can act
        // on.
        lines.Add(Line("Community", missing
            ? "Unknown — no API key configured"
            : "The community behind the configured API key"));
        lines.Add(Line("API key source", missing
            ? "Missing — create it via Assets > Create > Octopus Example Config"
            : "OctopusExampleConfig (git-ignored asset)"));
        lines.Add(Line("SSO user", missing ? "—" : Describe(profile)));
        lines.Add(Line("Entitlements", "Carried in the SSO token; presets set claims when demo signing is configured"));
        // "Sample theme": the sample's own appearance (Settings › Appearance), not the SDK theme
        // the Theme scenario applies to the community screens.
        lines.Add(Line("Sample theme", OctopusSampleBranding.Theme.ToString()));
        lines.Add(Line("Language", string.IsNullOrEmpty(OctopusSampleState.LocaleOverride)
            ? "System default (no override)"
            : OctopusSampleState.LocaleOverride));
        return lines;
    }

    private static string Describe(OctopusExampleConfig.ExampleProfile profile)
    {
        if (string.IsNullOrEmpty(profile.userId)) return "Configured, no user id";
        return string.IsNullOrEmpty(profile.nickname)
            ? profile.userId
            : profile.nickname + " · " + profile.userId;
    }

    private static KeyValuePair<string, string> Line(string label, string value)
    {
        return new KeyValuePair<string, string>(label, value);
    }

    private void BuildSdkStatusCard(RectTransform parent)
    {
        var card = SampleUi.Card(SdkStatusCardId, parent);
        var header = HeaderRow(card);
        _sdkTitle = SampleUi.FlexibleLabel(header, "SDK status", SampleUi.TextTitle,
            SampleUi.TitleColor);
        _sdkDotHost = DotHost(header, 0f);
        SampleUi.FlexibleLabel(card, "SDK " + OctopusSDK.Version, SampleUi.TextBody, SampleUi.TitleColor)
            .gameObject.name = "home-sdk-version";
        var details = SampleUi.Panel("Details", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(details, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm),
            new RectOffset(), false);
        SampleUi.FlexibleLabel(details, "Native build pins · " + OctopusSampleNativePins.Current(),
            SampleUi.TextCaption, SampleUi.Muted).gameObject.name = "home-sdk-native-pins";
        _sdkDetail = SampleUi.FlexibleLabel(details, string.Empty, SampleUi.TextBody, SampleUi.Muted);
        BuildDisclosure(card, "home-sdk-status-details", details);
    }

    private void BuildConfigurationBlock(RectTransform parent)
    {
        // The strong hairline in dark, so the block stands out from the cards around it.
        var card = SampleUi.Card(ConfigurationBlockId, parent, OctopusSampleBranding.Palette.BorderStrong);
        SampleUi.FlexibleLabel(card, "Current configuration", SampleUi.TextTitle, SampleUi.TitleColor);
        _configurationSummary = SampleUi.FlexibleLabel(card, string.Empty,
            SampleUi.TextBody, SampleUi.TitleColor);
        var details = SampleUi.Panel("Details", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(details, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd), new RectOffset(), false);
        foreach (var line in ConfigurationLines())
        {
            // Stacked labels keep long technical values readable at narrow widths.
            var row = SampleUi.Panel(line.Key, details, OctopusSampleBranding.Clear);
            SampleUi.VerticalStack(row, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs), new RectOffset(), false);
            SampleUi.FlexibleLabel(row, line.Key, SampleUi.TextCaption, SampleUi.Muted);
            var value = SampleUi.FlexibleLabel(row, line.Value, SampleUi.TextBody, SampleUi.TitleColor);
            value.richText = false;
            _configValues.Add(value);
        }
        BuildDisclosure(card, "home-configuration-details", details);
    }

    private void BuildConnectionCard(RectTransform parent)
    {
        var card = SampleUi.Card(ConnectionCardId, parent);
        var header = HeaderRow(card);
        SampleUi.FlexibleLabel(header, "Last connection call", SampleUi.TextTitle, SampleUi.TitleColor);
        _connectionDotHost = DotHost(header, 0f);
        SampleUi.FlexibleLabel(card, "The call result does not confirm a connected session.",
            SampleUi.TextBody, SampleUi.Muted);
        var details = SampleUi.Panel("Details", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(details, 0f, new RectOffset(), false);
        _connectionDetail = SampleUi.FlexibleLabel(details, string.Empty, SampleUi.TextBody, SampleUi.Muted);
        _connectionDetail.richText = false;
        BuildDisclosure(card, "home-connection-details", details);
        SampleUi.Button(ConnectionScenarioLinkId, card, "Open Connection scenario",
            SampleUiButtonVariant.Tertiary, OpenConnectionScenario);
    }

    /// <summary>
    /// Opens the Connection scenario over the Scenarios tab. Wired to
    /// <see cref="ConnectionScenarioLinkId"/>; a no-op outside the shell.
    /// </summary>
    public void OpenConnectionScenario()
    {
        var shell = GetComponentInParent<OctopusSampleShell>();
        if (shell != null) shell.OpenScenarioScreen(ConnectionScenarioId);
    }

    private static void BuildDisclosure(RectTransform card, string id, RectTransform details)
    {
        details.gameObject.SetActive(false);
        RectTransform toggle = null;
        toggle = SampleUi.Button(id, card, "Show details",
            SampleUiButtonVariant.Tertiary, () =>
            {
                var open = !details.gameObject.activeSelf;
                details.gameObject.SetActive(open);
                toggle.GetComponentInChildren<TMP_Text>().text = open ? "Hide details" : "Show details";
            });
        toggle.SetSiblingIndex(details.GetSiblingIndex());
    }

    private void BuildCommunityAccessCard(RectTransform parent)
    {
        var card = SampleUi.Card(CommunityAccessCardId, parent);
        SampleUi.FlexibleLabel(card, "Community access", SampleUi.TextTitle, SampleUi.TitleColor);
        // The only two values on this screen that come from the SDK rather than from the sample.
        _accessValue = ValueRow(card, "Access");
        var details = SampleUi.Panel("Details", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(details, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm),
            new RectOffset(), false);
        _unseenValue = ValueRow(details, "Unseen notifications");
        SampleUi.FlexibleLabel(details,
                               "Access and notification counts update when the SDK reports them.",
                               SampleUi.TextCaption, SampleUi.Muted);
        BuildDisclosure(card, "home-community-access-details", details);
    }

    private static TMP_Text ValueRow(RectTransform card, string label)
    {
        var row = SampleUi.Panel(label, card, Color.clear);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
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
    /// The slot a status marker is rebuilt into. It carries its own minimum height: the marker is
    /// stretched to fill this host, and a host inside a height-controlling layout group reports the
    /// preferred height of *its* layout element, not of its children — leaving it unset collapses
    /// the row to nothing and the marker with it.
    /// </summary>
    private static RectTransform DotHost(RectTransform header, float width)
    {
        var host = SampleUi.Panel("Status", header, Color.clear);
        var element = host.gameObject.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.minHeight = OctopusSampleBranding.Dp(18f);
        return host;
    }

    private static RectTransform HeaderRow(RectTransform card)
    {
        var row = SampleUi.Panel("Header", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(row, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm), new RectOffset(), false);
        return row;
    }

    // The dot is rebuilt rather than recoloured: its marker and its label are two graphics that
    // must never disagree, and one call that replaces both cannot leave them half-updated.
    private static void Repaint(RectTransform host, string id, Color color, string label)
    {
        for (var i = host.childCount - 1; i >= 0; i--)
        {
            var child = host.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        var dot = SampleUi.StatusDot(id, host, color, label);
        SampleUi.Stretch(dot, Vector2.zero, Vector2.one);
        dot.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
    }
}
