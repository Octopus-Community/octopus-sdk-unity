using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Searchable scenario groups; search input survives all list updates.</summary>
public class OctopusScenariosListView : MonoBehaviour
{
    /// <summary>The shared catalogue's id for the search field.</summary>
    public const string SearchInputId = "scenarios-search-input";

    /// <summary>The "nothing matched" card, present only when the query filters everything out.</summary>
    public const string EmptyStateId = "scenarios-empty";

    /// <summary>The Sign-in section's feature-toggle row, which holds the `Force login` switch.</summary>
    public const string ForceLoginRowId = "scenarios-toggle-force-login-row";

    /// <summary>The line that appears under the switch once the SDK is up and it decides nothing.</summary>
    public const string ForceLoginLockedNoteId = "scenarios-toggle-force-login-locked";

    /// <summary>
    /// The link under that line to the Configuration screen, the one place that still changes
    /// Force login once the SDK is up.
    /// </summary>
    public const string ForceLoginConfigLinkId = "scenarios-toggle-force-login-config";

    public const string PushRegistrationRowId = "scenarios-toggle-push-registration-row";
    public const string PushRegistrationLockedNoteId = "scenarios-toggle-push-registration-locked";

    // Existing QA handles move with Unified Profile to its dedicated detail screen.
    public const string UnifiedProfileId = "scenarios-unified-profile";
    public const string UnifiedProfileBackendId = "expose-override-backend";
    public const string UnifiedProfileForceOnId = "expose-override-on";
    public const string UnifiedProfileForceOffId = "expose-override-off";
    public const string UnifiedProfileLiveValueId = "unified-profile-live-value";
    public const string UnifiedProfileRouteRowId = "unified-profile-route";
    public const string UnifiedProfileRouteToggleId = "unified-profile-route-toggle";

    /// <summary>
    /// The height of the block pinned above the scroll, in dp: the 48dp large title — which, under
    /// the 64dp app bar, makes Android's 112dp large top app bar — a 16dp gap and the 56dp outlined
    /// search field.
    /// </summary>
    public const float TopBlockDp = 120f;

    /// <summary>The large title's block under the app bar, in dp: 64 + 48 = Android's 112dp.</summary>
    public const float TitleBlockDp = 48f;

    /// <summary>Android's group row: 48dp, one caption line at most.</summary>
    public const float GroupRowMinDp = 48f;

    /// <summary>The Open-the-community tiles, Android's 48dp pills.</summary>
    public const float TileDp = 48f;

    /// <summary>The count badge: a 20dp filled pill, Android's `CountPill`.</summary>
    public const float CountBadgeDp = 20f;
    private const float SearchDp = 56f;
    private const float HeadRadiusDp = OctopusSampleBranding.CardRadius;

    private RectTransform _list;
    private RectTransform _live;
    private RectTransform _configBody;
    private bool _listening;
    private bool _lockedAsDrawn;
    private bool _initAsDrawn;
    private bool _repaintRequested;
    private float _queryChangedAt;
    private string _query = "";
    private TMP_InputField _searchField;

    private enum ConfigPhase { Idle, Loading, Loaded, Unavailable, Failed }
    private ConfigPhase _configPhase;
    private int _configGen;
    private int _communityAsDrawn;
    private float _configStartedAt;
    private TMP_Text _openStatus;
    private readonly OctopusSampleNativeRequest _native = new OctopusSampleNativeRequest();
    private string _configError = "";
    private List<KeyValuePair<string, string>> _configLines;

    public sealed class ViewState
    {
        public string Query = "";
        public readonly Dictionary<ScenarioSection, bool> Open = new Dictionary<ScenarioSection, bool>();
    }

    public ViewState CaptureState()
    {
        var state = new ViewState { Query = _query };
        foreach (var pair in _open) state.Open[pair.Key] = pair.Value;
        return state;
    }
    private readonly Dictionary<ScenarioSection, bool> _open = new Dictionary<ScenarioSection, bool>();

    /// <summary>
    /// Fills <paramref name="host"/> — the shell's content area — with the scrollable list.
    /// </summary>
    public static OctopusScenariosListView BuildInto(RectTransform host, ViewState state = null)
    {
        var view = host.gameObject.AddComponent<OctopusScenariosListView>();
        if (state != null)
        {
            view._query = state.Query;
            foreach (var pair in state.Open) view._open[pair.Key] = pair.Value;
        }
        BuildLargeTitle(host);
        view._searchField = BuildSearch(host, view._query);
        view._searchField.onValueChanged.AddListener(view.QueueQuery);
        var padding = SampleUi.ContentPadding();
        padding.top += Mathf.RoundToInt(OctopusSampleBranding.Dp(TopBlockDp));
        var content = SampleUi.VerticalScroll(host, padding);
        content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd);
        view.Build(content);
        return view;
    }

    /// <summary>The live query, as the search field last reported it.</summary>
    public string Query { get { return _query; } }

    /// <summary>
    /// Whether <paramref name="section"/> shows its cards. A non-blank query opens every section
    /// regardless — a card hidden by a collapse the reader set ten taps ago reads as "no result".
    /// </summary>
    public bool IsSectionOpen(ScenarioSection section)
    {
        if (Searching) return true;
        bool open;
        return _open.TryGetValue(section, out open) && open;
    }

    /// <summary>Collapses an open section, or opens a collapsed one, and repaints the list.</summary>
    public void ToggleSection(ScenarioSection section)
    {
        // Reads through IsSectionOpen so the default (collapsed) is stated in one place, and writes the
        // remembered position rather than the effective one: a toggle made while searching would
        // otherwise persist "open" for every section it touched.
        _open[section] = !IsSectionOpen(section);
        Repaint();
    }

    // Retained for callers of the previous list entry; it now navigates to a screen.
    public void ToggleUnifiedProfile() { OpenScenario("unifiedProfile"); }

    public void ResetForTabSwitch()
    {
        _open.Clear();
        SetQuery("");
    }

    private void QueueQuery(string query)
    {
        _query = query ?? "";
        _queryChangedAt = Time.unscaledTime;
        _repaintRequested = true;
    }

    private void LateUpdate()
    {
        ExpireConfigRead(Time.realtimeSinceStartup);
        HoldTop(Time.unscaledTime);
        if (!_repaintRequested || Time.unscaledTime - _queryChangedAt < 0.12f) return;
        _repaintRequested = false;
        Repaint();
        ScrollToTop();
    }

    /// <summary>How long a query's return to the top is held against a late layout or viewport change.</summary>
    public const float TopHoldSeconds = 0.6f;

    private float _holdTopUntil = -1f;
    private bool _holdTopNextFrame;
    private Vector2 _heldViewport;

    /// <summary>
    /// Back to the top of the list after a query change: the filtered list is shorter, and an
    /// offset kept from the full one would leave its results above the viewport.
    ///
    /// The layout is rebuilt first, so the position is set against the filtered list rather than
    /// the one it replaces, then held for <see cref="TopHoldSeconds"/>: re-applied on the next
    /// frame and whenever the viewport changes size. Typing with the soft keyboard open used to
    /// leave the results area blank until a scroll gesture (device QA), the reset having run
    /// before the list and the keyboard-shrunk viewport settled.
    /// </summary>
    private void ScrollToTop()
    {
        FocusedSection = null;
        var scroll = GetComponentInChildren<ScrollRect>(true);
        if (scroll == null) return;
        PinTop(scroll, false);
        _heldViewport = ViewportSize(scroll);
        _holdTopNextFrame = true;
        _holdTopUntil = Time.unscaledTime + TopHoldSeconds;
    }

    /// <summary>
    /// Re-applies a pending return to the top, once on the frame after it and again on any viewport
    /// resize, until <see cref="TopHoldSeconds"/> have passed or the reader scrolls. True when it
    /// re-applied. Called every frame; <paramref name="now"/> is unscaled time.
    /// </summary>
    internal bool HoldTop(float now)
    {
        if (_holdTopUntil < 0f) return false;
        var scroll = GetComponentInChildren<ScrollRect>(true);
        // A drag or a fling is the reader taking over: stop holding.
        if (scroll == null || now > _holdTopUntil || scroll.velocity.sqrMagnitude > 1f)
        {
            _holdTopUntil = -1f;
            return false;
        }
        var viewport = ViewportSize(scroll);
        if (!_holdTopNextFrame && viewport == _heldViewport) return false;
        _holdTopNextFrame = false;
        _heldViewport = viewport;
        PinTop(scroll, true);
        return true;
    }

    private static void PinTop(ScrollRect scroll, bool recull)
    {
        scroll.StopMovement();
        if (scroll.content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.verticalNormalizedPosition = 1f;
        if (!recull) return;
        // Cards rebuilt under a clip rect that did not move can keep a stale cull state until the
        // content moves; re-registering them with the mask clips and culls them afresh.
        var mask = scroll.viewport != null ? scroll.viewport.GetComponent<RectMask2D>() : null;
        if (mask != null && mask.enabled)
        {
            mask.enabled = false;
            mask.enabled = true;
        }
    }

    private static Vector2 ViewportSize(ScrollRect scroll)
    {
        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        return viewport.rect.size;
    }

    /// <summary>
    /// The section <see cref="ScrollTo"/> last brought to the top, or null. Test-only observable:
    /// the scroll offset itself depends on the canvas size an EditMode run happens to have.
    /// </summary>
    public ScenarioSection? FocusedSection { get; private set; }

    /// <summary>
    /// Scrolls the list so the card of <paramref name="section"/> starts at the top of the
    /// viewport, as far as the list's length allows. False when that section is not on screen —
    /// filtered out by the query, for instance.
    /// </summary>
    public bool ScrollTo(ScenarioSection section)
    {
        var card = SectionCard(section);
        var scroll = GetComponentInChildren<ScrollRect>();
        if (card == null || scroll == null || scroll.content == null) return false;
        // Takes over from the pending return to the top that SetQuery("") arms on the way here:
        // left armed, the next frame would pin the list back on its first card.
        _holdTopUntil = -1f;
        _holdTopNextFrame = false;
        FocusedSection = section;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        var scrollable = scroll.content.rect.height - viewport.rect.height;
        if (scrollable <= 0f)
        {
            scroll.verticalNormalizedPosition = 1f;
            return true;
        }
        var corners = new Vector3[4];
        card.GetWorldCorners(corners);
        // corners[1] is the card's top-left; measured from the content's top edge.
        var fromTop = scroll.content.rect.yMax - scroll.content.InverseTransformPoint(corners[1]).y;
        scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(fromTop / scrollable);
        return true;
    }

    private RectTransform SectionCard(ScenarioSection section)
    {
        if (_list == null) return null;
        var headerId = OctopusScenarioSections.HeaderIdOf(section);
        for (var i = 0; i < _list.childCount; i++)
        {
            var card = _list.GetChild(i);
            if (card.gameObject.activeSelf && card.Find("HeadSlot/" + headerId) != null) return (RectTransform)card;
        }
        return null;
    }

    /// <summary>Applies a new search query and repaints. Wired to the search field's own events.</summary>
    public void SetQuery(string query)
    {
        _query = query ?? "";
        if (_searchField != null) _searchField.SetTextWithoutNotify(_query);
        _repaintRequested = false;
        Repaint();
        ScrollToTop();
    }

    /// <summary>The empty state's exact wording, echoing the query the way React Native's does.</summary>
    public static string EmptyStateText(string query)
    {
        return "No scenario matches \"" + (query ?? "").Trim() + "\".";
    }

    private bool Searching { get { return _query.Trim().Length > 0; } }

    private void Build(RectTransform content)
    {
        // The two Android cards above the groups: how to open the community, and what the SDK
        // resolved for it. Hidden while searching — a query only ever matches scenario cards.
        _live = SampleUi.Panel("LiveCommunity", content, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(_live, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd), new RectOffset(), false);
        BuildOpenCommunityCard(_live);
        BuildCommunityConfigPreview(_live);

        // Its own container, so a query repaints the list without touching the search field — which
        // would take the caret and the text with it on every keystroke.
        _list = SampleUi.Panel("SectionList", content, Color.clear);
        SampleUi.VerticalStack(_list, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd), new RectOffset(), false);

        // Subscribed from here rather than only from OnEnable, for the reason the Home dashboard
        // gives: in edit mode Unity never calls OnEnable, so a test that initialises the SDK after
        // building would watch a list that never hears about it. Listen() is idempotent.
        Listen();
        Repaint();
    }

    /// <summary>
    /// Android's large top app bar title: a 48dp block under the 64dp compact bar (112dp in all),
    /// its 28sp title in Title ink on the page colour, 16dp from the edge. The shell hides its own
    /// compact title on this tab and draws its bar on the page colour too.
    /// </summary>
    private static void BuildLargeTitle(RectTransform host)
    {
        var p = OctopusSampleBranding.Palette;
        var block = SampleUi.Panel("LargeTitle", host, p.PageHeader);
        SampleUiHalo.PaintPage(block.GetComponent<Image>());
        SampleUi.Stretch(block, new Vector2(0f, 1f), Vector2.one);
        block.pivot = new Vector2(0.5f, 1f);
        block.anchoredPosition = Vector2.zero;
        block.sizeDelta = new Vector2(0f, OctopusSampleBranding.Dp(TitleBlockDp));
        var title = SampleUi.Label("Title", block, "Scenarios", 84, p.Title, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        var scaled = title.GetComponent<SampleUiScaledText>();
        if (scaled != null) scaled.Configure(84, OctopusSampleBranding.Dp(36f), singleLine: true);
        SampleUi.Stretch(title.rectTransform, Vector2.zero, Vector2.one);
        float side = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
        title.rectTransform.offsetMin = new Vector2(side, 0f);
        title.rectTransform.offsetMax = new Vector2(-side, 0f);
    }

    /// <summary>
    /// Android's outlined search field: 56dp, no fill, a leading magnifier, 16dp from both edges and
    /// 16dp under the large title. The sample ships no search glyph, so the magnifier is drawn: a
    /// stroked ring and a handle.
    /// </summary>
    private static TMP_InputField BuildSearch(RectTransform host, string query)
    {
        var p = OctopusSampleBranding.Palette;
        var search = SampleUi.Panel("Search", host, OctopusSampleBranding.Clear);
        SampleUi.Stretch(search, new Vector2(0f, 1f), Vector2.one);
        search.pivot = new Vector2(0.5f, 1f);
        search.anchoredPosition = new Vector2(0f, -OctopusSampleBranding.Dp(TitleBlockDp + OctopusSampleBranding.SpaceLg));
        search.sizeDelta = new Vector2(0f, OctopusSampleBranding.Dp(SearchDp));
        int side = Mathf.RoundToInt(OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg));
        SampleUi.VerticalStack(search, 0f, new RectOffset(side, side, 0, 0), false);

        var field = SampleUi.Field(SearchInputId, search, query, placeholder: "Search scenarios");
        var size = field.GetComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = OctopusSampleBranding.Dp(SearchDp);
        size.layoutPriority = 2;
        var elevation = field.GetComponent<SampleUiElevation>();
        if (elevation != null)
        {
            if (Application.isPlaying) Destroy(elevation); else DestroyImmediate(elevation);
        }
        var styled = field as SampleUiInputField;
        if (styled != null) { styled.Outlined = true; styled.RefreshVisuals(); }
        if (field.textViewport != null)
            field.textViewport.offsetMin = new Vector2(OctopusSampleBranding.Dp(52f), field.textViewport.offsetMin.y);

        var icon = SampleUi.Panel("SearchIcon", field.transform, OctopusSampleBranding.Clear);
        icon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(24f);
        icon.anchoredPosition = new Vector2(OctopusSampleBranding.Dp(16f), 0f);
        var ring = SampleUi.Panel("Ring", icon, p.Muted);
        var ringImage = ring.GetComponent<Image>();
        ringImage.sprite = SampleUiShapes.Rounded(7f, 2f);
        ringImage.type = Image.Type.Sliced;
        ringImage.raycastTarget = false;
        ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
        ring.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(14f);
        ring.anchoredPosition = new Vector2(-OctopusSampleBranding.Dp(2f), OctopusSampleBranding.Dp(2f));
        var handle = SampleUi.Panel("Handle", icon, p.Muted);
        handle.GetComponent<Image>().raycastTarget = false;
        handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(OctopusSampleBranding.Dp(2f), OctopusSampleBranding.Dp(7f));
        handle.anchoredPosition = new Vector2(OctopusSampleBranding.Dp(6.5f), -OctopusSampleBranding.Dp(6.5f));
        handle.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return field;
    }

    /// <summary>
    /// Android's `OpenCommunityCard`: an accent-tinted card holding a 2x2 grid — Tab, Modal, Sheet,
    /// Full screen. The Unity SDK opens full screen only, so the other three stay in the grid,
    /// disabled, and a caption says why (<see cref="OctopusSampleOpenModes"/>).
    /// </summary>
    private void BuildOpenCommunityCard(RectTransform parent)
    {
        var p = OctopusSampleBranding.Palette;
        var card = SampleUi.Panel(OctopusSampleOpenModes.CardId, parent, p.TabIndicator, 16f,
            OctopusSampleBranding.Tint(p.Accent, p.TabIndicator, 0.35f));
        int pad = Mathf.RoundToInt(OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg));
        SampleUi.VerticalStack(card, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd),
            new RectOffset(pad, pad, Mathf.RoundToInt(OctopusSampleBranding.Dp(4f)), pad), false);

        var header = SampleUi.Panel("Header", card, OctopusSampleBranding.Clear);
        var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm);
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = headerLayout.childForceExpandHeight = false;
        header.gameObject.AddComponent<LayoutElement>().minHeight = OctopusSampleBranding.Dp(48f);
        var glyph = SampleUi.Panel("Icon", header, OctopusSampleBranding.Clear);
        SampleUiIcons.Apply(glyph.GetComponent<Image>(), "forum", p.Accent);
        glyph.GetComponent<Image>().raycastTarget = false;
        var glyphSize = glyph.gameObject.AddComponent<LayoutElement>();
        glyphSize.minWidth = glyphSize.preferredWidth = OctopusSampleBranding.Dp(20f);
        glyphSize.minHeight = glyphSize.preferredHeight = OctopusSampleBranding.Dp(20f);
        var title = SampleUi.Label("Title", header, OctopusSampleOpenModes.Title, SampleUi.TextLabel, p.Title,
            TextAnchor.MiddleLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        SampleUi.FlexibleLabel(card, OctopusSampleOpenModes.Caption, SampleUi.TextCaption, p.Muted);

        var grid = SampleUi.Panel("Grid", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(grid, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm), new RectOffset(), false);
        RectTransform row = null;
        for (var i = 0; i < OctopusSampleOpenModes.Order.Length; i++)
        {
            if (i % 2 == 0)
            {
                row = SampleUi.Panel("Row", grid, OctopusSampleBranding.Clear);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm);
                rowLayout.childControlWidth = rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childForceExpandHeight = false;
            }
            var mode = OctopusSampleOpenModes.Order[i];
            var button = SampleUi.Button(OctopusSampleOpenModes.Id(mode), row, OctopusSampleOpenModes.Label(mode),
                mode == OctopusSampleOpenModes.FirstAvailable() ? SampleUiButtonVariant.Primary : SampleUiButtonVariant.Secondary,
                () => OpenCommunity(mode));
            var size = button.GetComponent<LayoutElement>();
            size.flexibleWidth = 1f;
            size.preferredWidth = 0f;
            // Fixed: the pill sprite's slice borders would otherwise report a 96dp preferred height.
            size.minHeight = size.preferredHeight = OctopusSampleBranding.Dp(TileDp);
            size.layoutPriority = Mathf.Max(size.layoutPriority, 2);
            if (!OctopusSampleOpenModes.IsAvailable(mode)) button.GetComponent<SampleUiButton>().interactable = false;
        }
        var unavailable = SampleUi.FlexibleLabel(card, OctopusSampleOpenModes.UnavailableCaption,
            SampleUi.TextCaption, p.Muted);
        unavailable.name = OctopusSampleOpenModes.UnavailableCaptionId;
        SampleUiLineClamp.Apply(unavailable, 1);
        _openStatus = SampleUi.FlexibleLabel(card, "", SampleUi.TextCaption, p.Muted);
        _openStatus.name = OctopusSampleOpenModes.StatusId;
        _openStatus.richText = false;
        _openStatus.gameObject.SetActive(false);
    }

    /// <summary>
    /// Opens the community the way the Community tab does (<see cref="OctopusSampleNativeRequest"/>):
    /// the SDK is brought up first, the call is logged, a throw becomes a line under the grid.
    /// </summary>
    private void OpenCommunity(OctopusSampleOpenMode mode)
    {
        if (!OctopusSampleOpenModes.IsAvailable(mode)) return;
        bool notReady;
        var failure = _native.Request("OctopusSDK.Open", null, () => OctopusScenarioSdk.Current.Open(), out notReady);
        if (_openStatus == null) return;
        _openStatus.gameObject.SetActive(failure != null);
        if (failure == null) return;
        _openStatus.text = failure;
        _openStatus.color = notReady ? OctopusSampleBranding.Palette.Muted : OctopusSampleBranding.Palette.Negative;
    }

    private void OnApplicationPause(bool paused)
    {
        if (_native.OnPause(paused))
            OctopusSampleLog.Current.LogStateChange("Returned to the sample", "Open the community");
    }

    /// <summary>
    /// Android's `CommunityConfigPreview`, as far as the Unity SDK exposes it: the four fields of
    /// <c>DebugGetCommunityConfig</c>, read on tap — never at build time, so opening the tab still
    /// calls nothing — and a LIMITS line naming what it cannot show.
    /// </summary>
    private void BuildCommunityConfigPreview(RectTransform parent)
    {
        var p = OctopusSampleBranding.Palette;
        var wrapper = SampleUi.Panel(OctopusSampleCommunityConfigLines.WrapperId, parent, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(wrapper, 0f, new RectOffset(), false);
        var card = SampleUi.Panel(OctopusSampleCommunityConfigLines.PreviewId, wrapper, p.Surface,
            HeadRadiusDp, p.Border);
        SampleUi.VerticalStack(card, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs),
            SampleUi.Padding(OctopusSampleBranding.SpaceMd, OctopusSampleBranding.SpaceMd), false);
        var title = SampleUi.Label("Title", card, OctopusSampleCommunityConfigLines.Title, SampleUi.TextLabel,
            p.Title, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyles.Bold;
        SampleUi.FlexibleLabel(card, OctopusSampleCommunityConfigLines.Description, SampleUi.TextCaption, p.Muted);
        _configBody = SampleUi.Panel("ConfigBody", card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(_configBody, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs), new RectOffset(), false);
        var limits = SampleUi.FlexibleLabel(card, OctopusSampleCommunityConfigLines.Limits, SampleUi.TextCaption, p.Muted);
        limits.name = "Limits";
        _configPhase = ConfigPhase.Idle;
        RenderConfig();
    }

    private void RenderConfig()
    {
        if (_configBody == null) return;
        _initAsDrawn = OctopusSampleState.IsInitialized;
        _communityAsDrawn = OctopusScenarioSdk.CommunityGeneration;
        for (var i = _configBody.childCount - 1; i >= 0; i--)
        {
            var child = _configBody.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        var p = OctopusSampleBranding.Palette;
        if (!_initAsDrawn) _configPhase = ConfigPhase.Idle;
        ConfigLine(OctopusSampleCommunityConfigLines.ApiKeyLabel, OctopusScenarioSdk.LiveMaskedApiKey);
        switch (_configPhase)
        {
            case ConfigPhase.Loading:
                ConfigLine(OctopusSampleCommunityConfigLines.StatusLabel, OctopusSampleCommunityConfigLines.Loading);
                break;
            case ConfigPhase.Loaded:
                foreach (var line in _configLines) ConfigLine(line.Key, line.Value);
                ConfigButton(OctopusSampleCommunityConfigLines.RefreshLabel);
                break;
            case ConfigPhase.Unavailable:
                ConfigLine(OctopusSampleCommunityConfigLines.StatusLabel, OctopusSampleCommunityConfigLines.NotAvailable);
                ConfigButton(OctopusSampleCommunityConfigLines.RetryLabel);
                break;
            case ConfigPhase.Failed:
                var error = SampleUi.Panel(OctopusSampleCommunityConfigLines.ErrorId, _configBody, OctopusSampleBranding.Clear);
                SampleUi.VerticalStack(error, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs), new RectOffset(), false);
                SampleUi.FlexibleLabel(error, _configError, SampleUi.TextCaption, p.Negative).richText = false;
                ConfigButton(OctopusSampleCommunityConfigLines.RetryLabel);
                break;
            default:
                if (!_initAsDrawn)
                    ConfigLine(OctopusSampleCommunityConfigLines.StatusLabel, OctopusSampleCommunityConfigLines.NotInitialised);
                else
                    ConfigButton(OctopusSampleCommunityConfigLines.ReadLabel);
                break;
        }
    }

    private void ConfigLine(string label, string value)
    {
        SampleUi.FlexibleLabel(_configBody, OctopusSampleCommunityConfigLines.Line(label, value), SampleUi.TextCaption,
            OctopusSampleBranding.Palette.Title).richText = false;
    }

    private void ConfigButton(string label)
    {
        var holder = SampleUi.Panel("Actions", _configBody, OctopusSampleBranding.Clear);
        var layout = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        var button = SampleUi.Button(OctopusSampleCommunityConfigLines.ReadButtonId, holder, label,
            SampleUiButtonVariant.Secondary, ReadConfig);
        // Fixed, like Android's 40dp outlined button in its 48dp touch row: the pill sprite's
        // slice borders would otherwise report a 96dp preferred height.
        var size = button.GetComponent<LayoutElement>();
        if (size == null) size = button.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = OctopusSampleBranding.MinTouchUnits;
        size.layoutPriority = Mathf.Max(size.layoutPriority, 2);
    }

    /// <summary>
    /// Whether the panel still shows a reading of the community the SDK runs now. A stop, a start
    /// or a switch — onto any community, even the same one — makes it stale.
    /// </summary>
    private bool ConfigStale()
    {
        return OctopusSampleState.IsInitialized != _initAsDrawn ||
            OctopusScenarioSdk.CommunityGeneration != _communityAsDrawn;
    }

    /// <summary>
    /// Drops whatever the panel shows — a reading, a read in flight, an error — and goes back to
    /// the Read button. Bumping the generation makes a late answer of the old read land nowhere.
    /// </summary>
    private void ResetConfig()
    {
        ++_configGen;
        _configPhase = ConfigPhase.Idle;
        _configLines = null;
        RenderConfig();
    }

    /// <summary>
    /// Gives the Read button back when a read stayed on "loading…" past
    /// <see cref="OctopusSampleCommunityConfigLines.ReadTimeoutSeconds"/>: a callback the SDK never
    /// delivers must not leave the panel without a way to retry. Called every frame with the clock.
    /// </summary>
    internal bool ExpireConfigRead(float now)
    {
        if (_configPhase != ConfigPhase.Loading ||
            now - _configStartedAt < OctopusSampleCommunityConfigLines.ReadTimeoutSeconds) return false;
        OctopusSampleLog.Current.LogStateChange("Community config read timed out",
            OctopusSampleCommunityConfigLines.ReadTimeoutSeconds + " s without an answer");
        ResetConfig();
        return true;
    }

    /// <summary>Reads the community config once. Never initialises the SDK: that is a scenario's job.</summary>
    private void ReadConfig()
    {
        if (!OctopusSampleState.IsInitialized) { ResetConfig(); return; }
        var gen = ++_configGen;
        _configPhase = ConfigPhase.Loading;
        _configStartedAt = Time.realtimeSinceStartup;
        RenderConfig();
        try
        {
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DebugGetCommunityConfig", "");
            OctopusScenarioSdk.Current.DebugGetCommunityConfig(config =>
            {
                if (this == null || gen != _configGen) return;
                if (config == null)
                {
                    _configPhase = ConfigPhase.Unavailable;
                }
                else
                {
                    _configLines = OctopusSampleCommunityConfigLines.Lines(config.ExposeClientUserId,
                        config.TermsAcceptanceMode, config.ForceLoginOnStrongActions, config.DisplayAccountAge, config.ForceNicknameChoice, config.ConsentTexts);
                    _configPhase = ConfigPhase.Loaded;
                }
                RenderConfig();
            }, error =>
            {
                if (this == null || gen != _configGen) return;
                _configError = "Config read failed: " + error;
                _configPhase = ConfigPhase.Failed;
                RenderConfig();
            });
        }
        catch (System.Exception exception)
        {
            if (gen != _configGen) return;
            _configError = "Config read failed: " + exception.Message;
            _configPhase = ConfigPhase.Failed;
            RenderConfig();
        }
    }

    // Explicit for retained tabs: EditMode does not send the MonoBehaviour activation callbacks.
    public void SetTabActive(bool active)
    {
        if (!active) { StopListening(); return; }
        Listen();
        // State changes made while the tab was hidden were not heard (StopListening above), so
        // the lock and Force login rows must be redrawn on every return.
        _repaintRequested = false;
        Repaint();
        if (ConfigStale()) ResetConfig();
    }

    private void OnEnable() { Listen(); }

    private void OnDisable() { StopListening(); }

    private void OnDestroy()
    {
        // Edit mode delivers no OnDisable either, and a handler left on a static event would fire
        // on a destroyed view in the next test.
        StopListening();
        ++_configGen;
    }

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
    /// Repaints when — and only when — Force login becomes locked, and resets the config preview
    /// to its Read button when the SDK comes up, goes down or changes community — a reading of the
    /// previous community, or a read still in flight for it, must not stay on screen.
    ///
    /// The list itself reads a static catalogue, so nothing else here depends on the sample state,
    /// and rebuilding every card each time a notification count arrives would be waste. The lock
    /// does depend on it, and on a change nothing else would deliver: a scenario initialises the
    /// SDK from a screen drawn *over* this list, so returning from it would otherwise leave a live
    /// switch on a header built before the SDK came up.
    /// </summary>
    private void OnStateChanged()
    {
        if (ConfigStale()) ResetConfig();
        if (Locked() == _lockedAsDrawn) return;
        Repaint();
    }

    private static bool Locked()
    {
        return OctopusSampleFeatureToggles.ForceLoginLockedNote().Length > 0;
    }

    private void Repaint()
    {
        if (_list == null) return;
        _repaintRequested = false;

        // Recorded here rather than where the row is built: a query that filters the Sign-in
        // section out draws no switch at all, and a stale value would then keep OnStateChanged
        // from repainting once the query is cleared.
        _lockedAsDrawn = Locked();
        if (_live != null) _live.gameObject.SetActive(!Searching);

        for (var i = _list.childCount - 1; i >= 0; i--)
        {
            var child = _list.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        var groups = OctopusScenarioSections.Filter(_query);
        if (groups.Count == 0)
        {
            var empty = SampleUi.Card(EmptyStateId, _list);
            SampleUi.FlexibleLabel(empty, EmptyStateText(_query), SampleUi.TextBody, SampleUi.Muted);
            return;
        }

        foreach (var group in groups)
        {
            // A flat stack, as Android's groups: the head is the only framed surface; open
            // cards follow it 8dp apart.
            var section = SampleUi.Panel("Section", _list, OctopusSampleBranding.Clear);
            SampleUi.VerticalStack(section, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm), new RectOffset(), false);
            var headSlot = SampleUi.Panel("HeadSlot", section, OctopusSampleBranding.Clear);
            SampleUi.VerticalStack(headSlot, 0f, new RectOffset(), false);
            BuildSectionHead(group, headSlot);
            // As on Android, the switch stays while searching: a query does not change what it does.
            // The slot holds the head and its switch overlay only: the switch stretches over the
            // whole slot, so anything else in it would pull the switch off the row's centre.
            BuildFeatureToggle(group, headSlot);
            BuildLockedNote(group, section);
            if (!IsSectionOpen(group.Section)) continue;
            foreach (var scenario in group.Scenarios) BuildCard(scenario, section);
        }
    }

    private static bool HasToggle(ScenarioSection section)
    {
        return section == ScenarioSection.SignIn || section == ScenarioSection.Notifications;
    }

    /// <summary>
    /// Android's collapsed group row: a rounded surface, 48dp at least and 12x10dp padding, holding
    /// the title, the effect of the section's switch on one ellipsized line when it has one, the
    /// count pill, the switch's slot and the chevron. The switch itself is the next sibling
    /// (<see cref="BuildFeatureToggle"/>), laid over that slot, so a tap on it never collapses the
    /// section; the Force login lock link goes under the row (<see cref="BuildLockedNote"/>).
    /// </summary>
    private void BuildSectionHead(ScenarioSectionGroup group, RectTransform parent)
    {
        var p = OctopusSampleBranding.Palette;
        var head = SampleUi.Panel(group.HeaderId, parent, p.SurfaceHigh);
        var image = head.GetComponent<Image>();
        image.sprite = SampleUiShapes.Rounded(HeadRadiusDp);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        var layout = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = SampleUi.Padding(OctopusSampleBranding.SpaceMd, 10f);
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        head.gameObject.AddComponent<LayoutElement>().minHeight = OctopusSampleBranding.Dp(GroupRowMinDp);

        var copy = SampleUi.Panel("Copy", head, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(copy, OctopusSampleBranding.Dp(2f), new RectOffset(), false);
        var copySize = copy.gameObject.AddComponent<LayoutElement>();
        copySize.minWidth = 0f;
        copySize.preferredWidth = 0f;
        copySize.flexibleWidth = 1f;
        var title = SampleUi.Label("Title", copy, group.Title, SampleUi.TextLabel, p.Title, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyles.Bold;
        SampleUiLineClamp.Apply(title, 1);

        TMP_Text caption = null;
        bool toggle = HasToggle(group.Section);
        if (toggle)
        {
            var push = group.Section == ScenarioSection.Notifications;
            var on = push ? OctopusSampleFeatureToggles.PushRegistration : OctopusSampleFeatureToggles.ForceLogin;
            var label = push ? OctopusSampleFeatureToggles.PushRegistrationLabel : OctopusSampleFeatureToggles.ForceLoginLabel;
            var effect = push ? OctopusSampleFeatureToggles.PushRegistrationEffect(on) : OctopusSampleFeatureToggles.ForceLoginEffect(on);
            caption = SampleUi.FlexibleLabel(copy, label + ": " + effect, SampleUi.TextCaption, p.Muted);
            SampleUiLineClamp.Apply(caption, 1);
        }

        // Android's `CountPill`: a 20dp filled pill. Its height is fixed, because a pill sprite's
        // slice borders report a 96dp preferred size — the circle the QA saw.
        var badge = SampleUi.Panel("CountBadge", head, p.AccentIndicator);
        var badgeImage = badge.GetComponent<Image>();
        badgeImage.sprite = SampleUiShapes.Rounded(CountBadgeDp / 2f);
        badgeImage.type = Image.Type.Sliced;
        badgeImage.raycastTarget = false;
        var badgeStack = SampleUi.VerticalStack(badge, 0f, SampleUi.Padding(OctopusSampleBranding.SpaceSm, 2f), false);
        badgeStack.childForceExpandWidth = false;
        badgeStack.childAlignment = TextAnchor.MiddleCenter;
        var badgeSize = badge.gameObject.AddComponent<LayoutElement>();
        badgeSize.minWidth = OctopusSampleBranding.Dp(CountBadgeDp);
        badgeSize.minHeight = badgeSize.preferredHeight = OctopusSampleBranding.Dp(CountBadgeDp);
        badgeSize.layoutPriority = 2;
        var count = SampleUi.Label("Count", badge, OctopusSampleCommunityConfigLines.CountText(group.Scenarios.Count),
            SampleUi.TextCaption, p.OnAccentIndicator, TextAnchor.MiddleCenter);
        count.fontStyle = FontStyles.Bold;
        count.textWrappingMode = TextWrappingModes.NoWrap;

        if (toggle)
        {
            var slot = SampleUi.Panel("SwitchSlot", head, OctopusSampleBranding.Clear);
            var slotSize = slot.gameObject.AddComponent<LayoutElement>();
            slotSize.minWidth = slotSize.preferredWidth = OctopusSampleBranding.Dp(OctopusSampleBranding.SwitchWidth);
        }

        var chevron = SampleUi.Label("Chevron", head, IsSectionOpen(group.Section) ? "⌃" : "⌄",
            SampleUi.TextTitle, p.Muted, TextAnchor.MiddleCenter);
        var chevronSize = chevron.gameObject.AddComponent<LayoutElement>();
        chevronSize.minWidth = chevronSize.preferredWidth = OctopusSampleBranding.Dp(24f);
        if (Searching || group.Scenarios.Count == 0) return;

        image.raycastTarget = true;
        head.gameObject.AddComponent<SampleUiTouchTarget>();
        var button = head.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = image;
        button.Configure(image, null, title, p.SurfaceHigh, p.Title, p.Border, HeadRadiusDp, detail: caption);
        var section = group.Section;
        button.onClick.AddListener(() => ToggleSection(section));
    }

    /// <summary>
    /// The Force login lock, once the SDK is up: one accent line under the group row that is itself
    /// the link to the Configuration screen, whose profiles still change it live. Under the row
    /// rather than in it, so the row keeps Android's 48–56dp — and a sibling of the head's slot,
    /// not a child: the switch overlay stretches over that slot and would centre on the seam.
    /// </summary>
    private static void BuildLockedNote(ScenarioSectionGroup group, RectTransform parent)
    {
        if (group.Section != ScenarioSection.SignIn) return;
        var locked = OctopusSampleFeatureToggles.ForceLoginLockedNote();
        if (locked.Length > 0) BuildLockedLink(parent, locked);
    }

    private static void BuildLockedLink(RectTransform parent, string locked)
    {
        var p = OctopusSampleBranding.Palette;
        var link = SampleUi.Panel(ForceLoginConfigLinkId, parent, OctopusSampleBranding.Clear);
        var fill = link.GetComponent<Image>();
        fill.raycastTarget = true;
        SampleUi.VerticalStack(link, 0f, new RectOffset(), false).childAlignment = TextAnchor.MiddleLeft;
        link.gameObject.AddComponent<LayoutElement>().minHeight = OctopusSampleBranding.MinTouchUnits;
        link.gameObject.AddComponent<SampleUiTouchTarget>();
        var note = SampleUi.Label(ForceLoginLockedNoteId, link, locked,
            SampleUi.TextCaption, p.Accent, TextAnchor.MiddleLeft);
        // Never clamped: the note explains where to change the profile and that it applies live.
        var button = link.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = fill;
        button.onClick.AddListener(() => OctopusSampleConfigView.Open());
        button.Configure(fill, null, note, OctopusSampleBranding.Clear, p.Accent, OctopusSampleBranding.Clear, 0f);
    }

    /// <summary>
    /// The section's feature switch, the head's next sibling — and nowhere else, which is the
    /// shared design contract's rule for a toggle. Sign-in and Notifications carry switches;
    /// every other section returns without adding a row.
    ///
    /// The row takes no layout space: it lies over the head, the switch right-aligned on the slot
    /// the head keeps free for it, so the group stays one 48–56dp row as on Android. What the switch
    /// does is the head's caption, in product words, in both positions. Force login locks once the
    /// SDK is up (<see cref="OctopusSampleFeatureToggles"/>); the view listens to
    /// <see cref="OctopusSampleState.Changed"/> so it also locks when a scenario initialises the
    /// SDK over this list. Push registration stays interactable.
    /// </summary>
    private void BuildFeatureToggle(ScenarioSectionGroup group, RectTransform parent)
    {
        if (!HasToggle(group.Section)) return;

        var push = group.Section == ScenarioSection.Notifications;
        var id = push ? OctopusSampleFeatureToggles.PushRegistrationId : OctopusSampleFeatureToggles.ForceLoginId;
        var on = push ? OctopusSampleFeatureToggles.PushRegistration : OctopusSampleFeatureToggles.ForceLogin;
        var locked = push ? string.Empty : OctopusSampleFeatureToggles.ForceLoginLockedNote();

        var row = SampleUi.Panel(push ? PushRegistrationRowId : ForceLoginRowId, parent, OctopusSampleBranding.Clear);
        row.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        SampleUi.Stretch(row, Vector2.zero, Vector2.one);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        // Head padding (12dp) + chevron (24dp) + spacing (8dp): the switch lands on its slot.
        layout.padding = new RectOffset(0, Mathf.RoundToInt(OctopusSampleBranding.Dp(
            OctopusSampleBranding.SpaceMd + 24f + OctopusSampleBranding.SpaceSm)), 0, 0);
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var next = !on;
        SampleUi.Switch(id, row, on, locked.Length == 0, () => FlipFeatureToggle(push, next));
    }

    /// <summary>
    /// Moves the switch, then repaints — the row draws its own position and its own effect line, so
    /// a flip that did not repaint would leave both stating the position the tester just left.
    /// </summary>
    private void FlipFeatureToggle(bool push, bool enabled)
    {
        var changed = push ? OctopusSampleFeatureToggles.SetPushRegistration(enabled)
                           : OctopusSampleFeatureToggles.SetForceLogin(enabled);
        if (changed) Repaint();
    }

    internal OctopusScenarioScreenView OpenScenario(string id)
    {
        var pilot = OctopusScenarioPilots.Create(id);
        return pilot == null ? null : OctopusScenarioScreenView.Open(pilot);
    }

    /// <summary>
    /// Android's `ScenarioCard`: an outlined 12dp card, no chevron — the title, what you will
    /// observe, then the one SDK symbol in a filled monospace chip.
    /// </summary>
    private void BuildCard(OctopusScenario scenario, RectTransform parent)
    {
        var p = OctopusSampleBranding.Palette;
        // One GameObject.name per card: every sibling used to be called "Card", which makes a
        // hierarchy dump and a failing UI assertion equally unreadable. The catalog id stays on
        // the button inside (scenario.CardTestId) — that is what QA taps.
        var panel = SampleUi.Panel("scenarios-" + scenario.Id + "-card-panel", parent, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(panel, 0f, new RectOffset(), false);
        var id = scenario.Id;

        var card = SampleUi.Panel(scenario.CardTestId, panel, OctopusSampleBranding.Clear, HeadRadiusDp, p.Border);
        var fill = card.GetComponent<Image>();
        fill.raycastTarget = true;
        SampleUi.VerticalStack(card, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs),
            SampleUi.Padding(OctopusSampleBranding.SpaceLg, OctopusSampleBranding.SpaceMd), false);
        card.gameObject.AddComponent<LayoutElement>().minHeight = OctopusSampleBranding.MinTouchUnits;
        card.gameObject.AddComponent<SampleUiTouchTarget>();

        var title = SampleUi.Label("Label", card, scenario.DisplayTitle, SampleUi.TextLabel, p.Title, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyles.Bold;
        var subtitle = SampleUi.FlexibleLabel(card, scenario.Subtitle, SampleUi.TextCaption, p.Muted);
        SampleUiLineClamp.Apply(subtitle, 2);

        var holder = SampleUi.Panel("ApiRow", card, OctopusSampleBranding.Clear);
        var rowLayout = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        var chip = SampleUi.Panel(scenario.CardTestId + "-api", holder, p.ChipFill, 6f, p.ChipFill, 0f);
        chip.GetComponent<Image>().raycastTarget = false;
        var chipStack = SampleUi.VerticalStack(chip, 0f, SampleUi.Padding(OctopusSampleBranding.SpaceSm, 3f), false);
        chipStack.childForceExpandWidth = false;
        var symbol = SampleUi.Label("Line", chip, scenario.ReferenceApi, SampleUi.TextCaption, p.ChipInk, TextAnchor.MiddleLeft);
        symbol.font = SampleUiFonts.Mono;
        symbol.textWrappingMode = TextWrappingModes.NoWrap;
        // At most the row's width, then an ellipsis: the row lays the chip out at its text width
        // and shrinks it to fit, since a label's minimum width is zero.
        symbol.overflowMode = TextOverflowModes.Ellipsis;
        symbol.gameObject.AddComponent<LayoutElement>().minWidth = 0f;
        var chipSize = chip.gameObject.AddComponent<LayoutElement>();
        chipSize.minWidth = 0f;
        chipSize.flexibleWidth = 0f;

        var button = card.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = fill;
        button.onClick.AddListener(() => OpenScenario(id));
        var stroke = card.Find(SampleUi.StrokeName);
        button.Configure(fill, stroke != null ? stroke.GetComponent<Image>() : null, title,
            OctopusSampleBranding.Clear, p.Title, p.Border, HeadRadiusDp, false, subtitle);
    }
}
