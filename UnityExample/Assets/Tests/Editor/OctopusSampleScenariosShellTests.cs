using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Scenarios tab's Android shell: the "Open the community" grid, the live community config
/// preview, and the pure wording both read.
/// </summary>
public class OctopusSampleScenariosShellTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        OctopusSampleState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusScenarioSdk.Use(null);
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSampleState.Reset();
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void OpenModesFollowAndroidsGridOrderAndOnlyFullScreenIsAvailable()
    {
        CollectionAssert.AreEqual(new[]
        {
            OctopusSampleOpenMode.Tab, OctopusSampleOpenMode.Modal,
            OctopusSampleOpenMode.Sheet, OctopusSampleOpenMode.FullScreen,
        }, OctopusSampleOpenModes.Order);
        CollectionAssert.AreEqual(new[] { "Tab", "Modal", "Sheet", "Full screen" },
            System.Array.ConvertAll(OctopusSampleOpenModes.Order, OctopusSampleOpenModes.Label));
        CollectionAssert.AreEqual(new[]
        {
            "open-community-tab-button", "open-community-modal-button",
            "open-community-sheet-button", "open-community-fullscreen-button",
        }, System.Array.ConvertAll(OctopusSampleOpenModes.Order, OctopusSampleOpenModes.Id));
        foreach (var mode in OctopusSampleOpenModes.Order)
            Assert.AreEqual(mode == OctopusSampleOpenMode.FullScreen, OctopusSampleOpenModes.IsAvailable(mode), mode.ToString());
    }

    [Test]
    public void ConfigLinesUseAndroidsLabelsAndOrder()
    {
        Assert.AreEqual("✓ on", OctopusSampleCommunityConfigLines.Tick(true));
        Assert.AreEqual("✗ off", OctopusSampleCommunityConfigLines.Tick(false));
        Assert.AreEqual("API key: —", OctopusSampleCommunityConfigLines.Line(OctopusSampleCommunityConfigLines.ApiKeyLabel, "—"));
        Assert.AreEqual("Status: SDK not initialised", OctopusSampleCommunityConfigLines.Line(
            OctopusSampleCommunityConfigLines.StatusLabel, OctopusSampleCommunityConfigLines.NotInitialised));
        Assert.AreEqual("What the SDK sees for the current community (backend config + any local override applied below). Updates after switchCommunity.",
            OctopusSampleCommunityConfigLines.Description);
        Assert.AreEqual("explicit · multi checkbox",
            OctopusSampleCommunityConfigLines.Terms(OctopusTermsAcceptanceMode.ExplicitMultiCheckbox));
        Assert.AreEqual("explicit · single checkbox",
            OctopusSampleCommunityConfigLines.Terms(OctopusTermsAcceptanceMode.ExplicitSingleCheckbox));
        Assert.AreEqual("implicit (legal footer)",
            OctopusSampleCommunityConfigLines.Terms(OctopusTermsAcceptanceMode.Implicit));

        var lines = OctopusSampleCommunityConfigLines.Lines(true, OctopusTermsAcceptanceMode.Implicit, false, true);
        Assert.AreEqual(6, lines.Count);
        Assert.AreEqual("Unified Profile (exposeClientUserId)", lines[0].Key);
        Assert.AreEqual("✓ on", lines[0].Value);
        Assert.AreEqual("Terms acceptance", lines[1].Key);
        Assert.AreEqual("implicit (legal footer)", lines[1].Value);
        Assert.AreEqual("Force login (strong actions)", lines[2].Key);
        Assert.AreEqual("✗ off", lines[2].Value);
        Assert.AreEqual("Display account age", lines[3].Key);
        Assert.AreEqual("✓ on", lines[3].Value);
        Assert.AreEqual("Force nickname choice", lines[4].Key);
        Assert.AreEqual("✗ off", lines[4].Value);
        Assert.AreEqual("Consent texts", lines[5].Key);
        Assert.AreEqual("SDK wordings (none supplied)", lines[5].Value);
        Assert.AreEqual("12", OctopusSampleCommunityConfigLines.CountText(12));
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void ScenariosTabShowsTheOpenGridAboveTheConfigPreview(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);

        var card = Find(shell.transform, OctopusSampleOpenModes.CardId);
        Assert.IsNotNull(card, "The Open the community card is missing.");
        foreach (var mode in OctopusSampleOpenModes.Order)
        {
            var button = Find(card, OctopusSampleOpenModes.Id(mode));
            Assert.IsNotNull(button, OctopusSampleOpenModes.Id(mode) + " is missing: a mode Unity cannot do stays visible.");
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.AreEqual(OctopusSampleOpenModes.IsAvailable(mode), button.GetComponent<Selectable>().interactable,
                OctopusSampleOpenModes.Id(mode));
        }
        Assert.IsNotNull(Find(card, OctopusSampleOpenModes.UnavailableCaptionId), "Disabled modes need their reason.");

        var wrapper = Find(shell.transform, OctopusSampleCommunityConfigLines.WrapperId);
        Assert.IsNotNull(wrapper);
        Assert.IsNotNull(Find(wrapper, OctopusSampleCommunityConfigLines.PreviewId));
        Assert.AreSame(card.parent, wrapper.parent);
        Assert.Less(card.GetSiblingIndex(), wrapper.GetSiblingIndex());
        Assert.IsNull(Find(wrapper, OctopusSampleCommunityConfigLines.ReadButtonId),
            "Nothing to read before the SDK is up.");
    }

    [Test]
    public void TheLiveBlockStepsAsideWhileSearching()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var list = shell.GetComponentInChildren<OctopusScenariosListView>();
        var card = Find(shell.transform, OctopusSampleOpenModes.CardId);
        list.SetQuery("connection");
        Assert.IsFalse(card.gameObject.activeInHierarchy);
        list.SetQuery("");
        Assert.IsTrue(card.gameObject.activeInHierarchy);
    }

    [Test]
    public void TheConfigIsReadOnTapOnlyAndAMissingConfigIsNeutral()
    {
        var sdk = new OctopusRecordingScenarioSdk();
        OctopusScenarioSdk.Use(sdk);
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        OctopusSampleState.ReportInitialized("SSO");

        var wrapper = Find(shell.transform, OctopusSampleCommunityConfigLines.WrapperId);
        var read = Find(wrapper, OctopusSampleCommunityConfigLines.ReadButtonId);
        Assert.IsNotNull(read, "Once the SDK is up the preview offers a read.");
        CollectionAssert.DoesNotContain(sdk.Methods, "DebugGetCommunityConfig", "The preview read without a tap.");

        read.GetComponent<Button>().onClick.Invoke();

        CollectionAssert.Contains(sdk.Methods, "DebugGetCommunityConfig");
        CollectionAssert.Contains(probe.Methods, "OctopusSDK.DebugGetCommunityConfig");
        // The recording SDK answers with no config: nothing failed, so the preview says so in a
        // neutral line with a Retry, and keeps the error block for a read that threw.
        Assert.IsNull(Find(wrapper, OctopusSampleCommunityConfigLines.ErrorId));
        var texts = wrapper.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToList();
        CollectionAssert.Contains(texts, OctopusSampleCommunityConfigLines.Line(
            OctopusSampleCommunityConfigLines.StatusLabel, OctopusSampleCommunityConfigLines.NotAvailable));
        CollectionAssert.Contains(texts, OctopusSampleCommunityConfigLines.RetryLabel);
        Assert.IsTrue(texts.Any(t => t.StartsWith(OctopusSampleCommunityConfigLines.ApiKeyLabel + ": ")),
            "The masked API key line is always shown, as on Android.");
    }

    [Test]
    public void TheFullScreenTileIsThePrimaryOne()
    {
        Assert.AreEqual(OctopusSampleOpenMode.FullScreen, OctopusSampleOpenModes.FirstAvailable());
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var card = Find(shell.transform, OctopusSampleOpenModes.CardId);
        foreach (var mode in OctopusSampleOpenModes.Order)
        {
            var tile = Find(card, OctopusSampleOpenModes.Id(mode)).GetComponent<SampleUiButton>();
            Assert.AreEqual(mode == OctopusSampleOpenMode.FullScreen ? SampleUiButtonVariant.Primary : SampleUiButtonVariant.Secondary,
                tile.PageVariant, mode.ToString());
            var size = tile.GetComponents<LayoutElement>().OrderByDescending(e => e.layoutPriority).First();
            Assert.AreEqual(OctopusSampleBranding.Dp(OctopusScenariosListView.TileDp), size.preferredHeight, 0.01f,
                "The tile is pinned to 48dp; the pill sprite would otherwise inflate it.");
        }
    }

    [Test]
    public void TheMonoFaceIsAnOsMonospaceFaceMarkedDontSaveWithAnInterFallback()
    {
        var mono = SampleUiFonts.Mono;
        Assert.IsNotNull(mono, "The API chip needs a face.");
        // Menlo ships with every macOS, the platform the Editor gates run on.
        if (Application.platform == RuntimePlatform.OSXEditor)
            Assert.AreNotSame(SampleUiFonts.Regular, mono, "No OS monospace face was loaded; the chip fell back to Inter.");
        if (mono == SampleUiFonts.Regular) return;

        StringAssert.EndsWith(" SDF (sample cache)", mono.name);
        Assert.AreEqual(HideFlags.DontSave, mono.hideFlags);
        CollectionAssert.Contains(mono.fallbackFontAssetTable, SampleUiFonts.Regular,
            "Glyphs the OS face lacks must fall back to Inter.");
        Assert.IsNotEmpty(mono.atlasTextures);
        foreach (var texture in mono.atlasTextures)
            if (texture != null)
                Assert.AreEqual(HideFlags.DontSave, texture.hideFlags,
                    "An atlas without DontSave is destroyed by an asset unload (MissingReferenceException in TMP).");
    }

    [Test]
    public void AThrownReadShowsTheErrorAndARetry()
    {
        var sdk = new OctopusRecordingScenarioSdk { ThrowOnConfigRead = true };
        var wrapper = ReadyPreview(sdk);

        TapRead(wrapper);

        var error = Find(wrapper, OctopusSampleCommunityConfigLines.ErrorId);
        Assert.IsNotNull(error, "A read that threw must say so.");
        CollectionAssert.Contains(Texts(error), "Config read failed: config read unavailable");
        Assert.AreEqual(OctopusSampleCommunityConfigLines.RetryLabel, ReadButtonLabel(wrapper));
    }

    [Test]
    public void AReadExpiresAfterItsTimeoutAndItsLateAnswerIsIgnored()
    {
        var sdk = new OctopusRecordingScenarioSdk { DeferCompletions = true };
        var wrapper = ReadyPreview(sdk);
        var list = wrapper.GetComponentInParent<OctopusScenariosListView>();

        TapRead(wrapper);
        var loading = OctopusSampleCommunityConfigLines.Line(OctopusSampleCommunityConfigLines.StatusLabel,
            OctopusSampleCommunityConfigLines.Loading);
        CollectionAssert.Contains(Texts(wrapper), loading);
        Assert.IsFalse(list.ExpireConfigRead(Time.realtimeSinceStartup), "Expired before its timeout.");
        CollectionAssert.Contains(Texts(wrapper), loading);

        Assert.IsTrue(list.ExpireConfigRead(Time.realtimeSinceStartup + OctopusSampleCommunityConfigLines.ReadTimeoutSeconds + 1f));
        CollectionAssert.DoesNotContain(Texts(wrapper), loading);
        Assert.AreEqual(OctopusSampleCommunityConfigLines.ReadLabel, ReadButtonLabel(wrapper),
            "A read that never answers must give the Read button back.");

        sdk.ConfigResult(Config(true));
        sdk.ConfigError("late failure");
        Assert.AreEqual(OctopusSampleCommunityConfigLines.ReadLabel, ReadButtonLabel(wrapper),
            "The expired read's answer must land nowhere.");
        Assert.IsNull(Find(wrapper, OctopusSampleCommunityConfigLines.ErrorId));
        Assert.IsFalse(Texts(wrapper).Any(t => t.StartsWith("Unified Profile")));
    }

    [Test]
    public void ACommunitySwitchWhileTheTabIsHiddenResetsALoadedPanel()
    {
        var sdk = new OctopusRecordingScenarioSdk { DeferCompletions = true };
        var wrapper = ReadyPreview(sdk);
        var list = wrapper.GetComponentInParent<OctopusScenariosListView>();
        Load(sdk, wrapper);

        // Hidden: the Changed event of the switch is not heard, only the generation tells.
        list.SetTabActive(false);
        var generation = OctopusScenarioSdk.CommunityGeneration;
        OctopusScenarioSdk.CommunitySwitched(new OctopusExampleConfig.ExampleProfile
            { apiKey = "test-alt", apiServerHost = "demo.example.invalid", userId = "alt-user" }, 2);
        Assert.AreNotEqual(generation, OctopusScenarioSdk.CommunityGeneration);
        Assert.IsTrue(OctopusSampleState.IsInitialized);
        list.SetTabActive(true);

        Assert.IsFalse(Texts(wrapper).Any(t => t.StartsWith("Unified Profile")),
            "A reading of the previous community stayed on screen.");
        Assert.AreEqual(OctopusSampleCommunityConfigLines.ReadLabel, ReadButtonLabel(wrapper));
    }

    [Test]
    public void AStateChangeThatStopsTheSdkResetsALoadedPanel()
    {
        var sdk = new OctopusRecordingScenarioSdk { DeferCompletions = true };
        var wrapper = ReadyPreview(sdk);
        Load(sdk, wrapper);

        OctopusSampleState.Reset();

        Assert.IsFalse(Texts(wrapper).Any(t => t.StartsWith("Unified Profile")),
            "A reading stayed on screen after the SDK went down.");
        CollectionAssert.Contains(Texts(wrapper), OctopusSampleCommunityConfigLines.Line(
            OctopusSampleCommunityConfigLines.StatusLabel, OctopusSampleCommunityConfigLines.NotInitialised));
        Assert.IsNull(Find(wrapper, OctopusSampleCommunityConfigLines.ReadButtonId));
    }

    [TestCase(1080f)]
    [TestCase(720f)]
    public void TheLockedForceLoginSwitchSitsOnItsRowAndTheNoteInItsGroup(float canvasWidth)
    {
        OctopusSampleState.ReportInitialized("SSO");
        var host = new GameObject("LockCanvas", typeof(RectTransform));
        _spawned.Add(host);
        var canvas = SampleUi.OverlayCanvas(host, 0);
        canvas.renderMode = RenderMode.WorldSpace;
        var hostRect = (RectTransform)host.transform;
        hostRect.sizeDelta = new Vector2(canvasWidth, 2340f);
        OctopusScenariosListView.BuildInto(hostRect);
        for (var pass = 0; pass < 3; pass++)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRect);
        }

        var head = (RectTransform)Find(host.transform, OctopusScenarioSections.HeaderIdOf(ScenarioSection.SignIn));
        var toggle = (RectTransform)Find(host.transform, OctopusSampleFeatureToggles.ForceLoginId);
        var note = (RectTransform)Find(host.transform, OctopusScenariosListView.ForceLoginConfigLinkId);
        Assert.IsNotNull(head);
        Assert.IsNotNull(toggle);
        Assert.IsNotNull(note, "The SDK is up: the lock note is expected.");
        Assert.IsFalse(toggle.GetComponentInChildren<Selectable>(true).interactable, "Force login is locked once the SDK is up.");
        var slot = (RectTransform)head.parent;
        var group = (RectTransform)slot.parent;
        Assert.AreEqual(head.rect.height, slot.rect.height, 0.5f, "The head's slot must hold the row alone.");

        var centre = head.InverseTransformPoint(toggle.TransformPoint(toggle.rect.center));
        Assert.IsTrue(head.rect.Contains(centre),
            "The switch centre " + centre + " lies outside its row " + head.rect + ".");
        Assert.AreEqual(head.rect.center.y, centre.y, 1f, "The switch must be centred on its row.");

        var corners = new Vector3[4];
        note.GetWorldCorners(corners);
        var groupRect = group.rect;
        foreach (var corner in corners)
        {
            var local = group.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(groupRect.xMin - 0.5f, groupRect.xMax + 0.5f), "The note leaves its group.");
            Assert.That(local.y, Is.InRange(groupRect.yMin - 0.5f, groupRect.yMax + 0.5f), "The note leaves its group.");
            Assert.LessOrEqual(head.InverseTransformPoint(corner).y, head.rect.yMin + 0.5f,
                "The note must sit under the row, not over it.");
        }

        // Device QA: the note ended "Change in Confi…" under a two-line clamp.
        var label = Find(host.transform, OctopusScenariosListView.ForceLoginLockedNoteId).GetComponent<TMP_Text>();
        Assert.IsNull(label.GetComponent<SampleUiLineClamp>(), "The lock note must never be clamped.");
        Assert.AreNotEqual(TextOverflowModes.Ellipsis, label.overflowMode);
        label.ForceMeshUpdate();
        Assert.IsFalse(label.isTextTruncated, "The lock note is cut.");
        Assert.AreEqual(OctopusSampleFeatureToggles.ForceLoginLockedNote(), label.GetParsedText().TrimEnd(),
            "The view must not append a second instruction to change the profile in Configuration.");
        Assert.GreaterOrEqual(label.rectTransform.rect.height + 0.5f, label.preferredHeight,
            "The lock note overflows its row.");
    }

    [Test]
    public void AQueryHoldsTheListAtTheTopWhileTheViewportSettles()
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var safe = shell.GetComponentInChildren<SampleUiSafeArea>();
        var full = new Vector2(1080f, 1920f);
        safe.Apply(new Rect(Vector2.zero, full), new Rect(), full);
        Canvas.ForceUpdateCanvases();
        var list = shell.GetComponentInChildren<OctopusScenariosListView>();
        var scroll = list.GetComponentInChildren<ScrollRect>();
        var now = Time.unscaledTime;

        list.SetQuery("sign");
        Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.5f, "The query did not go back to the top.");
        // The next frame re-applies it, whatever moved the list in between.
        scroll.content.anchoredPosition = new Vector2(0f, 600f);
        Assert.IsTrue(list.HoldTop(now), "The return to the top is not re-applied on the next frame.");
        Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.5f);
        Assert.IsFalse(list.HoldTop(now), "Nothing moved: the hold must not fight the reader.");

        // The soft keyboard opens (or finishes opening) after the query: the viewport shrinks.
        safe.Apply(new Rect(Vector2.zero, full), new Rect(0f, full.y - 900f, full.x, 900f), full);
        Canvas.ForceUpdateCanvases();
        scroll.content.anchoredPosition = new Vector2(0f, 600f);
        Assert.IsTrue(list.HoldTop(now), "A viewport resize during the hold must re-pin the top.");
        Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.5f, "The results are left above the viewport.");

        Assert.IsFalse(list.HoldTop(now + OctopusScenariosListView.TopHoldSeconds + 0.1f),
            "The hold outlived its window.");
        safe.Apply(new Rect(Vector2.zero, full), new Rect(), full);
        Canvas.ForceUpdateCanvases();
        Assert.IsFalse(list.HoldTop(now), "An expired hold re-applied.");
    }

    [Test]
    public void AKeyboardReportedFromTheTopIsReadAsDockedAtTheBottom()
    {
        var screen = new Vector2(1080f, 2400f);
        // Docked keyboard as TouchScreenKeyboard.area reports it: y is the keyboard's top, from the top.
        Assert.AreEqual(new Rect(0f, 0f, 1080f, 883f),
            SampleUiSafeArea.BottomOriginKeyboard(new Rect(0f, 1517f, 1080f, 883f), screen));
        // Same, stopping above a 63px navigation bar.
        Assert.AreEqual(new Rect(0f, 63f, 1080f, 883f),
            SampleUiSafeArea.BottomOriginKeyboard(new Rect(0f, 1454f, 1080f, 883f), screen));
        Assert.AreEqual(new Rect(), SampleUiSafeArea.BottomOriginKeyboard(new Rect(), screen));

        var safe = new Rect(Vector2.zero, screen);
        var fromTop = SampleUiSafeArea.UsableArea(safe, new Rect(0f, 1517f, 1080f, 883f), screen);
        Assert.AreEqual(883f / 2400f, fromTop.yMin, 0.0001f, "The keyboard swallowed the whole screen.");
        Assert.AreEqual(1f, fromTop.yMax, 0.0001f);

        var aboveNavigation = SampleUiSafeArea.UsableArea(new Rect(0f, 63f, 1080f, 2337f),
            new Rect(0f, 1454f, 1080f, 883f), screen);
        Assert.AreEqual(946f / 2400f, aboveNavigation.yMin, 0.0001f);
        Assert.Greater(aboveNavigation.height, 0f);
    }

    [TestCase(0f)]
    [TestCase(1517f)]
    public void ClosingADockedKeyboardRestoresTheColdStartSafeAreaAndViewport(float hiddenAreaY)
    {
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var layout = shell.GetComponentInChildren<SampleUiSafeArea>();
        var rect = (RectTransform)layout.transform;
        var screen = new Vector2(1080f, 2400f);
        var native = new Vector4(0f, 63f, 0f, 63f);
        var initialSafe = SampleUiSafeArea.AndroidSafeArea(
            new Rect(0f, 63f, 1080f, 2337f), native, screen, true);
        layout.Apply(initialSafe, new Rect(), screen);
        Canvas.ForceUpdateCanvases();
        var initialAnchors = new Rect(rect.anchorMin, rect.anchorMax - rect.anchorMin);
        var initialTop = rect.anchorMax.y;
        var initialRect = rect.rect;
        var viewport = shell.GetComponentInChildren<OctopusScenariosListView>()
            .GetComponentInChildren<ScrollRect>().viewport;
        var initialViewportRect = viewport.rect;
        var initialViewportPosition = viewport.position;

        // Android reports the docked IME from the top: [0,1517][1080,2400].
        layout.Apply(initialSafe, new Rect(0f, 1517f, 1080f, 883f), screen);
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(883f / 2400f, rect.anchorMin.y, 0.0001f);
        Assert.AreEqual(initialTop, rect.anchorMax.y);
        Assert.Less(viewport.rect.height, initialViewportRect.height);

        // After the IME relayout Unity can retain a second 63px top exclusion, while native
        // surface-relative bars remain unchanged. Re-intersecting Unity made this permanent.
        var afterSafe = SampleUiSafeArea.AndroidSafeArea(
            Rect.MinMaxRect(0f, 63f, 1080f, 2274f), native, screen, true);
        layout.Apply(afterSafe, new Rect(0f, hiddenAreaY, 0f, 0f), screen);
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(initialSafe, afterSafe, "The top or bottom safe inset survived the IME.");
        Assert.AreEqual(initialAnchors, new Rect(rect.anchorMin, rect.anchorMax - rect.anchorMin));
        Assert.AreEqual(initialRect, rect.rect);
        Assert.AreEqual(initialViewportRect, viewport.rect);
        Assert.AreEqual(initialViewportPosition, viewport.position);
    }

    [TestCase(1080f, 2400f, 100f, 1500f, 600f, 400f, 500f)]
    [TestCase(1080f, 2400f, 0f, 1500f, 1080f, 400f, 500f)]
    [TestCase(1080f, 2400f, 0f, 0f, 1080f, 400f, 2000f)]
    [TestCase(2048f, 2732f, 100f, 2200f, 600f, 400f, 132f)]
    [TestCase(1080f, 2400f, 100f, 1936f, 600f, 400f, 64f)]
    public void ATopOriginFloatingKeyboardLeavesTheUsableAreaUnchanged(float screenWidth, float screenHeight,
        float x, float y, float width, float height, float expectedBottom)
    {
        var screen = new Vector2(screenWidth, screenHeight);
        var keyboard = new Rect(x, y, width, height);
        Assert.AreEqual(new Rect(x, expectedBottom, width, height),
            SampleUiSafeArea.BottomOriginKeyboard(keyboard, screen));

        var safe = new Rect(0f, 63f, screen.x, screen.y - 63f);
        var usable = SampleUiSafeArea.UsableArea(safe, keyboard, screen);
        Assert.AreEqual(63f / screen.y, usable.yMin, 0.0001f,
            "A floating keyboard must not reduce the usable area, even near the bottom edge.");
        Assert.AreEqual(1f, usable.yMax, 0.0001f);
        Assert.Greater(usable.height, 0f);

        var full = SampleUiSafeArea.UsableArea(new Rect(Vector2.zero, screen), keyboard, screen);
        Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), full);
    }

    [TestCase(1080f, 2400f, 100f, 600f, 0f, 0f, 0f)]
    [TestCase(2400f, 1080f, 63f, 2337f, 63f, 0f, 0f)]
    [TestCase(2400f, 1080f, 0f, 2337f, 0f, 63f, 0f)]
    [TestCase(2400f, 1080f, 0f, 2337f, 0f, 63f, 63f)]
    public void ADockedKeyboardShrinksTheUsableAreaWhateverItsWidth(float screenWidth, float screenHeight,
        float x, float width, float leftInset, float rightInset, float bottomInset)
    {
        var screen = new Vector2(screenWidth, screenHeight);
        var keyboard = new Rect(x, screen.y - bottomInset - 400f, width, 400f);
        var safe = new Rect(leftInset, bottomInset, screen.x - leftInset - rightInset,
            screen.y - bottomInset);
        var usable = SampleUiSafeArea.UsableArea(safe, keyboard, screen);

        Assert.AreEqual((bottomInset + 400f) / screen.y, usable.yMin, 0.0001f);
        Assert.AreEqual(1f, usable.yMax, 0.0001f);
        Assert.AreEqual(leftInset / screen.x, usable.xMin, 0.0001f);
        Assert.AreEqual((screen.x - rightInset) / screen.x, usable.xMax, 0.0001f);
        Assert.Greater(usable.height, 0f);
    }

    [Test]
    public void SearchingAScrolledListWithTheKeyboardOpenShowsTheFirstMatchesUnderTheField()
    {
        // Device QA, PR #468: scroll down, open the keyboard, type "sign" — the list area stayed
        // empty until the keyboard closed. The keyboard area came from the top and the safe area
        // collapsed, leaving the list viewport with no height at all.
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        var safe = shell.GetComponentInChildren<SampleUiSafeArea>();
        var full = new Vector2(1080f, 1920f);
        var screenSafe = new Rect(Vector2.zero, full);
        safe.Apply(screenSafe, new Rect(), full);
        Canvas.ForceUpdateCanvases();
        var list = shell.GetComponentInChildren<OctopusScenariosListView>();
        var scroll = list.GetComponentInChildren<ScrollRect>();
        // The collapsed list can fit the runner's viewport. Open the sections so this regression
        // really starts from scrolled content, whatever size the EditMode canvas happens to have.
        foreach (var section in OctopusScenarioSections.All)
            if (!list.IsSectionOpen(section)) list.ToggleSection(section);
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
        Assert.Greater(scroll.content.anchoredPosition.y, 1f, "The list did not scroll: the test proves nothing.");

        // A known bottom safe inset leaves the same viewport as a docked keyboard: the reference.
        safe.Apply(new Rect(0f, 480f, full.x, full.y - 480f), new Rect(), full);
        Canvas.ForceUpdateCanvases();
        var expected = scroll.viewport.rect.height;

        safe.Apply(screenSafe, new Rect(0f, full.y - 480f, full.x, 480f), full);
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(0.25f, ((RectTransform)safe.transform).anchorMin.y, 0.0001f,
            "The safe area does not stop at the keyboard's top.");
        Assert.AreEqual(expected, scroll.viewport.rect.height, 0.5f, "The keyboard collapsed the list viewport.");

        list.SetQuery("sign");
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.5f, "The filtered list kept the old offset.");
        var header = (RectTransform)Find(list.transform, OctopusScenarioSections.HeaderIdOf(ScenarioSection.SignIn));
        Assert.IsNotNull(header, "\"sign\" no longer matches the Sign-in section.");
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, header);
        var viewport = scroll.viewport.rect;
        Assert.LessOrEqual(bounds.max.y, viewport.yMax + 0.5f, "The first match starts above the viewport.");
        Assert.GreaterOrEqual(bounds.max.y, viewport.yMax - OctopusSampleBranding.Dp(48f),
            "The first match is not right under the search field.");

        // Clearing the query goes back to the top as well.
        scroll.verticalNormalizedPosition = 0f;
        list.SetQuery("");
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.5f, "Clearing the query kept the old offset.");
    }

    private RectTransform ReadyPreview(OctopusRecordingScenarioSdk sdk)
    {
        OctopusScenarioSdk.Use(sdk);
        var shell = Create();
        shell.Select(OctopusSampleTab.Scenarios);
        OctopusSampleState.ReportInitialized("SSO");
        var wrapper = (RectTransform)Find(shell.transform, OctopusSampleCommunityConfigLines.WrapperId);
        Assert.IsNotNull(wrapper);
        return wrapper;
    }

    private static void Load(OctopusRecordingScenarioSdk sdk, Transform wrapper)
    {
        TapRead(wrapper);
        sdk.ConfigResult(Config(true));
        Assert.IsTrue(Texts(wrapper).Any(t => t.StartsWith("Unified Profile")), "The read did not load.");
        Assert.AreEqual(OctopusSampleCommunityConfigLines.RefreshLabel, ReadButtonLabel(wrapper));
    }

    private static void TapRead(Transform wrapper)
    {
        var read = Find(wrapper, OctopusSampleCommunityConfigLines.ReadButtonId);
        Assert.IsNotNull(read, "No read button.");
        read.GetComponent<Button>().onClick.Invoke();
    }

    private static string ReadButtonLabel(Transform wrapper)
    {
        var read = Find(wrapper, OctopusSampleCommunityConfigLines.ReadButtonId);
        Assert.IsNotNull(read, "No read button.");
        return read.GetComponentInChildren<TMP_Text>(true).text;
    }

    private static List<string> Texts(Transform root)
    {
        return root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToList();
    }

    private static OctopusCommunityConfig Config(bool exposeClientUserId)
    {
        var config = (OctopusCommunityConfig)System.Activator.CreateInstance(typeof(OctopusCommunityConfig), true);
        typeof(OctopusCommunityConfig).GetProperty("ExposeClientUserId").SetValue(config, exposeClientUserId);
        return config;
    }

    private OctopusSampleShell Create()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        return shell;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
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

        public void LogStateChange(string headline, string detail = null) { }
    }
}
