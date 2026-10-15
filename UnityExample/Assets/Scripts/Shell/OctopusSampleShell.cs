using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>The three sample destinations, matching the Android shell.</summary>
public enum OctopusSampleTab
{
    Scenarios,
    Community,
    Settings
}

/// <summary>
/// Code-built uGUI shell, opening on Scenarios. Each tab retains its content and detail pages
/// while another tab is selected; only a theme change rebuilds the content from saved view state.
/// The generated scene owns the camera, EventSystem and this component.
/// </summary>
public class OctopusSampleShell : MonoBehaviour, IOctopusSampleQaNavigation
{
    /// <summary>The scene this shell is the entry point of. Build index 0.</summary>
    public const string SceneName = "OctopusSample";

    /// <summary>The legacy menu scene this shell replaced as the entry point.</summary>
    public const string LegacyMenuScene = "MainMenu";

    /// <summary>
    /// The shell owns the scene, so it draws under every overlay the sample opens over it — the
    /// scenario list at 1000, a scenario screen at 1100.
    /// </summary>
    private const int SortingOrder = 0;

    /// <summary>App bar height: 64dp, the same figure the other samples use.</summary>
    private const float AppBarHeight = OctopusSampleBranding.AppBarUnits;

    /// <summary>
    /// Tab bar height: Android's 80dp navigation bar — a 32dp indicator pill 12dp from the top, the
    /// label 4dp under it, 16dp of bottom padding.
    /// </summary>
    public const float TabBarHeight = 240f;

    /// <summary>
    /// How far above the bottom of the screen a floating overlay has to sit to clear the tab bar,
    /// in the canvas units <see cref="SampleUi"/> lays out in.
    ///
    /// Published because the debug console's entry button lives on its own always-on-top canvas and
    /// so cannot see this layout: it sat squarely over the Home tab, hiding the label and leaving
    /// only a sliver of the tab tappable. That is the worst shape of broken — the reader concludes
    /// the app is unresponsive rather than that something is in the way.
    ///
    /// The tab bar is laid out inside the safe area, so the gesture bar is *under* it and its
    /// height has to be added on top; it is in pixels, hence the conversion.
    /// </summary>
    public static float BottomOverlayInsetUnits()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return TabBarHeight;
        var unitsPerPixel = SampleUi.ReferenceWidthFor(SampleUi.ScreenDpWidth()) / Screen.width;
        return TabBarHeight + SampleUiSafeArea.ScreenSafeArea().yMin * unitsPerPixel;
    }

    private static readonly OctopusSampleTab[] Tabs =
    {
        OctopusSampleTab.Scenarios,
        OctopusSampleTab.Community,
        OctopusSampleTab.Settings
    };

    /// <summary>
    /// The name of the transitional route back to the legacy menu scene. Not a catalogue id: the
    /// shared catalogue describes the sample the series is building, not the scenes it is replacing.
    /// </summary>
    public const string LegacyScenesId = "shell-legacy-scenes";

    private OctopusSampleTab _selected = OctopusSampleTab.Scenarios;
    private bool _built;
    private bool _listening;
    private bool _rebuildRequested;
    private RectTransform _content;
    private GameObject _root;
    private OctopusSampleTab _renderedTab;
    private readonly Dictionary<OctopusSampleTab, float> _scrollPositions = new Dictionary<OctopusSampleTab, float>();
    private readonly Dictionary<OctopusSampleTab, RectTransform> _tabContents = new Dictionary<OctopusSampleTab, RectTransform>();
    private bool _themeDirty;
    private OctopusSampleCommunityView.ViewState _communityState;
    private OctopusSampleSettingsView.ViewState _settingsState;
    private OctopusScenariosListView.ViewState _listState;
    private ScenarioSection? _pendingSection;
    private RectTransform _appBar;
    private RectTransform _topBleed, _bottomBleed;
    private RectTransform _debugEntry;
    private Transform _debugEntryParent;
    private int _debugBaseSize;
    private float _debugBaseLineHeight;
    private Vector2 _debugAnchorMin, _debugAnchorMax, _debugPivot, _debugSize, _debugPosition;
    private RectOffset _debugEntryPadding;
    private float _debugMinWidth, _debugPreferredWidth, _debugFlexibleWidth;
    private float _debugMinHeight, _debugPreferredHeight, _debugFlexibleHeight;
    private int _debugLayoutPriority;

    public OctopusSampleLocation Location { get; } = new OctopusSampleLocation();

    /// <summary>The tab currently shown.</summary>
    public OctopusSampleTab Selected
    {
        get { return _selected; }
    }

    /// <summary>
    /// Creates the shell over the current scene and builds it immediately. Used by the tests, and by
    /// any caller that needs the shell to exist fully formed the moment the call returns — in
    /// EditMode, `Start` never runs.
    /// </summary>
    public static OctopusSampleShell Create()
    {
        var existing = FindAnyObjectByType<OctopusSampleShell>();
        if (existing != null) return existing;

        var host = new GameObject("OctopusSampleShell");
        var shell = host.AddComponent<OctopusSampleShell>();
        shell.Build();
        return shell;
    }

    /// <summary>
    /// Adds a floating button that leaves the legacy menu for this shell, on its own canvas above
    /// the menu's. Called from `MainMenu.Start` rather than serialized into the scene.
    ///
    /// The transitional bridge out of `MainMenu.unity` and the eight `*Example` scenes (issue
    /// #100): each of those is deleted in the PR that rebuilds its scenario, and this button goes
    /// with the last of them. Its name is deliberately not a catalogue id — `scenarios-tab` names
    /// exactly one object, the tab, and two objects answering to it would make the QA pipeline's
    /// addressing ambiguous.
    /// </summary>
    public static void InstallLegacyMenuEntry()
    {
        if (GameObject.Find("OctopusSampleShellEntry") != null) return;

        var host = new GameObject("OctopusSampleShellEntry");
        // 900: over the menu scene's own canvas. The cost of SampleUi's 1080x1920 reference, shared
        // by every code-built screen here, is that this scales on a slightly different curve from
        // MainMenu.unity's own 800x600 buttons beside it.
        SampleUi.OverlayCanvas(host, 900);

        var safe = SampleUi.SafeArea("SafeArea", host.transform);
        var button = SampleUi.Button("sample-shell-entry", safe, "Sample shell",
                                     OctopusSampleLegacyBack.ReturnToShell);
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 0f);
        button.sizeDelta = new Vector2(380f, OctopusSampleBranding.MinTouchUnits);
        button.anchoredPosition = new Vector2(-40f, 40f);
        OctopusSampleLegacyBack.Install();
    }

    /// <summary>
    /// Set on the way back from the legacy scenes: the next shell start lands on Settings → About,
    /// where "Legacy examples" opened them.
    /// </summary>
    public static bool OpenAboutOnStart;

    /// <summary>The catalogue id the QA pipeline taps for <paramref name="tab"/>.</summary>
    public static string TestId(OctopusSampleTab tab)
    {
        switch (tab)
        {
            case OctopusSampleTab.Scenarios: return "scenarios-tab";
            case OctopusSampleTab.Community: return "community-tab";
            default: return "settings-tab";
        }
    }

    /// <summary>The app bar title for <paramref name="tab"/>.</summary>
    public static string Title(OctopusSampleTab tab)
    {
        switch (tab)
        {
            case OctopusSampleTab.Scenarios: return "Scenarios";
            case OctopusSampleTab.Community: return "Community";
            default: return "Settings";
        }
    }

    /// <summary>
    /// Selects <paramref name="tab"/> and rebuilds the shell around it.
    ///
    /// At runtime the rebuild is deferred by one frame, because the caller is usually the tab
    /// button's own click handler: destroying a <see cref="Button"/> from inside its handler leaves
    /// uGUI holding a destroyed selectable for the pointer-up that follows. In the editor — where
    /// the tests run and no frame ever elapses — it happens immediately instead.
    /// </summary>
    public void Select(OctopusSampleTab tab)
    {
        if (_selected == OctopusSampleTab.Scenarios && tab != _selected)
        {
            var list = _content == null ? null : _content.GetComponent<OctopusScenariosListView>();
            if (list != null) list.ResetForTabSwitch();
            _listState = null;
            _scrollPositions[OctopusSampleTab.Scenarios] = 1f;
        }
        SampleUiDetailPage.HideOtherTabs(tab);
        _selected = tab;
        Location.OpenTab(tab);
        OctopusSampleQaLaunch.Log("tab=" + tab.ToString().ToLowerInvariant());
        if (!_built) return;
        if (Application.isPlaying) _rebuildRequested = true;
        else Rebuild();
    }

    /// <summary>
    /// Opens the Scenarios tab on <paramref name="section"/>: search cleared, the section expanded
    /// and scrolled to the top of the list. What a scenario's feature chip links to — landing on
    /// the list wherever it was last scrolled, possibly with the section collapsed or filtered out,
    /// left the reader hunting for the switch the chip had just named.
    /// </summary>
    public void ShowScenarioSection(ScenarioSection section)
    {
        SampleUiDetailPage.CloseForNavigation(OctopusSampleTab.Scenarios);
        _pendingSection = section;
        Select(OctopusSampleTab.Scenarios);
    }

    /// <summary>
    /// Opens the Scenarios tab on the section of scenario <paramref name="id"/>, then that
    /// scenario's screen over it, so Back from the screen lands next to the row it came from.
    /// Null for an id with no pilot.
    /// </summary>
    public OctopusScenarioScreenView OpenScenarioScreen(string id)
    {
        var pilot = OctopusScenarioPilots.Create(id);
        if (pilot == null)
        {
            Select(OctopusSampleTab.Scenarios);
            return null;
        }
        ShowScenarioSection(OctopusScenarioSections.SectionOf(id));
        // Restore synchronously before lookup: the shell chrome is deferred in Play mode, but
        // a suspended scenario must participate in the same replacement decision as a visible one.
        SampleUiDetailPage.ShowTab(OctopusSampleTab.Scenarios);
        // Open() hands back whatever screen is already up; a link names one scenario, so another
        // one left open would answer it with the wrong screen.
        var open = FindAnyObjectByType<OctopusScenarioScreenView>();
        if (open != null && open.ScenarioId != id) open.Dismiss();
        return OctopusScenarioScreenView.Open(pilot);
    }

    void IOctopusSampleQaNavigation.SelectTab(string id)
    {
        Select((OctopusSampleTab)System.Enum.Parse(typeof(OctopusSampleTab), id, true));
    }

    bool IOctopusSampleQaNavigation.IsTabReady(string id)
    {
        return _built && !_rebuildRequested && _renderedTab.ToString().ToLowerInvariant() == id;
    }

    IOctopusSampleQaScenario IOctopusSampleQaNavigation.OpenScenario(string id)
    {
        var list = _content.GetComponent<OctopusScenariosListView>();
        return list == null ? null : list.OpenScenario(id);
    }

    bool IOctopusSampleQaNavigation.OpenDestination(string id)
    {
        if (id == OctopusSampleQaDestinations.Config) { OctopusSampleConfigView.Open(); return true; }
        if (id == OctopusSampleQaDestinations.Account) { OctopusSampleAccountView.Open(); return true; }
        if (id != OctopusSampleQaDestinations.Arcade) return false;
        OctopusReefRunView.Open();
        return true;
    }

    private void Start()
    {
        if (GetComponent<OctopusSampleSystemBars>() == null) gameObject.AddComponent<OctopusSampleSystemBars>();
        OctopusSamplePushTokenSource.EnsureExists();
        // Android player only; a no-op in the Editor and on iOS. Shows nothing unless Play offers
        // an update this tester has not dismissed.
        OctopusSampleAppUpdate.EnsureStarted();
        if (!_built) Build();
        RestoreConfiguration();
        if (OpenAboutOnStart)
        {
            OpenAboutOnStart = false;
            Select(OctopusSampleTab.Settings);
            OctopusSampleAboutView.Open();
        }
        StartCoroutine(OctopusSampleQaLaunch.ShellReady(this));
    }

    public void RestoreConfiguration()
    {
        OctopusScenarioSdk.RestoreInitializedSdk();
        UpdateStartupNotice();

    }

    private void LateUpdate()
    {
        SampleUi.ResizeBleed(_topBleed, true);
        SampleUi.ResizeBleed(_bottomBleed, false);
        // SafeArea updates its anchors in place, retaining search focus while the keyboard opens.

        if (!_rebuildRequested) return;
        _rebuildRequested = false;
        Rebuild();
    }

    private void OnEnable()
    {
        Listen();
        AttachDebugEntry();
        if (_root != null) ClaimPageStatusGround();
    }

    private void OnDisable() { Shutdown(); }

    public void Shutdown()
    {
        foreach (var content in _tabContents.Values) SetContentActive(content, false);
        SampleUiDetailPage.CloseAll(true);
        ReleaseDebugEntry();
        StopListening();
        ReleasePageStatusGround();
    }

    private void OnDestroy()
    {
        ReleasePageStatusGround();
        ReleaseDebugEntry();
        // Edit mode gets no OnDisable, and a handler left on the static event would fire on a
        // destroyed shell in the next test.
        StopListening();
    }

    /// <summary>
    /// Idempotent, and called from <see cref="Build"/> as well as from <see cref="OnEnable"/>:
    /// Unity does not send OnEnable to a component without <c>[ExecuteAlways]</c> outside play mode,
    /// so a shell created by the EditMode suite would otherwise never hear the theme change it is
    /// there to assert on. The guard is what keeps the two entry points from subscribing twice and
    /// rebuilding the shell twice per toggle at runtime.
    /// </summary>
    private void Listen()
    {
        if (_listening) return;
        OctopusSampleBranding.ThemeChanged += OnThemeChanged;
        OctopusSampleState.Changed += UpdateStartupNotice;
        OctopusSampleAppUpdate.Changed += UpdateStartupNotice;
        SampleUi.DebugEntryChanged += AttachDebugEntry;
        SampleUi.DebugEntryClaimed += OnDebugEntryClaimed;
        _listening = true;
    }

    private void StopListening()
    {
        if (!_listening) return;
        OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
        OctopusSampleState.Changed -= UpdateStartupNotice;
        OctopusSampleAppUpdate.Changed -= UpdateStartupNotice;
        SampleUi.DebugEntryChanged -= AttachDebugEntry;
        SampleUi.DebugEntryClaimed -= OnDebugEntryClaimed;
        _listening = false;
    }

    private void UpdateStartupNotice()
    {
        if (this == null) { StopListening(); return; }
        RectTransform host;
        if (!_tabContents.TryGetValue(OctopusSampleTab.Scenarios, out host)) return;
        var scroll = host.GetComponentInChildren<ScrollRect>(true);
        if (scroll == null) return;
        RemoveNotice(scroll.content, UpdateBannerId);
        RemoveNotice(scroll.content, "config-startup-state");
        var update = OctopusSampleAppUpdate.Status;
        if (update.ShouldAnnounce(OctopusSampleAppUpdate.DismissedVersionCode))
        {
            var banner = SampleUi.Card(UpdateBannerId, scroll.content);
            banner.SetSiblingIndex(0);
            SampleUi.FlexibleLabel(banner, update.Text, SampleUi.TextBody, SampleUi.TitleColor).richText = false;
            SampleUi.Button(UpdateBannerId + "-action", banner, update.ActionText, SampleUiButtonVariant.Primary,
                OctopusSampleAppUpdate.StartUpdate);
            SampleUi.Button(UpdateBannerId + "-dismiss", banner, "Later", SampleUiButtonVariant.Tertiary,
                OctopusSampleAppUpdate.DismissAnnouncement);
        }
        var reason = OctopusScenarioSdk.StartupFailureReason;
        if (string.IsNullOrEmpty(reason)) return;
        var card = SampleUi.Card("config-startup-state", scroll.content);
        card.SetSiblingIndex(0);
        SampleUi.FlexibleLabel(card, reason, SampleUi.TextBody, SampleUi.Attention).richText = false;
        SampleUi.Button("config-startup-settings", card, "Open Configuration", SampleUiButtonVariant.Secondary,
            () => { Select(OctopusSampleTab.Settings); OctopusSampleConfigView.Open(); });
    }

    /// <summary>
    /// The launch announcement of a Play update, above the scenarios. Only an installable update
    /// whose versionCode the tester has not dismissed shows it; every other answer stays silent.
    /// </summary>
    public const string UpdateBannerId = "sample-update-banner";

    private static void RemoveNotice(Transform content, string id)
    {
        var old = content.Find(id);
        if (old == null) return;
        old.gameObject.SetActive(false);
        old.SetParent(null, false);
        if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
    }

    private void OnThemeChanged()
    {
        // A destroyed shell can still be holding this handler: the theme event is static, and Unity
        // sends no OnDestroy to a plain MonoBehaviour outside play mode. Drop it here rather than
        // reach for a destroyed transform — the same protection a scene unload needs at runtime.
        if (this == null)
        {
            StopListening();
            return;
        }

        _themeDirty = true;
        // The same deferral as Select keeps a theme update out of a live click handler.
        if (Application.isPlaying) _rebuildRequested = true;
        else if (_built) Rebuild();
    }

    private void Build()
    {
        _built = true;
        Location.OpenTab(_selected);
        Listen();
        OctopusSampleBackHandler.Attach(this);
        SampleUi.OverlayCanvas(gameObject, SortingOrder);

        // The scene supplies the EventSystem, as everywhere else in this sample: the project is
        // Input-System-only (ProjectSettings activeInputHandler: 1), so the legacy
        // StandaloneInputModule a hand-rolled EventSystem would need throws every frame.
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[Octopus] No EventSystem in this scene — the shell will render but " +
                             "not respond to taps.");
        }

        Rebuild();
    }

    /// <summary>
    /// Tears the shell down and builds it again for the current tab and theme. Everything below the
    /// root is disposable; the component itself and its canvas survive, so the sorting order and the
    /// scene reference stay put.
    /// </summary>
    private void Rebuild()
    {
        CaptureContentState();
        // Consumed here, after the capture: the list on screen is usually the one being replaced,
        // and its captured state would otherwise overwrite the section the caller asked for.
        var focus = _pendingSection;
        _pendingSection = null;
        if (_selected != OctopusSampleTab.Scenarios) focus = null;
        if (focus.HasValue)
        {
            if (_listState == null) _listState = new OctopusScenariosListView.ViewState();
            _listState.Query = "";
            _listState.Open[focus.Value] = true;
        }
        ReleaseDebugEntry();
        foreach (var pair in _tabContents)
        {
            var content = pair.Value;
            if (_themeDirty)
            {
                var community = content.GetComponentInChildren<OctopusSampleCommunityView>(true);
                if (community != null) _communityState = community.CaptureState();
                var settings = content.GetComponent<OctopusSampleSettingsView>();
                if (settings != null) _settingsState = settings.CaptureState();
                var oldList = content.GetComponent<OctopusScenariosListView>();
                if (oldList != null) _listState = oldList.CaptureState();
                var oldScroll = content.GetComponentInChildren<ScrollRect>(true);
                if (oldScroll != null) _scrollPositions[pair.Key] = oldScroll.verticalNormalizedPosition;
                SetContentActive(content, false);
                if (Application.isPlaying) Destroy(content.gameObject); else DestroyImmediate(content.gameObject);
            }
            else
            {
                content.SetParent(transform, false);
                SetContentActive(content, false);
            }
        }
        if (_themeDirty) _tabContents.Clear();
        _themeDirty = false;
        if (_root != null)
        {
            // Deactivated before the destroy, not after: at runtime Destroy only takes effect at the
            // end of the frame, and two shells laid out on top of each other for that frame is a
            // flicker a reviewer would file as a bug.
            _root.SetActive(false);
            if (Application.isPlaying) Destroy(_root);
            else DestroyImmediate(_root);
        }

        var root = SampleUi.Panel("Root", transform, SampleUi.Background);
        SampleUi.Stretch(root, Vector2.zero, Vector2.one);
        _root = root.gameObject;
        // No halo on the Community tab: the SDK's view owns that page, as on the other samples.
        SampleUi.Halo(root).GetComponent<SampleUiHalo>().Hidden = _selected == OctopusSampleTab.Community;

        // The page ground bleeds to the physical edges; the bars' own colours continue into the
        // insets, so a cutout or a gesture bar sits on navy and on the tab bar's surface rather than
        // on a strip of page colour. Only the interactive layout is inset.
        _topBleed = SampleUi.BuildBleed(root, "TopBleed", HeaderFill, true);
        if (HeaderOnPage) SampleUiHalo.PaintPage(_topBleed.GetComponent<Image>());
        ClaimPageStatusGround();
        _bottomBleed = SampleUi.BuildBleed(root, "BottomBleed", OctopusSampleBranding.Palette.TabBar, false);

        var safe = BuildSafeArea(root);

        BuildAppBar(safe);
        BuildTabBar(safe);
        foreach (var content in _tabContents.Values) content.SetParent(safe, false);
        BuildContent(safe);
        UpdateStartupNotice();
        _renderedTab = _selected;
        Canvas.ForceUpdateCanvases();
        var scroll = _content.GetComponentInChildren<ScrollRect>();
        var list = _content.GetComponent<OctopusScenariosListView>();
        float position;
        if (focus.HasValue && list != null)
        {
            list.SetQuery("");
            if (!list.IsSectionOpen(focus.Value)) list.ToggleSection(focus.Value);
            list.ScrollTo(focus.Value);
        }
        else if (scroll != null && _scrollPositions.TryGetValue(_selected, out position))
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = position;
        }
        AttachDebugEntry();
        SampleUiDetailPage.ShowTab(_selected);
    }

    private void CaptureContentState()
    {
        if (_content == null) return;
        var scroll = _content.GetComponentInChildren<ScrollRect>();
        if (scroll != null) _scrollPositions[_renderedTab] = scroll.verticalNormalizedPosition;
        var list = _content.GetComponent<OctopusScenariosListView>();
        if (list != null) _listState = list.CaptureState();
    }

    // Adopt the injected control by its existing QA name. No private assembly reference is
    // needed, and removing the Debug folder simply leaves the app bar without this action.
    private void AttachDebugEntry()
    {
        if (this == null)
        {
            StopListening();
            return;
        }
        if (_appBar == null || _debugEntry != null) return;
        var entry = SampleUi.DebugEntry;
        if (entry == null) return;
        // A detail route drawn above the shell holds the entry: a shell rebuild must not pull it
        // back down into the app bar, where the route's own overlay would bury it.
        if (entry.GetComponentInParent<SampleUiDebugEntryHost>() != null) return;
        _debugEntry = entry;
        _debugEntryParent = entry.parent;
        _debugAnchorMin = entry.anchorMin;
        _debugAnchorMax = entry.anchorMax;
        _debugPivot = entry.pivot;
        _debugSize = entry.sizeDelta;
        _debugPosition = entry.anchoredPosition;
        var label = entry.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            var scaled = label.GetComponent<SampleUiScaledText>();
            if (scaled != null)
            {
                _debugBaseSize = scaled.BaseSize;
                _debugBaseLineHeight = scaled.BaseLineHeight;
                scaled.Configure(SampleUi.TextBody, OctopusSampleBranding.Dp(20f));
            }
        }
        entry.SetParent(_appBar, false);
        // The app bar is a HorizontalLayoutGroup that controls its children's size and position, so
        // the placement lives in the LayoutElement; anchors written here would be overwritten.
        var element = entry.GetComponent<LayoutElement>();
        if (element == null) element = entry.gameObject.AddComponent<LayoutElement>();
        _debugMinHeight = element.minHeight;
        _debugPreferredHeight = element.preferredHeight;
        _debugFlexibleHeight = element.flexibleHeight;
        _debugLayoutPriority = element.layoutPriority;
        var group = entry.GetComponent<HorizontalOrVerticalLayoutGroup>();
        _debugEntryPadding = group != null
            ? new RectOffset(group.padding.left, group.padding.right, group.padding.top, group.padding.bottom)
            : null;
        _debugMinWidth = element.minWidth;
        _debugPreferredWidth = element.preferredWidth;
        _debugFlexibleWidth = element.flexibleWidth;
        element.minWidth = element.preferredWidth =
            Mathf.Max(OctopusSampleBranding.Dp(80f), SampleUi.PreferredButtonWidth(entry));
        element.flexibleWidth = 0f;
        SampleUi.AppBarItem(entry);
        var button = entry.GetComponent<SampleUiButton>();
        // Android's app bar draws a bare bug IconButton here — no disc, the glyph in the bar's
        // content colour — not a text pill.
        if (button != null) button.SetIconStyle(HeaderInk);
        SampleUi.SetDebugEntryIcon(entry, true, HeaderInk);
        var badge = _appBar.Find(PlatformBadgeName);
        if (badge != null) entry.SetSiblingIndex(badge.GetSiblingIndex());
    }

    /// <summary>A detail route's app bar is taking the entry over; hand it back to its owner.</summary>
    private void OnDebugEntryClaimed(object claimant)
    {
        if (this == null)
        {
            StopListening();
            return;
        }
        ReleaseDebugEntry();
    }

    private void ReleaseDebugEntry()
    {
        if (_debugEntry == null) return;
        SampleUi.SetDebugEntryIcon(_debugEntry, false);
        var button = _debugEntry.GetComponent<SampleUiButton>();
        if (button != null) button.SetChromeStyle(false);
        var label = _debugEntry.GetComponentInChildren<TMP_Text>();
        var scaled = label != null ? label.GetComponent<SampleUiScaledText>() : null;
        if (scaled != null) scaled.Configure(_debugBaseSize, _debugBaseLineHeight);
        var element = _debugEntry.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.minHeight = _debugMinHeight;
            element.preferredHeight = _debugPreferredHeight;
            element.flexibleHeight = _debugFlexibleHeight;
            element.layoutPriority = _debugLayoutPriority;
            element.minWidth = _debugMinWidth;
            element.preferredWidth = _debugPreferredWidth;
            element.flexibleWidth = _debugFlexibleWidth;
        }
        // The persistent owner may already have gone during unload or application shutdown.
        var group = _debugEntry.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (group != null && _debugEntryPadding != null) group.padding = _debugEntryPadding;
        if (_debugEntryParent == null) { _debugEntry = null; return; }
        _debugEntry.SetParent(_debugEntryParent, false);
        _debugEntry.anchorMin = _debugAnchorMin;
        _debugEntry.anchorMax = _debugAnchorMax;
        _debugEntry.pivot = _debugPivot;
        _debugEntry.sizeDelta = _debugSize;
        _debugEntry.anchoredPosition = _debugPosition;
        _debugEntry = null;
    }

    /// <summary>
    /// The container every interactive element lives in: the screen minus the cutouts, the status
    /// bar and the gesture bar.
    ///
    /// The player is built with `androidRenderOutsideSafeArea: 1`, so without this the app bar's
    /// title sits under the status bar and the tab row under the home indicator — on the emulator
    /// this repo's QA runs on, which has neither, it looks perfectly fine. Anchors rather than
    /// offsets, so the inset is expressed as a fraction of the screen and needs no unit conversion.
    /// </summary>
    private RectTransform BuildSafeArea(RectTransform root)
    {
        var safe = SampleUi.SafeArea("SafeArea", root);
        var layout = safe.GetComponent<SampleUiSafeArea>();
        // Only the content scrolls on short/keyboard viewports; detail pages share these pinned tabs.
        layout.KeepBarsPinned = true;
        layout.Apply(SampleUiSafeArea.ScreenSafeArea(), SampleUiSafeArea.KeyboardArea(), new Vector2(Screen.width, Screen.height));
        return safe;
    }

    // The shell that last wrote the process-wide status-ground flag. Only it may clear the flag: a
    // shell torn down after another one was built would otherwise take the dark status icons away
    // from the shell still on screen.
    private static OctopusSampleShell _pageStatusGroundOwner;

    private void ClaimPageStatusGround()
    {
        _pageStatusGroundOwner = this;
        OctopusSampleSystemBars.PageStatusGround = HeaderOnPage;
    }

    /// <summary>
    /// Clears the flag about this shell's header if this shell set it: left set, it would keep dark
    /// status icons on whatever scene or shell comes next.
    /// </summary>
    private void ReleasePageStatusGround()
    {
        if (!ReferenceEquals(_pageStatusGroundOwner, this)) return;
        _pageStatusGroundOwner = null;
        OctopusSampleSystemBars.PageStatusGround = false;
    }

    /// <summary>
    /// Whether the app bar sits on the page, as Android's `MediumTopAppBar` does on Scenarios (its
    /// container is `bg`): the large title drawn under it then continues the same band, 112dp in
    /// all, rather than a navy bar stacked on a second one.
    /// </summary>
    private bool HeaderOnPage { get { return _selected == OctopusSampleTab.Scenarios; } }

    /// <summary>The app bar's fill: the page on Scenarios, the shared chrome elsewhere.</summary>
    private Color HeaderFill
    {
        get { return HeaderOnPage ? OctopusSampleBranding.Palette.PageHeader : OctopusSampleBranding.Palette.Header; }
    }

    /// <summary>The app bar's content colour, for its icon buttons.</summary>
    internal Color HeaderInk
    {
        get { return HeaderOnPage ? OctopusSampleBranding.Palette.Title : SampleUi.ChromeButtonInk; }
    }

    private void BuildAppBar(RectTransform root)
    {
        var bar = SampleUi.AppBar("AppBar", root, Title(_selected));
        _appBar = bar;
        if (HeaderOnPage)
        {
            SampleUiHalo.PaintPage(bar.GetComponent<Image>());
            // The Scenarios tab draws Android's large title under this bar; the compact one keeps its
            // text and its slot (the badge and Debug stay right-aligned) but is not painted.
            var title = bar.Find("Title");
            var text = title != null ? title.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                text.color = HeaderInk;
                text.alpha = 0f;
            }
        }
        BuildPlatformBadge(bar);

        // Appearance is owned by Settings; ThemeChanged still rebuilds this shell.
    }

    /// <summary>The app bar's round platform badge, Android's `PlatformBadge`.</summary>
    public const string PlatformBadgeName = "PlatformBadge";

    /// <summary>Android's `SampleSize.badgeHeight`: the compact badge is a disc this wide.</summary>
    public const float BadgeDp = 28f;

    /// <summary>
    /// A 28dp disc in the app bar's last slot carrying the platform's mark — Android's compact
    /// `PlatformBadge` (`SampleSize.badgeHeight` square, pill-clipped), which shows the Android robot.
    /// Unity's is a game controller glyph: the sample ships no engine logo. The slot is the disc's own
    /// width, so the bar's 16dp end padding is Android's 4dp action inset plus the badge's 12dp.
    /// </summary>
    private static void BuildPlatformBadge(RectTransform bar)
    {
        var p = OctopusSampleBranding.Palette;
        var slot = SampleUi.Panel(PlatformBadgeName, bar, OctopusSampleBranding.Clear);
        SampleUi.AppBarItem(slot);
        var size = slot.GetComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = OctopusSampleBranding.Dp(BadgeDp);
        size.flexibleWidth = 0f;
        // Pill radius, not 14dp: the 9-slice then always meets in the middle, so the disc stays a
        // circle whatever the sprite's pixel rounding.
        var circle = SampleUi.Panel("Circle", slot, p.Surface, OctopusSampleBranding.ButtonRadius, p.Border);
        var elevation = circle.GetComponent<SampleUiElevation>();
        if (elevation != null)
        {
            if (Application.isPlaying) Destroy(elevation); else DestroyImmediate(elevation);
        }
        circle.GetComponent<Image>().raycastTarget = false;
        circle.anchorMin = circle.anchorMax = circle.pivot = new Vector2(0.5f, 0.5f);
        circle.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(BadgeDp);
        circle.anchoredPosition = Vector2.zero;
        var mark = SampleUi.Panel("Icon", circle, OctopusSampleBranding.Clear);
        var image = mark.GetComponent<Image>();
        SampleUiIcons.Apply(image, "sports_esports", p.PlatformSlot);
        image.raycastTarget = false;
        mark.anchorMin = mark.anchorMax = mark.pivot = new Vector2(0.5f, 0.5f);
        mark.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(16f);
        mark.anchoredPosition = Vector2.zero;
    }

    private void BuildTabBar(RectTransform root)
    {
        var bar = SampleUi.Panel("TabBar", root, OctopusSampleBranding.Palette.TabBar);
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = new Vector2(0f, TabBarHeight);
        bar.anchoredPosition = Vector2.zero;

        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;
        // Android's NavigationBar: one flat surface, the items edge to edge, no boxes between them.
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm);

        foreach (var tab in Tabs) BuildTabItem(bar, tab);
    }

    private void BuildTabItem(RectTransform bar, OctopusSampleTab tab)
    {
        var selected = tab == _selected;
        var palette = OctopusSampleBranding.Palette;

        // GameObject.name IS the catalogue's test id, verbatim — the handle the QA pipeline taps.
        // Flat, as Android's NavigationBar item: no frame, no elevation; the only shape is the
        // 64x32dp indicator pill behind the selected icon, in the accent's indicator container.
        var item = SampleUi.Panel(TestId(tab), bar, palette.TabBar);
        var fill = item.GetComponent<Image>();
        fill.raycastTarget = true;
        var button = item.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = fill;
        var target = tab;
        button.onClick.AddListener(() => Select(target));

        var element = item.gameObject.AddComponent<LayoutElement>();
        element.minHeight = OctopusSampleBranding.MinTouchUnits;
        element.minWidth = OctopusSampleBranding.MinTouchUnits;
        element.preferredWidth = 0f;
        element.flexibleWidth = 1f;

        var indicator = SampleUi.Panel("Indicator", item, selected ? palette.AccentIndicator : OctopusSampleBranding.Clear);
        var pill = indicator.GetComponent<Image>();
        pill.sprite = SampleUiShapes.Rounded(16f);
        pill.type = Image.Type.Sliced;
        pill.raycastTarget = false;
        BuildTabIcon(indicator, tab, selected ? palette.OnAccentIndicator : palette.Muted);

        var label = SampleUi.Label("Label", item, tab.ToString(), SampleUi.TextCaption,
                                   selected ? palette.Title : palette.Muted,
                                   TextAnchor.MiddleCenter, tabLabel: true);
        label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.Normal;
        var stack = SampleUi.VerticalStack(item, OctopusSampleBranding.Dp(4f),
            new RectOffset(0, 0, Mathf.RoundToInt(OctopusSampleBranding.Dp(12f)), Mathf.RoundToInt(OctopusSampleBranding.Dp(16f))), false);
        stack.childAlignment = TextAnchor.UpperCenter;
        stack.childForceExpandWidth = false;
        var iconSize = indicator.gameObject.AddComponent<LayoutElement>();
        iconSize.minWidth = iconSize.preferredWidth = OctopusSampleBranding.Dp(64f);
        iconSize.minHeight = iconSize.preferredHeight = OctopusSampleBranding.Dp(32f);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        button.Configure(fill, null, label, palette.TabBar, selected ? palette.Title : palette.Muted,
            palette.TabBar, 0f);
        item.gameObject.AddComponent<SampleUiTouchTarget>();
    }

    // Compose the three navigation pictograms from cached uGUI shapes, independent of font glyphs.
    private static void BuildTabIcon(RectTransform parent, OctopusSampleTab tab, Color ink)
    {
        var icon = SampleUi.Panel("Icon", parent, OctopusSampleBranding.Clear);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(24f);
        icon.anchoredPosition = Vector2.zero;
        float[,] lines;
        switch (tab)
        {
            case OctopusSampleTab.Scenarios:
                lines = new float[,] { { 9, 3, 15, 3 }, { 10, 3, 10, 10 }, { 10, 10, 4, 20 },
                    { 4, 20, 20, 20 }, { 20, 20, 14, 10 }, { 14, 10, 14, 3 }, { 7, 15, 17, 15 } };
                break;
            case OctopusSampleTab.Community:
                lines = new float[,] { { 4, 4, 20, 4 }, { 20, 4, 20, 16 }, { 20, 16, 9, 16 },
                    { 9, 16, 4, 20 }, { 4, 20, 4, 4 }, { 8, 8, 16, 8 }, { 8, 12, 13, 12 } };
                break;
            default:
                lines = new float[,] { { 12, 2, 12, 5 }, { 12, 19, 12, 22 }, { 2, 12, 5, 12 },
                    { 19, 12, 22, 12 }, { 5, 5, 7, 7 }, { 17, 17, 19, 19 },
                    { 5, 19, 7, 17 }, { 17, 7, 19, 5 } };
                var ring = SampleUi.Panel("Ring", icon, ink);
                ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
                ring.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(10f);
                ring.anchoredPosition = Vector2.zero;
                var image = ring.GetComponent<Image>();
                image.sprite = SampleUiShapes.Rounded(5f, 2f);
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;
                break;
        }
        for (var i = 0; i < lines.GetLength(0); i++)
        {
            var from = new Vector2(lines[i, 0] - 12f, 12f - lines[i, 1]);
            var to = new Vector2(lines[i, 2] - 12f, 12f - lines[i, 3]);
            var segment = SampleUi.Panel(SampleUi.StrokeName, icon, ink, 1f, ink, 0f);
            segment.GetComponent<Image>().raycastTarget = false;
            segment.anchorMin = segment.anchorMax = new Vector2(0.5f, 0.5f);
            segment.sizeDelta = new Vector2(OctopusSampleBranding.Dp((to - from).magnitude),
                OctopusSampleBranding.Dp(2f));
            segment.anchoredPosition = (from + to) * (0.5f * OctopusSampleBranding.UnitsPerDp);
            segment.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }
        string symbol = tab == OctopusSampleTab.Scenarios ? "science"
            : tab == OctopusSampleTab.Community ? "forum" : "settings";
        if (SampleUiIcons.Apply(icon.GetComponent<Image>(), symbol, ink))
        {
            // Retain the existing Ring/Stroke objects and names; only their old line rendering
            // is replaced by the exported symbol on Icon. They remain the missing-asset fallback.
            foreach (var oldGraphic in icon.GetComponentsInChildren<Image>())
                if (oldGraphic.transform != icon) oldGraphic.enabled = false;
        }
    }

    private static void SetContentActive(RectTransform content, bool active)
    {
        if (content == null) return;
        content.gameObject.SetActive(active);
        var community = content.GetComponentInChildren<OctopusSampleCommunityView>(true);
        if (community != null) community.SetTabActive(active);
        var settings = content.GetComponent<OctopusSampleSettingsView>();
        if (settings != null) settings.SetTabActive(active);
        var overview = content.GetComponentInChildren<OctopusSampleHomeView>(true);
        if (overview != null) overview.SetTabActive(active);
        var list = content.GetComponent<OctopusScenariosListView>();
        if (list != null) list.SetTabActive(active);
    }

    private void BuildContent(RectTransform root)
    {
        if (_tabContents.TryGetValue(_selected, out _content))
        {
            SetContentActive(_content, true);
            return;
        }
        _content = SampleUi.Panel("Content", root, OctopusSampleBranding.Clear);
        _tabContents.Add(_selected, _content);
        SampleUi.Stretch(_content, Vector2.zero, Vector2.one);
        _content.offsetMin = new Vector2(0f, TabBarHeight);
        _content.offsetMax = new Vector2(0f, -AppBarHeight);

        switch (_selected)
        {
            case OctopusSampleTab.Scenarios:
                OctopusScenariosListView.BuildInto(_content, _listState);
                break;
            case OctopusSampleTab.Community:
                var community = OctopusSampleCommunityView.BuildInto(_content);
                community.RestoreState(_communityState);
                break;
            default:
                var settings = OctopusSampleSettingsView.BuildInto(_content);
                settings.RestoreState(_settingsState);

                break;
        }
    }

}
