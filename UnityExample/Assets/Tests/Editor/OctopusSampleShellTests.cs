using TMPro;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Proves the part of the SDK_STANDARDS §5.1 navigation contract that is observable without a
/// device: the four canonical destinations exist under the exact ids the shared catalogue
/// publishes, in the canonical order, the fifth one does not, and building the shell touches
/// nothing.
///
/// Everything is asserted through <c>GameObject.name</c> rather than through uGUI types: the test
/// assembly sets `overrideReferences` with `nunit.framework.dll` as its only precompiled reference,
/// so it cannot name a `UnityEngine.UI` type — and the name is what the QA pipeline actually
/// addresses anyway.
///
/// EditMode tests, so `Start` never runs; <see cref="OctopusSampleShell.Create"/> builds the shell
/// itself, which is why it can be tested at all.
/// </summary>
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
    public void PlatformIdentityBelongsToHomeAppBarOnly()
    {
        var shell = Create();
        foreach (OctopusSampleTab tab in System.Enum.GetValues(typeof(OctopusSampleTab)))
        {
            shell.Select(tab);
            var chip = Find(shell.transform, OctopusSampleHomeView.PlatformChipId);
            if (tab != OctopusSampleTab.Home) { Assert.IsNull(chip); continue; }
            Assert.IsNotNull(chip);
            Assert.IsTrue(chip.IsChildOf(Find(shell.transform, "AppBar")));
            Assert.IsFalse(chip.IsChildOf(Find(shell.transform, "Content")));
            Assert.AreEqual("Unity", chip.GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual(OctopusSampleBranding.PlatformSlotLight, chip.GetComponent<Image>().color);
            Assert.AreEqual(OctopusSampleBranding.MinTouchUnits, chip.GetComponent<LayoutElement>().minHeight);
        }
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
    public void TheShellExposesTheFourCanonicalTabIds()
    {
        var shell = Create();

        foreach (var id in new[] { "home-tab", "scenarios-tab", "community-tab", "settings-tab" })
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

        Assert.AreEqual(new[] { "home-tab", "scenarios-tab", "community-tab", "settings-tab" }, ids,
            "Tab order is fixed by SDK_STANDARDS §5.1: Home → Scenarios → [Explorer] → Community " +
            "→ Settings.");
    }

    [Test]
    public void EveryTabIsSelectableAndKeepsTheFourIds()
    {
        var shell = Create();

        foreach (OctopusSampleTab tab in System.Enum.GetValues(typeof(OctopusSampleTab)))
        {
            shell.Select(tab);

            Assert.AreEqual(tab, shell.Selected);
            foreach (var id in new[] { "home-tab", "scenarios-tab", "community-tab", "settings-tab" })
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

        Assert.AreEqual(OctopusSampleBranding.Navy, OctopusSampleBranding.Palette.Accent,
            "The light palette's accent role is the navy (TOKENS §3), not the fill-only blue.");
        var barAfter = Find(shell.transform, "AppBar");
        Assert.IsNotNull(barAfter, "The theme switch left no app bar.");
        Assert.AreNotSame(barBefore, barAfter,
            "The app bar is the same object as before the theme change — nothing was rebuilt, and " +
            "every colour on screen is still the dark palette's.");
        foreach (var id in new[] { "home-tab", "settings-tab" })
        {
            Assert.IsNotNull(Find(shell.transform, id),
                "The theme switch lost '" + id + "' — the shell did not rebuild.");
        }
    }

    [Test]
    public void TheSettingsTabKeepsTheLegacyScenesReachable()
    {
        // Four legacy scenes — OctopusAuth, ForcedLogin, SSO, LanguageOverride — are reachable only
        // from `MainMenu.unity`, and this shell took build index 0 away from it. Without a route
        // back they still build, still pass every gate, and cannot be opened by anyone.
        var shell = Create();
        shell.Select(OctopusSampleTab.Settings);

        Assert.IsNotNull(Find(shell.transform, OctopusSampleShell.LegacyScenesId),
            "Nothing in the shell leads back to '" + OctopusSampleShell.LegacyMenuScene + "'.");
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
    public void AppearanceHookKeepsTheThemeControlIdAndChangesThePalette()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        var host = new GameObject("AppearanceTest", typeof(RectTransform));
        _spawned.Add(host);
        var toggle = OctopusSampleShell.BuildThemeToggle(host.transform);
        Assert.AreEqual("shell-theme-toggle", toggle.name);
        toggle.GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(OctopusSampleTheme.Light, OctopusSampleBranding.Theme);
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
        Assert.AreEqual(4, Find(shell.transform, "TabBar").childCount);
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
    public void ListQueryCollapseAndScrollSurviveTabAndThemeChanges()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var view = shell.GetComponentInChildren<OctopusScenariosListView>();
        view.ToggleSection(ScenarioSection.SignIn);
        view.SetQuery("locale");
        shell.Select(OctopusSampleTab.Home);
        shell.Select(OctopusSampleTab.Scenarios);
        view = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.AreEqual("locale", view.Query);
        var restoredField = view.GetComponentInChildren<TMP_InputField>();
        Assert.AreEqual("locale", restoredField.text);
        Assert.IsFalse(restoredField.placeholder.enabled,
            "A restored query must hide the placeholder before the first canvas rebuild.");
        Canvas.ForceUpdateCanvases();
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        view = shell.GetComponentInChildren<OctopusScenariosListView>();
        Assert.AreEqual("locale", view.Query);
        view.SetQuery("");
        Assert.IsTrue(view.GetComponentInChildren<TMP_InputField>().placeholder.enabled);
        Assert.IsFalse(view.IsSectionOpen(ScenarioSection.SignIn));

        view.ToggleSection(ScenarioSection.SignIn);
        Canvas.ForceUpdateCanvases();
        var scroll = view.GetComponentInChildren<ScrollRect>();
        scroll.verticalNormalizedPosition = 0.4f;
        var before = scroll.verticalNormalizedPosition;
        shell.Select(OctopusSampleTab.Home);
        shell.Select(OctopusSampleTab.Scenarios);
        scroll = shell.GetComponentInChildren<OctopusScenariosListView>().GetComponentInChildren<ScrollRect>();
        Assert.AreEqual(before, scroll.verticalNormalizedPosition, 0.01f);
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
