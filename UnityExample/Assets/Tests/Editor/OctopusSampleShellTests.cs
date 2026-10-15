using TMPro;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Three canonical QA tabs, retained content/detail state and shared shell geometry.</summary>
public class OctopusSampleShellTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        DestroySpawned();
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void DeveloperToolsReturnsDebugEntryWhileItsTabIsHidden()
    {
        var owner = new GameObject("Debug owner", typeof(RectTransform));
        _spawned.Add(owner);
        var entry = SampleUi.Button("debug-open-button", owner.transform, "Debug", () => { });
        SampleUi.RegisterDebugEntry(entry);
        var shell = Create();
        shell.Select(OctopusSampleTab.Settings);
        var detail = OctopusSampleDeveloperToolsView.Open(() => 0,
            () => new List<OctopusSampleDeveloperToolsView.LogLine>(),
            () => new List<OctopusSampleDeveloperToolsView.InfoFact>(), () => { });
        _spawned.Add(detail.gameObject);
        Assert.IsTrue(entry.IsChildOf(detail.transform));
        shell.Select(OctopusSampleTab.Scenarios);
        Assert.IsTrue(detail != null && !detail.gameObject.activeSelf);
        Assert.IsTrue(entry != null);
        Assert.IsTrue(entry.IsChildOf(shell.transform));
    }

    [Test]
    public void KeyboardDoesNotTurnPinnedShellBarsIntoScrollableContent()
    {
        var shell = Create();
        var safe = shell.GetComponentInChildren<SampleUiSafeArea>();
        safe.Apply(new Rect(0f, 0f, 1080f, 1920f), new Rect(0f, 320f, 1080f, 1600f),
            new Vector2(1080f, 1920f));
        Canvas.ForceUpdateCanvases();
        Assert.IsTrue(safe.KeepBarsPinned);
        Assert.AreNotEqual("SafeAreaViewport", safe.transform.parent.name);
        Assert.AreSame(safe.transform, Find(shell.transform, "TabBar").parent);
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void DetailRetainsTabsAndReturnsWhenSelectingItsTab(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var shell = Create();
        shell.Select(OctopusSampleTab.Settings);
        var detail = OctopusSampleAppearanceView.Open();
        _spawned.Add(detail.gameObject);
        var safe = detail.GetComponentInChildren<SampleUiSafeArea>();
        Assert.AreEqual(OctopusSampleShell.TabBarHeight, safe.BottomInsetUnits);
        Assert.AreEqual(1, detail.GetComponentsInChildren<SampleUiSafeArea>().Length);
        var tabBar = Find(shell.transform, "TabBar").GetComponent<Image>();
        // Android's `sampleBottomBarColor` (surface2): #E4E8EE light, #22324C dark.
        Assert.AreEqual(theme == OctopusSampleTheme.Light ? OctopusSampleBranding.TintedBackground
            : OctopusSampleBranding.DarkSurface2, tabBar.color);
        if (Screen.width > 0 && Screen.height > 0)
        {
            Canvas.ForceUpdateCanvases();
            var pageCorners = new Vector3[4];
            var tabCorners = new Vector3[4];
            ((RectTransform)safe.transform).GetWorldCorners(pageCorners);
            ((RectTransform)tabBar.transform).GetWorldCorners(tabCorners);
            Assert.That(pageCorners[0].y, Is.EqualTo(tabCorners[1].y).Within(1f));
        }
        OctopusSampleBranding.Theme = theme == OctopusSampleTheme.Light ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
        Assert.IsTrue(detail != null);
        shell.Select(OctopusSampleTab.Scenarios);
        Assert.IsTrue(detail != null && !detail.gameObject.activeSelf);
        shell.Select(OctopusSampleTab.Settings);
        Assert.IsTrue(detail.gameObject.activeSelf);
        detail.Close();
    }

    [Test]
    public void StartsOnScenariosWithNoHomeOrExplorerTab()
    {
        var shell = Create();
        Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected);
        Assert.IsNull(Find(shell.transform, "home-tab"));
        Assert.IsNull(Find(shell.transform, "explorer-tab"));
        shell.Select(OctopusSampleTab.Settings);
        Assert.IsNotNull(Find(shell.transform, "settings-community-block"));
    }

    [Test]
    public void ShutdownToleratesAnAlreadyDestroyedEntryOwner()
    {
        var owner = new GameObject("Entry owner", typeof(RectTransform));
        var entry = SampleUi.Button("debug-open-button", owner.transform, "Debug", () => { });
        SampleUi.RegisterDebugEntry(entry);
        var shell = Create();
        Object.DestroyImmediate(owner);
        Assert.DoesNotThrow(shell.Shutdown);
    }

    [Test]
    public void TheShellExposesTheThreeCanonicalTabIds()
    {
        var shell = Create();

        foreach (var id in new[] { "scenarios-tab", "community-tab", "settings-tab" })
        {
            Assert.IsNotNull(Find(shell.transform, id),
                "No GameObject named '" + id + "' in the shell — the QA pipeline addresses tabs " +
                "by that name.");
        }
    }

    [Test]
    public void TheShellHasNoExplorerTab()
    {
        var shell = Create();

        Assert.IsNull(Find(shell.transform, "explorer-tab"),
            "The shell built an Explorer tab. SDK_STANDARDS §5.1 makes it Android + iOS only, so " +
            "its absence on Unity is the contract, not a gap to fill.");
    }

    [Test]
    public void TabsAppearInTheCanonicalOrder()
    {
        var shell = Create();
        var bar = Find(shell.transform, "TabBar");
        Assert.IsNotNull(bar, "The shell has no TabBar.");

        var ids = new List<string>();
        for (var i = 0; i < bar.childCount; i++) ids.Add(bar.GetChild(i).gameObject.name);

        Assert.AreEqual(new[] { "scenarios-tab", "community-tab", "settings-tab" }, ids,
            "The Android shell opens Scenarios → Community → Settings.");
    }

    [Test]
    public void EveryTabIsSelectableAndKeepsTheThreeIds()
    {
        var shell = Create();

        foreach (OctopusSampleTab tab in System.Enum.GetValues(typeof(OctopusSampleTab)))
        {
            shell.Select(tab);

            Assert.AreEqual(tab, shell.Selected);
            foreach (var id in new[] { "scenarios-tab", "community-tab", "settings-tab" })
            {
                Assert.IsNotNull(Find(shell.transform, id),
                    "Selecting '" + tab + "' lost the tab named '" + id + "'.");
            }
        }
    }

    [Test]
    public void TheScenariosTabHostsACardForEveryScenarioItCanDemonstrate()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        // Sections open collapsed, as on Android; open them all so every card is built.
        var list = shell.GetComponentInChildren<OctopusScenariosListView>();
        foreach (var group in OctopusScenarioSections.Filter("")) list.ToggleSection(group.Section);

        foreach (var scenario in OctopusScenarioCatalog.All)
        {
            var card = Find(shell.transform, scenario.CardTestId);
            if (OctopusScenarioSections.IsListed(scenario))
            {
                Assert.IsNotNull(card,
                    "The Scenarios tab has no card named '" + scenario.CardTestId + "', for a " +
                    "scenario this sample drives.");
            }
            else
            {
                // TOKENS §1: a scenario that is not implemented at all is not listed, and its
                // porting status lives in the versioned catalogue instead. A card here would be a
                // door onto an empty room.
                Assert.IsNull(card,
                    "The Scenarios tab shows '" + scenario.CardTestId + "', which no pilot drives.");
            }
        }
    }

    [Test]
    public void BuildingTheShellMakesNoSdkCall()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        var shell = Create();
        foreach (OctopusSampleTab tab in System.Enum.GetValues(typeof(OctopusSampleTab)))
        {
            shell.Select(tab);
        }

        Assert.IsEmpty(probe.Methods,
            "Building the shell reached the SDK: " + string.Join(", ", probe.Methods) +
            " — navigation is not a user action on the SDK.");
    }

    [Test]
    public void EveryTabHasAnAppBarTitle()
    {
        foreach (OctopusSampleTab tab in System.Enum.GetValues(typeof(OctopusSampleTab)))
        {
            Assert.IsNotEmpty(OctopusSampleShell.Title(tab), "Tab '" + tab + "' has no title.");
            Assert.IsNotEmpty(OctopusSampleShell.TestId(tab), "Tab '" + tab + "' has no test id.");
        }
    }

    [Test]
    public void SwitchingThemeRebuildsTheShellWithTheOtherPalette()
    {
        var shell = Create();
        Assert.AreEqual(OctopusSampleTheme.Dark, OctopusSampleBranding.Theme);
        // Identity, not presence: the ids below survive a shell that never rebuilt at all, which is
        // exactly what this test claimed to catch and did not.
        var barBefore = Find(shell.transform, "AppBar");

        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;

        Assert.AreEqual(OctopusSampleBranding.LightAccentText, OctopusSampleBranding.Palette.Accent);
        var barAfter = Find(shell.transform, "AppBar");
        Assert.IsNotNull(barAfter, "The theme switch left no app bar.");
        Assert.AreNotSame(barBefore, barAfter,
            "The app bar is the same object as before the theme change — nothing was rebuilt, and " +
            "every colour on screen is still the dark palette's.");
        foreach (var id in new[] { "settings-tab" })
        {
            Assert.IsNotNull(Find(shell.transform, id),
                "The theme switch lost '" + id + "' — the shell did not rebuild.");
        }
    }

    [Test]
    public void TheSettingsTabKeepsTheLegacyScenesReachable()
    {
        // Three legacy scenes — ForcedLogin, SSO, LanguageOverride — are reachable only
        // from `MainMenu.unity`, and this shell took build index 0 away from it. Without a route
        // back they still build, still pass every gate, and cannot be opened by anyone.
        var shell = Create();
        shell.Select(OctopusSampleTab.Settings);

        var about = OctopusSampleAboutView.Open();
        Assert.IsNotNull(Find(about.transform, OctopusSampleShell.LegacyScenesId),
            "Nothing in About leads back to '" + OctopusSampleShell.LegacyMenuScene + "'.");
        about.Close();
    }

    [Test]
    public void TheInteractiveLayoutIsHeldInsideTheSafeArea()
    {
        // The player renders outside the safe area (ProjectSettings androidRenderOutsideSafeArea:
        // 1), so the bars have to be inset by hand. On the emulator QA runs on there is no cutout
        // and no gesture bar, which is why this is a test and not a screenshot.
        var shell = Create();

        var safe = Find(shell.transform, "SafeArea");
        Assert.IsNotNull(safe, "The shell has no safe-area container.");
        foreach (var id in new[] { "AppBar", "TabBar", "Content" })
        {
            var child = Find(shell.transform, id);
            Assert.IsNotNull(child, "The shell has no '" + id + "'.");
            Assert.AreSame(safe, child.parent,
                "'" + id + "' is not inside the safe area: it can sit under a cutout or the " +
                "gesture bar.");
        }
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void NavigationIconsKeepTheirSizeAndDoNotStealTabHits(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var shell = Create();
        foreach (OctopusSampleTab tab in System.Enum.GetValues(typeof(OctopusSampleTab)))
        {
            var item = Find(shell.transform, OctopusSampleShell.TestId(tab));
            var icon = (RectTransform)item.Find("Indicator/Icon");
            Assert.IsNotNull(icon);
            Assert.AreEqual(Vector2.one * OctopusSampleBranding.Dp(24f), icon.sizeDelta);
            foreach (var graphic in icon.GetComponentsInChildren<Image>()) Assert.IsFalse(graphic.raycastTarget);
            Assert.GreaterOrEqual(item.GetComponent<LayoutElement>().minHeight, OctopusSampleBranding.MinTouchUnits);
        }
    }

    [Test]
    public void AppearanceDoesNotOccupyTheAppBar()
    {
        var shell = Create();
        Assert.IsNull(Find(shell.transform, "shell-theme-toggle"), "D1 moves Appearance to Settings.");
    }

    [Test]
    public void InjectedDebugEntryMovesToAppBarAndReturnsToItsOwner()
    {
        var host = new GameObject("OctopusDebugConsoleEntry");
        _spawned.Add(host);
        var owner = SampleUi.SafeArea("SafeArea", host.transform);
        var entry = SampleUi.Button("debug-open-button", owner, "Debug", () => { });
        SampleUi.RegisterDebugEntry(entry);
        var originalPosition = entry.anchoredPosition;
        var shell = Create();
        Assert.AreSame(Find(shell.transform, "AppBar"), entry.parent);
        Assert.AreEqual(3, Find(shell.transform, "TabBar").childCount);
        shell.Select(OctopusSampleTab.Scenarios);
        Assert.AreSame(Find(shell.transform, "AppBar"), entry.parent);
        Assert.LessOrEqual(entry.rect.height, ((RectTransform)entry.parent).rect.height);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        Assert.IsTrue(entry != null, "Rebuilding the app bar destroyed the injected control.");
        Assert.AreSame(Find(shell.transform, "AppBar"), entry.parent);

        // This plain MonoBehaviour receives OnDisable in a player, but not when DestroyImmediate
        // removes an EditMode fixture that has never run. Exercise its real shutdown callback
        // explicitly, then verify the owner and the control survive destruction of the shell.
        shell.Shutdown();
        Assert.AreSame(owner, entry.parent);
        Object.DestroyImmediate(shell.gameObject);
        Assert.IsTrue(entry != null, "Shutting down the shell destroyed the owner's control.");
        Assert.AreSame(owner, entry.parent);
        Assert.AreEqual(originalPosition, entry.anchoredPosition);
    }

    [Test]
    public void OnlyTheShellThatSetThePageStatusGroundClearsIt()
    {
        var older = Create();
        older.Select(OctopusSampleTab.Scenarios);
        // Create() returns the active shell: set the first one aside, as a scene switch does while
        // the next scene's shell is built before the old one is destroyed.
        older.gameObject.SetActive(false);
        var newer = Create();
        Assert.AreNotSame(older, newer);
        newer.Select(OctopusSampleTab.Scenarios);
        Assert.IsTrue(OctopusSampleSystemBars.PageStatusGround);

        older.Shutdown();
        Assert.IsTrue(OctopusSampleSystemBars.PageStatusGround,
            "A shell torn down after another was built cleared the flag the newer one set.");
        newer.Shutdown();
        Assert.IsFalse(OctopusSampleSystemBars.PageStatusGround, "The owning shell left the flag set.");
    }

    [Test]
    public void ListResetsOnTabSwitchAndRetainsSearchOnThemeChanges()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();
        view.ToggleSection(ScenarioSection.SignIn);
        view.SetQuery("locale");
        shell.Select(OctopusSampleTab.Community);
        shell.Select(OctopusSampleTab.Scenarios);
        view = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.AreEqual("", view.Query);
        Assert.IsFalse(view.IsSectionOpen(ScenarioSection.SignIn));
        var restoredField = view.GetComponentInChildren<TMP_InputField>();
        Assert.AreEqual("", restoredField.text);
        Assert.IsTrue(restoredField.placeholder.enabled);
        view.SetQuery("locale");
        Canvas.ForceUpdateCanvases();
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        view = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.AreEqual("locale", view.Query);
        view.SetQuery("");
        Assert.IsTrue(view.GetComponentInChildren<TMP_InputField>().placeholder.enabled);
        Assert.IsFalse(view.IsSectionOpen(ScenarioSection.SignIn));

        foreach (var section in OctopusScenarioSections.All)
            if (!view.IsSectionOpen(section)) view.ToggleSection(section);
        Canvas.ForceUpdateCanvases();
        var scroll = view.GetComponentInChildren<ScrollRect>();
        scroll.verticalNormalizedPosition = 0.4f;
        Canvas.ForceUpdateCanvases();
        Assert.Greater(scroll.content.anchoredPosition.y, 1f, "The reset must start from scrolled content.");
        shell.Select(OctopusSampleTab.Community);
        shell.Select(OctopusSampleTab.Scenarios);
        scroll = shell.GetComponentInChildren<OctopusScenariosListView>().GetComponentInChildren<ScrollRect>();
        Canvas.ForceUpdateCanvases();
        // ScrollRect can report 0 even at the top when the collapsed content fits its viewport.
        Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.5f);
        Assert.IsFalse(shell.GetComponentInChildren<OctopusScenariosListView>().IsSectionOpen(ScenarioSection.SignIn));
    }

    [Test]
    public void CommunityDraftAndDisclosuresSurviveOtherTabsAndThemeChanges()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Community);
        var view = shell.GetComponentInChildren<OctopusSampleCommunityView>();
        view.SetGroupId("sample-group");
        view.SetPostId("sample-post");
        view.SetPrefillText("Draft body");
        view.ToggleComposer();
        view.ToggleDestinations();
        var before = view.CaptureState();
        shell.Select(OctopusSampleTab.Settings);
        shell.Select(OctopusSampleTab.Community);
        Assert.AreSame(view, shell.GetComponentInChildren<OctopusSampleCommunityView>());
        shell.Select(OctopusSampleTab.Settings);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        shell.Select(OctopusSampleTab.Community);
        var after = shell.GetComponentInChildren<OctopusSampleCommunityView>().CaptureState();
        Assert.AreEqual(before.Group, after.Group);
        Assert.AreEqual(before.Post, after.Post);
        Assert.AreEqual(before.Prefill, after.Prefill);
        Assert.AreEqual(before.Result, after.Result);
        Assert.AreEqual(before.ComposerOpen, after.ComposerOpen);
        Assert.AreEqual(before.DestinationsOpen, after.DestinationsOpen);
    }

    [TestCase("appearance")]
    [TestCase("about")]
    [TestCase("account")]
    [TestCase("developer-tools")]
    [TestCase("scenario")]
    public void HiddenDetailsFollowThemeWithoutLosingTheirTabOrDebugEntry(string kind)
    {
        var owner = new GameObject("Debug owner", typeof(RectTransform));
        _spawned.Add(owner);
        var entry = SampleUi.Button("debug-open-button", owner.transform, "Debug", () => { });
        SampleUi.RegisterDebugEntry(entry);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        var shell = Create();
        var tab = kind == "scenario" ? OctopusSampleTab.Scenarios : OctopusSampleTab.Settings;
        shell.Select(tab);
        GameObject detail;
        if (kind == "appearance") detail = OctopusSampleAppearanceView.Open().gameObject;
        else if (kind == "about") detail = OctopusSampleAboutView.Open().gameObject;
        else if (kind == "account") detail = OctopusSampleAccountView.Open().gameObject;
        else if (kind == "scenario") detail = shell.OpenScenarioScreen("customEvents").gameObject;
        else
        {
            var tools = OctopusSampleDeveloperToolsView.Open(() => 0,
                () => new List<OctopusSampleDeveloperToolsView.LogLine>(),
                () => new List<OctopusSampleDeveloperToolsView.InfoFact>(), () => { });
            tools.ShowInfo();
            detail = tools.gameObject;
        }
        _spawned.Add(detail);
        shell.Select(tab == OctopusSampleTab.Settings ? OctopusSampleTab.Scenarios : OctopusSampleTab.Settings);
        Assert.IsFalse(detail.activeSelf);
        Assert.DoesNotThrow(() => OctopusSampleBranding.Theme = OctopusSampleTheme.Light);
        Assert.IsFalse(detail.activeSelf);
        Assert.IsTrue(entry.IsChildOf(shell.transform), "A hidden theme rebuild claimed the Debug entry.");
        shell.Select(tab);
        Assert.IsTrue(detail.activeSelf);
        var page = detail.GetComponentInChildren<SampleUiDetailPage>();
        Assert.AreEqual(OctopusSampleBranding.LightBackground, page.GetComponent<Image>().color);
        Assert.IsTrue(entry.IsChildOf(detail.transform));
        shell.Select(tab);
        Assert.IsTrue(entry.IsChildOf(detail.transform), "Reselecting the current tab buried Debug.");
        if (kind == "developer-tools")
            Assert.AreEqual("debug-info-screen", detail.GetComponent<OctopusSampleDeveloperToolsView>().CurrentScreen);
    }

    [Test]
    public void ExplicitSectionLinkRevealsTheListInsteadOfARetainedScenario()
    {
        var shell = Create();
        var detail = shell.OpenScenarioScreen("customEvents");
        _spawned.Add(detail.gameObject);
        shell.Select(OctopusSampleTab.Settings);
        var tools = OctopusSampleDeveloperToolsView.Open(() => 0,
            () => new List<OctopusSampleDeveloperToolsView.LogLine>(),
            () => new List<OctopusSampleDeveloperToolsView.InfoFact>(), () => { });
        _spawned.Add(tools.gameObject);
        tools.OpenPushRegistrationSource();
        Assert.IsTrue(detail == null);
        Assert.IsTrue(tools == null);
        Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected);
        Assert.AreEqual(ScenarioSection.Notifications,
            shell.GetComponentInChildren<OctopusScenariosListView>().FocusedSection);
    }

    [Test]
    public void OpeningALinkReplacesTheRetainedScenarioRatherThanStackingIt()
    {
        var shell = Create();
        var original = shell.OpenScenarioScreen("customEvents");
        _spawned.Add(original.gameObject);
        shell.Select(OctopusSampleTab.Settings);
        var replacement = shell.OpenScenarioScreen("locale");
        _spawned.Add(replacement.gameObject);
        Assert.AreEqual("locale", replacement.ScenarioId);
        Assert.IsTrue(original == null);
        Assert.AreEqual(1, Object.FindObjectsByType<OctopusScenarioScreenView>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length);
    }

    [Test]
    public void ScenarioDetailAndBackStackSurviveTabSwitch()
    {
        var shell = Create();
        var detail = shell.OpenScenarioScreen("customEvents");
        _spawned.Add(detail.gameObject);
        shell.Select(OctopusSampleTab.Settings);
        Assert.IsFalse(detail.gameObject.activeSelf);
        shell.Select(OctopusSampleTab.Scenarios);
        Assert.IsTrue(detail.gameObject.activeSelf);
        Assert.AreSame(detail, Object.FindAnyObjectByType<OctopusScenarioScreenView>());
        Assert.AreEqual(OctopusSampleBackOutcome.Popped,
            shell.GetComponent<OctopusSampleBackHandler>().HandleBack());
        Assert.AreEqual(OctopusSampleTab.Scenarios, shell.Selected);
    }

    [Test]
    public void SettingsConfirmationAndDemoGameRemainReachable()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Settings);
        var settings = shell.GetComponentInChildren<OctopusSampleSettingsView>();
        // The confirmation is a modal dialog, as on Android: leaving the tab dismisses it.
        settings.RequestReset();
        var dialog = GameObject.Find("settings-reset-dialog");
        Assert.IsNotNull(dialog);
        Assert.IsTrue(Find(dialog.transform, OctopusSampleSettingsView.ResetCancelId).gameObject.activeInHierarchy);
        shell.Select(OctopusSampleTab.Community);
        Assert.IsTrue(dialog == null, "The reset dialog outlived its tab.");
        Assert.IsFalse(settings.IsConfirmingReset);
        shell.Select(OctopusSampleTab.Settings);
        Assert.AreSame(settings, shell.GetComponentInChildren<OctopusSampleSettingsView>());
        settings.RequestReset();
        Assert.IsTrue(settings.IsConfirmingReset);
        settings.CancelReset();
        Assert.IsNull(GameObject.Find("settings-reset-dialog"));
        Assert.AreEqual("settings", OctopusSampleQaDestinations.TabOf(OctopusSampleQaDestinations.Arcade));
        var entry = Find(shell.transform, OctopusSampleSettingsView.DemoGameRowId);
        Assert.IsNotNull(entry);
        entry.GetComponent<Button>().onClick.Invoke();
        var game = Object.FindAnyObjectByType<OctopusReefRunView>();
        Assert.IsNotNull(game);
        _spawned.Add(game.gameObject);
        Assert.AreEqual(OctopusSampleBackOutcome.Popped, shell.GetComponent<OctopusSampleBackHandler>().HandleBack());
        Assert.AreEqual(OctopusSampleTab.Settings, shell.Selected);
    }

    private OctopusSampleShell Create()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        return shell;
    }

    private void DestroySpawned()
    {
        foreach (var host in _spawned)
        {
            if (host == null) continue;
            var shell = host.GetComponent<OctopusSampleShell>();
            if (shell != null) shell.Shutdown();
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.gameObject.name == name) return root;
        for (var i = 0; i < root.childCount; i++)
        {
            var found = Find(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> Methods = new List<string>();

        public void LogApiCall(string method, string detail = null)
        {
            Methods.Add(method);
        }

        public readonly List<(string headline, string detail)> StateChanges =
            new List<(string, string)>();

        public void LogStateChange(string headline, string detail = null)
        {
            StateChanges.Add((headline, detail));
        }
    }
}
