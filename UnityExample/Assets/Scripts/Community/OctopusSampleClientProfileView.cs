using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The host-rendered profile page the shared catalogue's `communityData` preset 6 navigates to.
///
/// The SDK exposes a member's public community data; a game shows it in its own UI rather than in an
/// Octopus screen. This page is that host UI for the sample: it is the Unity counterpart of the
/// Flutter example's `client_profile_page.dart` and the Android sample's `ClientProfileScreen`, and it
/// surfaces the same three destination ids the QA Tester asserts on, as the catalogue spells them —
/// `clientProfile-data`, `clientProfile-unknown`, `clientProfile-error` — plus `clientProfile-back`
/// on the app bar, the id both natives settled on for the way out.
///
/// It renders an <see cref="OctopusScenarioHostProfile"/> and nothing else: the scenario pilot
/// owns the fetch, this page only follows the destination's <c>Changed</c> event. Building it makes
/// no SDK call. Like every other sample screen it is built in code through <see cref="SampleUi"/>,
/// with no prefab and no scene YAML, so it cannot drift from the scenario screen it opens over.
/// </summary>
public class OctopusSampleClientProfileView : MonoBehaviour
{
    /// <summary>Above <see cref="OctopusScenarioScreenView"/>'s 1100, which stays open behind it.</summary>
    private const int SortingOrder = 1200;

    public const string BackTestId = "clientProfile-back";
    public const string LoadingTestId = "clientProfile-loading";

    private OctopusScenarioHostProfile _profile;
    private RectTransform _root, _topBleed, _bottomBleed;
    private bool _rebuildRequested;

    /// <summary>
    /// Opens the page for <paramref name="profile"/> over the current scene. A second call while
    /// one is open rebinds the open page to the new destination instead of stacking another.
    /// </summary>
    public static OctopusSampleClientProfileView Open(OctopusScenarioHostProfile profile)
    {
        var view = FindAnyObjectByType<OctopusSampleClientProfileView>();
        if (view == null)
        {
            var host = new GameObject("OctopusSampleClientProfileView");
            view = host.AddComponent<OctopusSampleClientProfileView>();
            SampleUi.OverlayCanvas(host, SortingOrder);
        }
        // Built here rather than from Start(): the page has to exist fully formed the moment Open
        // returns, both for the caller and for an EditMode test, where Start never runs.
        view.Bind(profile);
        return view;
    }

    /// <summary>The destination on screen, or null before <see cref="Bind"/>.</summary>
    public OctopusScenarioHostProfile Profile { get { return _profile; } }

    /// <summary>Attaches a destination and builds the page.</summary>
    public void Bind(OctopusScenarioHostProfile profile)
    {
        Unbind();
        OctopusSampleBranding.ThemeChanged += OnThemeChanged;
        _profile = profile;
        _profile.Changed += OnProfileChanged;
        Rebuild();
        OctopusSampleQaLaunch.Log("clientProfile state=opened");
    }

    /// <summary>Closes the page exactly as its own Back button does.</summary>
    public void Dismiss()
    {
        // Unbind here and not only in OnDestroy: OnDestroy never runs for a component that never
        // woke up (EditMode), and a destination completing after the page closed must not rebuild it.
        Unbind();
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    /// <summary>Drops both subscriptions; safe to call twice.</summary>
    private void Unbind()
    {
        if (_profile != null) _profile.Changed -= OnProfileChanged;
        _profile = null;
        OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
    }

    private void OnProfileChanged()
    {
        if (this == null) return;
        RequestRebuild();
    }

    private void OnThemeChanged()
    {
        // The theme event is static, so a page destroyed from outside (scene unload, a test's
        // teardown) can still hold this handler: drop it instead of rebuilding.
        if (this == null)
        {
            OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
            return;
        }
        RequestRebuild();
    }

    private void RequestRebuild()
    {
        if (Application.isPlaying) _rebuildRequested = true;
        else Rebuild();
    }

    private void LateUpdate()
    {
        SampleUi.ResizeBleed(_topBleed, true);
        SampleUi.ResizeBleed(_bottomBleed, false);
        if (!_rebuildRequested) return;
        _rebuildRequested = false;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_root != null)
        {
            _root.gameObject.SetActive(false);
            _root.SetParent(null, false);
            if (Application.isPlaying) Destroy(_root.gameObject);
            else DestroyImmediate(_root.gameObject);
        }
        Build();
        OctopusSampleQaLaunch.Log("clientProfile state=" + (_profile.IsLoading ? LoadingTestId : _profile.ResultTestId));
    }

    private void Build()
    {
        GetComponent<CanvasScaler>().referenceResolution = new Vector2(
            SampleUi.ReferenceWidthFor(SampleUi.ScreenDpWidth()), SampleUi.CanvasReference.y);

        _root = SampleUi.Panel("Root", transform, SampleUi.Background);
        SampleUi.Stretch(_root, Vector2.zero, Vector2.one);
        _topBleed = SampleUi.BuildBleed(_root, "TopBleed", OctopusSampleBranding.Palette.Chrome, true);
        _bottomBleed = SampleUi.BuildBleed(_root, "BottomBleed", OctopusSampleBranding.Palette.Surface, false);
        var root = SampleUi.SafeArea("clientProfile-safe-area", _root);

        SampleUi.AppBar("Header", root, "Client profile", Dismiss, BackTestId);

        var content = SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
        content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(16f);

        var context = SampleUi.Card("clientProfile-context", content);
        var heading = SampleUi.FlexibleLabel(context, "Host-rendered profile", SampleUi.TextTitle, SampleUi.TitleColor);
        heading.fontStyle = FontStyles.Bold;
        SampleUi.FlexibleLabel(context, "This page belongs to the game, not to the SDK: it shows the public " +
            "community data of one member, fetched through OctopusSDK.FetchCommunityData by clientUserId.",
            SampleUi.TextCaption, SampleUi.Muted);
        Row(context, "clientUserId", _profile.ClientUserId);

        BuildState(content);
    }

    private void BuildState(RectTransform content)
    {
        if (_profile.IsLoading)
        {
            var loading = SampleUi.Card(LoadingTestId, content);
            SampleUi.FlexibleLabel(loading, "Fetching community data…", SampleUi.TextBody, SampleUi.Muted);
            return;
        }

        // GameObject.name IS the destination id the catalogue's preset 6 asserts on, verbatim.
        var panel = SampleUi.Card(_profile.ResultTestId, content);
        if (_profile.Error != null)
        {
            var error = SampleUi.FlexibleLabel(panel, "Fetch failed: " + _profile.Error, SampleUi.TextBody, SampleUi.Attention);
            error.richText = false;
            return;
        }
        var data = _profile.Data;
        if (data == null)
        {
            SampleUi.FlexibleLabel(panel, "No Octopus profile for this clientUserId. Either the member has never " +
                "used the community, or the community is not configured to expose client user ids.",
                SampleUi.TextBody, SampleUi.TitleColor);
            return;
        }
        Row(panel, "profileId", data.ProfileId);
        Row(panel, "messageCount", data.MessageCount.HasValue ? data.MessageCount.Value.ToString() : "—");
        Row(panel, "gamification level", data.Gamification == null ? "— (gamification off)" : data.Gamification.Level.ToString());
        Row(panel, "gamification score", data.Gamification == null || !data.Gamification.Score.HasValue
            ? "— (not exposed by the native SDKs)" : data.Gamification.Score.Value.ToString());
    }

    private static void Row(RectTransform parent, string label, string value)
    {
        var caption = SampleUi.FlexibleLabel(parent, label, SampleUi.TextCaption, SampleUi.Muted);
        caption.name = "clientProfile-row-" + label.Replace(' ', '-');
        var body = SampleUi.FlexibleLabel(parent, string.IsNullOrEmpty(value) ? "—" : value, SampleUi.TextBody, SampleUi.TitleColor);
        body.richText = false;
    }
}
