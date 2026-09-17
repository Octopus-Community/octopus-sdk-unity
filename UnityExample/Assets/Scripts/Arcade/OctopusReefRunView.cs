using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reef Run's screen: the shell's uGUI vocabulary around a Unity 2D stage, and the four places
/// this sample actually integrates the SDK.
///
/// The game itself is <see cref="OctopusReefRunStage"/> — SpriteRenderers filmed by their own
/// camera. Everything here is chrome: an app bar, a HUD, a result card and the buttons that make
/// SDK calls. The split is deliberate and the UI half is meant to be replaceable — the sample's UI
/// layer is being migrated (issue #224), and nothing in <see cref="OctopusReefRun"/>,
/// <see cref="OctopusReefRunShare"/> or <see cref="OctopusReefRunChallenge"/> knows this file
/// exists.
///
/// The four integrations, each a use case a real host would have:
///
/// 1. **Share a score** — `FetchOrCreateClientObjectRelatedPost` with an
///    <see cref="OctopusClientObject"/> whose id carries the run, then `OpenPost` on what comes
///    back. The host owns the run; the community owns the conversation about it.
/// 2. **Come back from the community** — the shared post's CTA raises
///    `OnNavigateToClientObject`, and <see cref="OctopusReefRunChallenge"/> turns that id back into
///    a score to beat, which reopens this screen in challenge mode.
/// 3. **Share while signed out** — routed into the sample's *existing* sign-in flow, the
///    `connection` scenario, rather than a second `ConnectUser` call site of its own.
/// 4. **Name the player** — `OctopusSDK.CurrentProfile`, which is as far as the public API goes;
///    see the TODO on <see cref="MemberLine"/>.
///
/// Every one of them happens behind a tap (SDK_STANDARDS §5.2). Opening this screen is itself the
/// first tap, which is why it — and nothing earlier — is what starts observing the bridge.
/// </summary>
public sealed class OctopusReefRunView : MonoBehaviour
{
    public const string ScreenId = "arcade-screen";
    public const string BackId = "arcade-back";
    public const string StageId = "arcade-stage";
    public const string HudId = "arcade-hud";
    public const string TapId = "arcade-tap";
    public const string ActionId = "arcade-action";
    public const string ShareId = "arcade-share";
    public const string ResultId = "arcade-result";
    public const string ChallengeBannerId = "arcade-challenge-banner";
    public const string MemberId = "arcade-member";

    /// <summary>Above a scenario screen's 1100, which is what the game can be opened over.</summary>
    public const int SortingOrder = 1200;

    /// <summary>The scenario the sign-in path hands the player to. The sample's only ConnectUser.</summary>
    public const string SignInScenarioId = "connection";

    private static OctopusReefRunView _current;
    private static bool _routed;
    private static bool _handingBack;
    private static PendingShare _pending;
    // The very screen the detour opened. The run comes back to that visit and to no other: a
    // player who walks away and opens Connection again later is not in the detour any more.
    private static OctopusScenarioScreenView _detour;

    private OctopusReefRunProgress _progress;
    private OctopusReefRunStage _stage;
    private TMP_Text _hud;
    private TMP_Text _tapHint;
    private TMP_Text _resultTitle;
    private TMP_Text _resultDetail;
    private TMP_Text _member;
    private TMP_Text _actionText;
    private TMP_Text _banner;
    private RectTransform _result;
    private RectTransform _share;
    private RectTransform _bannerHost;
    private RawImage _screen;
    private int _best;
    private int _shownScore = -1;
    private bool _focused = true;
    private bool _suspended;
    private bool _sharing;
    private string _runId;
    private int _restoredScore;

    /// <summary>
    /// A finished run set aside while the player leaves to sign in.
    ///
    /// Sharing signed out closes this screen — the sign-in is another screen and the game's canvas
    /// sorts above it — so without this the run the player wanted to share dies with the view and
    /// they come back to an empty game. Static, because the screen it belongs to no longer exists
    /// by the time the connection completes.
    /// </summary>
    private struct PendingShare
    {
        public bool Has;
        public int Score;
        public string RunId;
        public int? Target;
        public OctopusReefRunProgress Progress;
    }

    /// <summary>The run being played. Never null between <see cref="Open"/> and <see cref="Close"/>.</summary>
    public OctopusReefRun Run { get; private set; }

    /// <summary>The line the last action left on the result card. Read by the EditMode tests.</summary>
    public string Result { get { return _resultDetail == null ? string.Empty : _resultDetail.text; } }

    /// <summary>The score this run was opened to beat, or null outside challenge mode.</summary>
    public int? Target { get; private set; }

    /// <summary>
    /// The score a Share would send: the attempt on screen, or the one restored after the sign-in
    /// detour — that run was played by a screen this one replaces, so it is not in <see cref="Run"/>.
    /// </summary>
    private int ShareScore { get { return Run != null && Run.Score > 0 ? Run.Score : _restoredScore; } }

    /// <summary>The screen currently open, or null. One at a time: it covers the whole display.</summary>
    public static OctopusReefRunView Current { get { return _current; } }

    /// <summary>
    /// Opens the game over whatever is on screen, on its own root object — the overlay pattern
    /// <see cref="OctopusSampleAboutView"/> and <see cref="OctopusScenarioScreenView"/> already
    /// follow. A canvas parented under another screen's is a *nested* canvas: Unity sorts it inside
    /// its parent and ignores the order it was given.
    /// </summary>
    public static OctopusReefRunView Open(OctopusReefRunProgress progress = null,
                                          int? target = null)
    {
        // Opening the game is one of the two taps that arm the return leg; the other is the tap
        // that initialises the SDK. Neither the shell nor Home reaches the SDK, by contract and by
        // test.
        EnsureRouted();

        if (_current != null)
        {
            _current.Challenge(target);
            return _current;
        }

        var host = new GameObject("OctopusReefRunView");
        var view = host.AddComponent<OctopusReefRunView>();
        view._progress = progress ?? new OctopusReefRunProgress();
        view._best = view._progress.Best;
        view.Run = new OctopusReefRun(System.Environment.TickCount);
        view.Target = target;
        view.Build();
        _current = view;
        return view;
    }

    /// <summary>
    /// Arms the whole return leg for this process: the bridge observation and the route that turns
    /// an incoming client-object id back into a game screen.
    ///
    /// Called from two places, and it has to be both. Opening the game is one — a player who shares
    /// a score must come back to it. <see cref="OctopusScenarioSdk.EnsureInitialized"/> is the
    /// other, and it is the one that matters for the recipient of a challenge: they receive a
    /// shared Reef Run post, open the community and tap its CTA without ever having opened the game
    /// in this process. A route armed by the game screen would be armed too late for exactly the
    /// player it exists for.
    ///
    /// Initialisation is the earliest point that is still honestly behind a user tap
    /// (SDK_STANDARDS §5.1/§5.2): the shell and Home make no SDK call, and every door into the SDK
    /// goes through that seam.
    /// </summary>
    public static void EnsureRouted()
    {
        OctopusReefRunChallenge.Observe();
        EnsureChallengeRoute();
        EnsureSignInHandBack();
    }

    /// <summary>
    /// Subscribes the *process* to the challenge callback, once. A CTA tapped inside the native
    /// community arrives while this screen is closed, so the thing that reopens it cannot be owned
    /// by the screen.
    /// </summary>
    public static void EnsureChallengeRoute()
    {
        if (_routed) return;
        _routed = true;
        OctopusReefRunChallenge.Received += OnChallengeReceived;
    }

    /// <summary>
    /// Subscribes the *process* to the sample's own session state, once, so a run set aside for the
    /// sign-in detour comes back when the connection completes.
    /// </summary>
    private static void EnsureSignInHandBack()
    {
        if (_handingBack) return;
        _handingBack = true;
        OctopusSampleState.Changed += OnSampleStateChanged;
    }

    /// <summary>Drops every process-scoped route and the pending run. EditMode only.</summary>
    public static void ResetRouting()
    {
        if (_routed) OctopusReefRunChallenge.Received -= OnChallengeReceived;
        _routed = false;
        if (_handingBack) OctopusSampleState.Changed -= OnSampleStateChanged;
        _handingBack = false;
        ClearPending();
    }

    // The event carries the run id of the post that was tapped; the screen does not keep it. A run
    // id names one attempt, and the attempt about to be played is a new one (#237).
    private static void OnChallengeReceived(string runId, int score)
    {
        if (_current != null) _current.Challenge(score);
        else Open(null, score);
    }

    /// <summary>Forgets the run set aside for a sign-in detour, and the detour it belonged to.</summary>
    private static void ClearPending()
    {
        _pending = default(PendingShare);
        _detour = null;
    }

    private static void OnSampleStateChanged()
    {
        if (!_pending.Has) return;
        if (OctopusSampleState.ConnectionSession != OctopusSampleState.Session.ConnectCompleted) return;
        // Only the player who is still inside the detour gets the run back, and "the detour" is
        // one screen instance, not a scenario id: someone who left it — to play again, to run
        // another scenario, or to open Connection again an hour later for an unrelated reason —
        // would otherwise have this attempt's id and score pasted onto whatever is on screen then,
        // or the game opened over them out of nowhere.
        if (_current != null || _detour == null || FindAnyObjectByType<OctopusScenarioScreenView>() != _detour)
        {
            ClearPending();
            return;
        }
        var pending = _pending;
        ClearPending();
        var view = Open(pending.Progress, pending.Target);
        view.Restore(pending.Score, pending.RunId);
    }

    /// <summary>Puts an open screen into challenge mode, ready for a fresh run against a score.</summary>
    public void Challenge(int? target)
    {
        if (!target.HasValue) return;
        Target = target;
        StartRun(target);
        Refresh();
    }

    /// <summary>
    /// Puts the run the sign-in detour interrupted back on the result card, under its own id, so
    /// one tap on Share finishes what the player asked for before connecting.
    /// </summary>
    private void Restore(int score, string runId)
    {
        _restoredScore = score;
        _runId = runId;
        Refresh();
        Report("Signed in. Your " + OctopusReefRunShare.Reefs(score) +
               " are still here — tap \"Share your score\".");
    }

    /// <summary>
    /// Starts one attempt. An attempt is the unit a share names, so each one drops the previous
    /// one's run id and score: a replay that happens to end on the same number as the last one is
    /// a different run and deserves its own post (#237).
    /// </summary>
    private void StartRun(int? target)
    {
        _runId = null;
        _restoredScore = 0;
        // A new attempt abandons whatever the previous one set aside for the sign-in detour.
        ClearPending();
        Run.Stop();
        Run.Start(target);
        OctopusReefRunChallenge.Clear();
    }

    private void Build()
    {
        SampleUi.OverlayCanvas(gameObject, SortingOrder);
        var ground = SampleUi.Panel("Reef Run ground", transform, SampleUi.Background);
        SampleUi.Stretch(ground, Vector2.zero, Vector2.one);
        var root = SampleUi.SafeArea(ScreenId, ground);

        SampleUi.AppBar("Reef Run header", root, "Reef Run", Close, BackId, "Line");

        var body = SampleUi.Panel("Reef Run body", root, OctopusSampleBranding.Clear);
        SampleUi.Stretch(body, Vector2.zero, Vector2.one);
        body.offsetMax = new Vector2(0f, -OctopusSampleBranding.AppBarUnits);
        var stack = SampleUi.VerticalStack(body, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd),
            SampleUi.Padding(OctopusSampleBranding.SpaceLg, OctopusSampleBranding.SpaceMd), false);
        stack.childAlignment = TextAnchor.UpperCenter;

        BuildBanner(body);
        BuildStage(body);
        BuildResult(body);
        var action = SampleUi.Button(ActionId, body, "Play", PrimaryAction);
        _actionText = action.GetComponentInChildren<TMP_Text>();

        Refresh();
    }

    private void BuildBanner(RectTransform parent)
    {
        _bannerHost = SampleUi.Panel(ChallengeBannerId, parent,
            OctopusSampleBranding.Palette.WarningSurface, OctopusSampleBranding.FieldRadius,
            OctopusSampleBranding.Clear, 0f);
        SampleUi.VerticalStack(_bannerHost, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs),
            SampleUi.Padding(OctopusSampleBranding.SpaceLg, OctopusSampleBranding.SpaceMd), false);
        _banner = SampleUi.FlexibleLabel(_bannerHost, string.Empty, SampleUi.TextCaption,
            OctopusSampleBranding.Palette.Attention);
        _bannerHost.gameObject.SetActive(false);
    }

    private void BuildStage(RectTransform parent)
    {
        _stage = OctopusReefRunStage.Create();

        var frame = SampleUi.Panel(StageId, parent, OctopusSampleBranding.Clear,
            OctopusSampleBranding.CardRadius, OctopusSampleBranding.Palette.Border, 0f);
        var flex = frame.gameObject.AddComponent<LayoutElement>();
        flex.flexibleHeight = 1f;
        flex.minHeight = OctopusSampleBranding.Dp(220f);

        var view = SampleUi.Panel("Stage view", frame, OctopusSampleBranding.Clear);
        SampleUi.Stretch(view, Vector2.zero, Vector2.one);
        var fitter = view.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = OctopusReefRunStage.TextureWidth / (float)OctopusReefRunStage.TextureHeight;

        // The game's pixels reach the screen through a texture rather than straight from the
        // camera, because a Screen Space – Overlay canvas draws after every camera: a camera
        // painting onto the display would sit behind this very screen whatever order it was given.
        var screenHost = new GameObject("Stage image", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(RawImage));
        var screenRect = (RectTransform)screenHost.transform;
        screenRect.SetParent(view, false);
        SampleUi.Stretch(screenRect, Vector2.zero, Vector2.one);
        _screen = screenHost.GetComponent<RawImage>();
        _screen.texture = _stage.Texture;
        _screen.raycastTarget = false;

        // The whole stage is the button: a one-tap game with a small target is not the game.
        var tap = SampleUi.Panel(TapId, view, OctopusSampleBranding.Clear);
        SampleUi.Stretch(tap, Vector2.zero, Vector2.one);
        tap.GetComponent<Image>().raycastTarget = true;
        var button = tap.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = tap.GetComponent<Image>();
        button.onClick.AddListener(Tap);

        _hud = SampleUi.Label(HudId, view, string.Empty, SampleUi.TextTitle,
            OctopusSampleBranding.ShapeInk, TextAnchor.UpperLeft);
        Overlay(_hud, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f));
        _tapHint = SampleUi.Label("Tap hint", view, "Tap to swim", SampleUi.TextBody,
            OctopusSampleBranding.ShapeInk, TextAnchor.MiddleCenter);
        Overlay(_tapHint, Vector2.zero, Vector2.one, Vector2.zero);
    }

    private static void Overlay(TMP_Text label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset)
    {
        var rect = label.rectTransform;
        SampleUi.Stretch(rect, anchorMin, anchorMax);
        var inset = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd);
        rect.offsetMin = new Vector2(inset, rect.offsetMin.y - inset + offset.y);
        rect.offsetMax = new Vector2(-inset, rect.offsetMax.y - inset + offset.y);
        label.raycastTarget = false;
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
    }

    private void BuildResult(RectTransform parent)
    {
        _result = SampleUi.Card(ResultId, parent);
        _resultTitle = SampleUi.FlexibleLabel(_result, string.Empty, SampleUi.TextTitle,
            SampleUi.TitleColor);
        _member = SampleUi.FlexibleLabel(_result, string.Empty, SampleUi.TextCaption, SampleUi.Muted);
        _member.gameObject.name = MemberId;
        _resultDetail = SampleUi.FlexibleLabel(_result, string.Empty, SampleUi.TextCaption,
            SampleUi.Muted);
        _resultDetail.richText = false;
        _share = SampleUi.Button(ShareId, _result, "Share your score",
            SampleUiButtonVariant.Secondary, Share);
        _result.gameObject.SetActive(false);
    }

    /// <summary>One tap: an impulse while the run is going, and the start of one when it is not.</summary>
    public void Tap()
    {
        if (!_focused || _suspended) return;
        if (!Run.Running) { PrimaryAction(); return; }
        if (Run.Paused) { Run.Resume(); Refresh(); return; }
        Run.Flap();
    }

    /// <summary>The footer button: play, pause, resume, or play again.</summary>
    public void PrimaryAction()
    {
        if (!_focused || _suspended) return;
        if (!Run.Running) StartRun(Target);
        else if (Run.Paused) Run.Resume();
        else Run.Pause();
        Refresh();
    }

    /// <summary>
    /// Use case 1 — and use case 3 when there is no session yet.
    ///
    /// The run is the host's object; `FetchOrCreateClientObjectRelatedPost` is what gives it a
    /// home in the community, creating the post the first time and finding it every time after.
    /// The id is the whole contract: the SDK hands it straight back through
    /// `OnNavigateToClientObject` when a member taps the post's CTA, which is use case 2.
    /// </summary>
    public void Share()
    {
        if (_sharing) return;
        var score = ShareScore;
        if (score <= 0)
        {
            Report("Clear at least one reef before sharing a score.");
            return;
        }
        // One id per attempt, minted the first time that attempt is shared and kept for as long as
        // it is the attempt on the card. Retrying a failed share — or coming back from the sign-in
        // detour — must reach the same post; a replay must not (#237).
        if (string.IsNullOrEmpty(_runId)) _runId = OctopusReefRunShare.NewRunId();

        string refusal;
        if (OctopusScenarioSdk.UsableProfile(out refusal) == null) { Report(refusal); return; }

        // Use case 3: no parallel sign-in. The sample has exactly one ConnectUser call site — the
        // `connection` scenario — and a second one here would be the sample teaching the wrong
        // shape to the host that copies it.
        if (OctopusSampleState.ConnectionSession != OctopusSampleState.Session.ConnectCompleted)
        {
            OpenSignIn();
            return;
        }

        string reported;
        if (OctopusScenarioSdk.EnsureInitialized(OctopusScenarioSdk.PilotMode(),
                OctopusScenarioSdk.PilotModeLabel, out reported) == null)
        { Report(reported); return; }

        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("Share Reef Run score", out busy))
        { Report(busy); return; }
        var token = OctopusScenarioSdk.OperationToken;

        var sdk = OctopusScenarioSdk.Current;
        OctopusScenarioSdk.EnsureBridgeObserving();

        _sharing = true;
        try
        {
            // The bundled share image the scenarios already ship, rather than a second binary in the
            // repository for the same job. It is prepared inside the guarded block on purpose: it
            // decodes a bundled asset and can throw, and a throw before the `try` left the single
            // SDK operation slot held until the app restarted (#238).
            var image = sdk.PrepareBundledShareImage();
            var clientObject = OctopusReefRunShare.ClientObject(_runId, score, image, sdk.SignBridgeShare);

            // Same debug-console trail every scenario leaves, so QA reads one log for both.
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.FetchOrCreateClientObjectRelatedPost",
                "objectId=" + clientObject.ObjectId);
            Report("Creating the post for " + OctopusReefRunShare.Reefs(score) + "…");
            sdk.FetchOrCreateClientObjectRelatedPost(clientObject, postId =>
            {
                _sharing = false;
                OctopusScenarioSdk.EndOperation(token);
                if (this == null) return;
                OctopusScenarioSdk.SetBridgePost(clientObject.ObjectId, postId);
                Report("Shared. Opening the post — its \"" + OctopusReefRunShare.ViewObjectButtonText +
                       "\" button brings players back here.");
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.OpenPost", "postId=" + postId);
                sdk.OpenPost(postId);
            }, error =>
            {
                _sharing = false;
                OctopusScenarioSdk.EndOperation(token);
                if (this == null) return;
                Report("Sharing failed: " + error.Code + " — " + error.Message);
            });
        }
        catch (System.Exception exception)
        {
            _sharing = false;
            OctopusScenarioSdk.EndOperation(token);
            Report("Sharing failed: " + exception.Message);
        }
    }

    private void OpenSignIn()
    {
        var pilot = OctopusScenarioPilots.Create(SignInScenarioId);
        if (pilot == null)
        {
            Report("Sign in from Scenarios › Connection, then share your score.");
            return;
        }
        // The run outlives the detour. A player who connects in order to share must not find the
        // score gone when they come back: the best score was already saved, but the run itself is
        // what the post is about (#237).
        _pending = new PendingShare
        {
            Has = true,
            Score = ShareScore,
            RunId = _runId,
            Target = Target,
            Progress = _progress,
        };
        // The game's own canvas sits above a scenario screen's, so it steps aside rather than
        // covering the very screen it is sending the player to.
        Close();
        // A player can reach the game from a post opened by another scenario, leaving that
        // scenario's screen underneath. `OctopusScenarioScreenView.Open` answers with whatever
        // screen is already there, so the detour has to clear an incompatible one first or it
        // hands the player back a screen that cannot sign them in.
        var open = FindAnyObjectByType<OctopusScenarioScreenView>();
        if (open != null && open.ScenarioId != SignInScenarioId) open.Dismiss();
        _detour = OctopusScenarioScreenView.Open(pilot);
    }

    /// <summary>
    /// Use case 4: who is playing.
    ///
    /// TODO(API gap): the public API carries no nickname and no avatar for the connected member.
    /// `OctopusProfile` exposes `Entitlements` and `ClientUserId`, and `OctopusCommunityData`
    /// exposes `ProfileId`, `MessageCount` and `Gamification` — none of them a display name or a
    /// picture URL. So the result card names the host's own user id, which is the only identity
    /// this side can honestly print. Reported as an API gap rather than worked around.
    /// </summary>
    public static string MemberLine()
    {
        if (OctopusSampleState.ConnectionSession != OctopusSampleState.Session.ConnectCompleted)
            return "Not signed in — sharing opens Scenarios › Connection first.";
        var profile = OctopusScenarioSdk.Current.CurrentProfile;
        if (profile == null || string.IsNullOrEmpty(profile.ClientUserId))
            return "Signed in. The SDK publishes no display name to this sample.";
        return "Signed in as " + profile.ClientUserId + ".";
    }

    private void Update() { Advance(Time.unscaledDeltaTime); }

    /// <summary>Drives one frame of the game. Public because EditMode has no player loop.</summary>
    public void Advance(float delta)
    {
        if (Run == null) return;
        var wasRunning = Run.Running;
        Run.Advance(delta);
        if (_stage != null) _stage.Render(Run);
        if (wasRunning && !Run.Running) Finish();
        else RefreshHud();
    }

    private void Finish()
    {
        var improved = _progress.Record(Run.Score);
        _best = _progress.Best;
        Refresh();
        if (Target.HasValue)
        {
            Report(Run.Beaten
                ? "Beaten! " + OctopusReefRunShare.Reefs(Run.Score) + " against " + Target.Value + "."
                : "Not this time — " + Target.Value + " still stands. Tap Play again.");
        }
        else
        {
            Report(improved
                ? "A new personal best. Share it and let the community try to beat it."
                : "Share your score to open a post the community can answer.");
        }
    }

    /// <summary>Repaints every label and every visibility from the run's state.</summary>
    public void Refresh()
    {
        if (_hud == null) return;
        _shownScore = -1;
        RefreshHud();

        var idle = !Run.Running;
        _tapHint.gameObject.SetActive(idle || Run.Paused);
        _tapHint.text = Run.Paused ? "Paused" : Run.Finished ? "Tap to swim again" : "Tap to swim";

        _bannerHost.gameObject.SetActive(Target.HasValue);
        if (Target.HasValue)
        {
            _banner.text = "Challenge from the community: beat " +
                           OctopusReefRunShare.Reefs(Target.Value) + ".";
        }

        // The card also stands for a run restored after the sign-in detour, which this screen
        // never played, so the restored score — not Run — is what puts it there.
        var card = Run.Finished || _restoredScore > 0;
        _result.gameObject.SetActive(card);
        if (card)
        {
            _resultTitle.text = OctopusReefRunShare.Reefs(Run.Finished ? Run.Score : _restoredScore) +
                                " cleared · best " + _best;
            _member.text = MemberLine();
        }
        _share.gameObject.SetActive(card && ShareScore > 0);

        _actionText.SetText(idle ? (Run.Finished ? "Play again" : "Play") : Run.Paused ? "Resume" : "Pause");
    }

    private void RefreshHud()
    {
        if (_hud == null || _shownScore == Run.Score) return;
        _shownScore = Run.Score;
        if (Target.HasValue) _hud.SetText("{0}   best {1}   target {2}", Run.Score, _best, Target.Value);
        else _hud.SetText("{0}   best {1}", Run.Score, _best);
    }

    private void Report(string message)
    {
        if (_resultDetail == null) return;
        _result.gameObject.SetActive(true);
        _resultDetail.text = message;
    }

    private void OnApplicationFocus(bool focused) { SetFocused(focused); }

    private void OnApplicationPause(bool paused) { SetSuspended(paused); }

    // The lifecycle callbacks only forward here, so the pause behaviour is reachable from a test
    // without SendMessage, which Unity refuses on a behaviour outside play mode.
    public void SetFocused(bool focused)
    {
        _focused = focused;
        if (!focused) PauseRun();
    }

    public void SetSuspended(bool suspended)
    {
        _suspended = suspended;
        if (suspended) PauseRun();
    }

    private void PauseRun()
    {
        if (Run == null || !Run.Running || Run.Paused) return;
        Run.Pause();
        SaveBest();
        Refresh();
    }

    private void SaveBest()
    {
        if (Run == null || _progress == null) return;
        _progress.Record(Run.Score);
        _best = _progress.Best;
    }

    private void OnDisable() { Teardown(); }

    private void OnDestroy() { Teardown(); }

    // Idempotent, and called explicitly by Close: outside play mode Unity delivers no OnDisable, so
    // the best score would never be written and the render texture never released.
    private void Teardown()
    {
        if (_current == this) _current = null;
        if (Run != null)
        {
            SaveBest();
            Run.Stop();
        }
        if (_screen != null) _screen.texture = null;
        if (_stage != null)
        {
            _stage.Dispose();
            _stage = null;
        }
    }

    public void Close()
    {
        Teardown();
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
