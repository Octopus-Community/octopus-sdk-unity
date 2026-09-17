using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Reef Run's contract: a deterministic game, a best score that survives the screen, and the four
/// SDK integrations the game exists to exercise.
///
/// The game logic is pure C# with a fixed step, so these tests drive exactly what a finger drives —
/// no player loop, no frame rate, no waiting. The screen tests go through `GameObject.name`, the
/// sample's QA addressing, and through the recording seam rather than a live SDK.
/// </summary>
public class OctopusReefRunTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly Dictionary<string, int> _memory = new Dictionary<string, int>();
    private OctopusRecordingScenarioSdk _sdk;

    [SetUp]
    public void SetUp()
    {
        _memory.Clear();
        OctopusReefRunChallenge.Reset();
        OctopusReefRunView.ResetRouting();
        OctopusSampleState.Reset();
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key" }
        };
        OctopusScenarioSdk.Use(_sdk);
        OctopusScenarioSdk.ResetOperationSlot();
    }

    [TearDown]
    public void TearDown()
    {
        if (OctopusReefRunView.Current != null) OctopusReefRunView.Current.Close();
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusReefRunView.ResetRouting();
        OctopusReefRunChallenge.Reset();
        OctopusReefRunArt.Forget();
        OctopusScenarioSdk.Use(null);
        OctopusSampleState.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    // ---------------------------------------------------------------- the game

    [Test]
    public void TheSameSeedAndTheSameTapsReplayTheSameRun()
    {
        var left = new OctopusReefRun(1234);
        var right = new OctopusReefRun(1234);
        left.Start();
        right.Start();
        for (var step = 0; step < 600; step++)
        {
            if (step % 17 == 0) { left.Flap(); right.Flap(); }
            left.Advance(OctopusReefRun.StepSeconds);
            right.Advance(OctopusReefRun.StepSeconds);
        }
        Assert.AreEqual(left.Steps, right.Steps);
        Assert.AreEqual(left.Score, right.Score);
        Assert.AreEqual(left.PlayerY, right.PlayerY);
        Assert.AreEqual(left.Running, right.Running);
    }

    [Test]
    public void FrameLengthChangesNothing()
    {
        // The whole point of the fixed step: 15 fps and 60 fps play the same game. Both runs tap on
        // the same frame boundary — and they have to tap, since an octopus left alone sinks into the
        // sea bed in 40 steps, which would end both runs on step 40 and prove nothing.
        var coarse = new OctopusReefRun(7);
        var fine = new OctopusReefRun(7);
        coarse.Start();
        fine.Start();
        for (var frame = 0; frame < 12; frame++)
        {
            // Every tenth frame, so the octopus arcs instead of pinning itself to the surface,
            // where the ceiling clamp would make any two runs agree for the wrong reason.
            var tap = frame % 10 == 0;
            if (tap) coarse.Flap();
            coarse.Advance(4f * OctopusReefRun.StepSeconds);
            if (tap) fine.Flap();
            for (var sub = 0; sub < 4; sub++) fine.Advance(OctopusReefRun.StepSeconds);
        }
        Assert.IsTrue(coarse.Running && fine.Running, "A run ended before the comparison was over.");
        Assert.AreEqual(48, fine.Steps, "The 60 fps run did not simulate one step per call.");
        Assert.AreEqual(fine.Steps, coarse.Steps, "The two frame rates simulated a different world.");
        Assert.AreEqual(fine.PlayerY, coarse.PlayerY, 1e-4f);
        Assert.AreEqual(fine.Score, coarse.Score);
        Assert.Less(coarse.PlayerY, OctopusReefRun.WorldHeight * 0.5f - OctopusReefRun.PlayerRadius - 0.1f,
            "The comparison only proved that both runs were stuck against the surface.");
    }

    [Test]
    public void ALostFrameCannotTeleportThroughAReef()
    {
        var run = new OctopusReefRun(3);
        run.Start();
        run.Advance(30f);
        Assert.LessOrEqual(run.Steps, Mathf.CeilToInt(OctopusReefRun.MaxAdvance / OctopusReefRun.StepSeconds));
    }

    [Test]
    public void NothingMovesBeforeStartOrWhilePausedOrAfterTheCrash()
    {
        var run = new OctopusReefRun(5);
        run.Advance(1f);
        Assert.AreEqual(0, run.Steps, "A run advanced before Start.");
        Assert.IsFalse(run.Flap());

        run.Start();
        run.Pause();
        run.Advance(1f);
        Assert.AreEqual(0, run.Steps, "A paused run advanced.");
        Assert.IsFalse(run.Flap(), "A paused run accepted a tap.");
        run.Resume();
        run.Advance(1f);
        Assert.Greater(run.Steps, 0);

        while (run.Running) run.Advance(OctopusReefRun.StepSeconds);
        var steps = run.Steps;
        run.Advance(1f);
        Assert.AreEqual(steps, run.Steps, "A finished run advanced.");
        Assert.IsTrue(run.Finished);
    }

    [Test]
    public void AnUntouchedOctopusFallsAndTheSurfaceIsAWallRatherThanADeath()
    {
        var falling = new OctopusReefRun(11);
        falling.Start();
        while (falling.Running) falling.Advance(OctopusReefRun.StepSeconds);
        Assert.IsTrue(falling.Finished);
        Assert.AreEqual(0, falling.Score, "A run that never tapped scored.");

        var rising = new OctopusReefRun(11);
        rising.Start();
        for (var step = 0; step < 120 && rising.Running; step++)
        {
            rising.Flap();
            rising.Advance(OctopusReefRun.StepSeconds);
        }
        Assert.IsTrue(rising.Running, "Holding the surface ended the run.");
        Assert.LessOrEqual(rising.PlayerY, OctopusReefRun.WorldHeight * 0.5f);
    }

    [Test]
    public void APlayedRunScoresAndTheGapNarrowsToAFloor()
    {
        var run = Played(new OctopusReefRun(21), 3);
        Assert.GreaterOrEqual(run.Score, 3);

        Assert.AreEqual(OctopusReefRun.GapHeight, OctopusReefRun.GapHeightAt(0));
        Assert.Less(OctopusReefRun.GapHeightAt(8), OctopusReefRun.GapHeightAt(2));
        Assert.AreEqual(OctopusReefRun.MinGapHeight, OctopusReefRun.GapHeightAt(1000));
        for (var score = 0; score < 200; score++)
            Assert.GreaterOrEqual(OctopusReefRun.GapHeightAt(score), OctopusReefRun.MinGapHeight);
    }

    [Test]
    public void EveryReefStaysInsideTheWorldTheStageFilms()
    {
        var run = Played(new OctopusReefRun(33), 5);
        for (var i = 0; i < OctopusReefRun.ReefCount; i++)
        {
            var reef = run.GetReef(i);
            Assert.GreaterOrEqual(reef.GapCenter - reef.GapHalf, -OctopusReefRun.WorldHeight * 0.5f);
            Assert.LessOrEqual(reef.GapCenter + reef.GapHalf, OctopusReefRun.WorldHeight * 0.5f);
        }
    }

    [Test]
    public void TwoReefsInARowNeverAskForAnImpossibleClimb()
    {
        // One tap lifts the octopus 1.17 units. Two openings a full world apart would make the line
        // a coin flip, so the generator caps the shift — and the cap is what makes the game a game.
        for (var seed = 0; seed < 12; seed++)
        {
            var run = Played(new OctopusReefRun(seed), 6);
            var line = new List<OctopusReefRun.Reef>();
            for (var i = 0; i < OctopusReefRun.ReefCount; i++) line.Add(run.GetReef(i));
            line.Sort((a, b) => a.X.CompareTo(b.X));
            for (var i = 1; i < line.Count; i++)
            {
                var shift = Mathf.Abs(line[i].GapCenter - line[i - 1].GapCenter);
                Assert.LessOrEqual(shift, OctopusReefRun.MaxGapShift + 1e-3f,
                    "Seed " + seed + " asked for a " + shift + " unit climb between two reefs.");
            }
        }
    }

    [Test]
    public void AnIdealPilotClearsAHundredReefs()
    {
        // The difficulty gate. A run nobody can survive is not a hard game, it is a broken one: if
        // the physics and the line ever drift apart again, this is the test that says so.
        var run = Played(new OctopusReefRun(5), 100);
        Assert.GreaterOrEqual(run.Score, 100);
        Assert.IsTrue(run.Running);
    }

    [Test]
    public void AChallengeRunIsBeatenOnlyAboveItsTarget()
    {
        var run = new OctopusReefRun(44);
        run.Start(2);
        Assert.AreEqual(2, run.TargetScore);
        Assert.IsFalse(run.Beaten);
        Played(run, 3);
        Assert.IsTrue(run.Beaten);

        run.Start(-1);
        Assert.IsNull(run.TargetScore, "A negative target became a challenge.");
    }

    // ---------------------------------------------------------------- best score

    [Test]
    public void TheBestScoreOnlyEverImproves()
    {
        var progress = Progress();
        Assert.AreEqual(0, progress.Best);
        Assert.IsTrue(progress.Record(7));
        Assert.IsFalse(progress.Record(3));
        Assert.AreEqual(7, progress.Best);
        Assert.IsTrue(progress.Record(9));
        Assert.AreEqual(9, Progress().Best, "The best score did not survive the screen.");
        Assert.AreEqual(9, _memory[OctopusReefRunProgress.BestKey]);
    }

    // ---------------------------------------------------------------- the client object

    [Test]
    public void AnObjectIdCarriesTheRunAndTheScoreBothWays()
    {
        var runId = OctopusReefRunShare.NewRunId();
        var objectId = OctopusReefRunShare.ObjectId(runId, 12);
        string parsedRun;
        int parsedScore;
        Assert.IsTrue(OctopusReefRunShare.TryParse(objectId, out parsedRun, out parsedScore));
        Assert.AreEqual(runId, parsedRun);
        Assert.AreEqual(12, parsedScore);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("bridge-recipe-1")]
    [TestCase("reef-run-12")]
    [TestCase("reef-run-abcdef12")]
    [TestCase("reef-run-abcdef12-007")]
    [TestCase("reef-run-abcdef12--1")]
    [TestCase("reef-run-nothex12-3")]
    public void AnIdThisGameDidNotMintIsRefused(string objectId)
    {
        string runId;
        int score;
        Assert.IsFalse(OctopusReefRunShare.TryParse(objectId, out runId, out score), objectId);
        Assert.IsNull(runId);
    }

    [Test]
    public void TheClientObjectCarriesTheScoreItsCtaAndItsSigner()
    {
        var runId = OctopusReefRunShare.NewRunId();
        var clientObject = OctopusReefRunShare.ClientObject(runId, 5, "/cache/share.png", _sdk.SignBridgeShare);
        Assert.AreEqual(OctopusReefRunShare.ObjectId(runId, 5), clientObject.ObjectId);
        Assert.AreEqual(OctopusReefRunShare.PostText(5), clientObject.Text);
        Assert.AreEqual(OctopusReefRunShare.ViewObjectButtonText, clientObject.ViewObjectButtonText);
        Assert.AreEqual("/cache/share.png", clientObject.ImagePath);
        Assert.IsNull(clientObject.ImageUrl, "A client object must carry at most one image.");
        Assert.IsNotNull(clientObject.SignBridgeShare);
        StringAssert.Contains("5", clientObject.CatchPhrase);
    }

    [Test]
    public void OneReefIsSingularAndTheRestArePlural()
    {
        Assert.AreEqual("1 reef", OctopusReefRunShare.Reefs(1));
        Assert.AreEqual("0 reefs", OctopusReefRunShare.Reefs(0));
        Assert.AreEqual("7 reefs", OctopusReefRunShare.Reefs(7));
    }

    // ---------------------------------------------------------------- the return leg

    [Test]
    public void ACtaOnAReefRunPostReopensTheGameOnItsScore()
    {
        var seen = new List<string>();
        var targets = new List<int>();
        OctopusReefRunChallenge.Received += (runId, score) => { seen.Add(runId); targets.Add(score); };
        OctopusReefRunChallenge.Observe();

        _sdk.EmitNavigate(OctopusReefRunShare.ObjectId("abcdef12", 9));
        Assert.AreEqual(new[] { "abcdef12" }, seen.ToArray());
        Assert.AreEqual(9, OctopusReefRunChallenge.Target);
        Assert.AreEqual("abcdef12", OctopusReefRunChallenge.RunId);

        // Anything this game did not mint leaves the pending challenge alone.
        _sdk.EmitNavigate("bridge-recipe-1");
        Assert.AreEqual(1, seen.Count);
        Assert.AreEqual(9, OctopusReefRunChallenge.Target);

        OctopusReefRunChallenge.Clear();
        Assert.IsNull(OctopusReefRunChallenge.Target);
    }

    [Test]
    public void AChallengeReachesAPlayerWhoHasNeverOpenedTheGame()
    {
        // Use case 2 seen from the recipient's side (#236). A challenge is received by the player
        // who has never played: they open a shared Reef Run post in the community and tap its CTA,
        // and the game screen has not been built once in this process. A route armed by
        // OctopusReefRunView.Open would therefore not exist yet — so the SDK initialisation every
        // door goes through is what arms it, here through an unrelated scenario.
        var screen = OctopusScenarioScreenView.Open(new CustomEventsScenario());
        _spawned.Add(screen.gameObject);
        Assert.IsTrue(((IOctopusSampleQaScenario)screen).RunPreset(1));
        CollectionAssert.Contains(_sdk.ScenarioMethods, "Initialize");
        Assert.IsNull(OctopusReefRunView.Current, "The game was built before any CTA was tapped.");

        _sdk.EmitNavigate(OctopusReefRunShare.ObjectId("abcdef12", 9));

        var view = OctopusReefRunView.Current;
        Assert.IsNotNull(view, "A challenge for a player who never opened the game routed nowhere.");
        _spawned.Add(view.gameObject);
        Assert.AreEqual(9, view.Target);
        Assert.IsTrue(Find(view.transform, OctopusReefRunView.ChallengeBannerId).gameObject.activeSelf);
    }

    [Test]
    public void ObservingTwiceSubscribesOnce()
    {
        OctopusReefRunChallenge.Observe();
        OctopusReefRunChallenge.Observe();
        var fires = 0;
        OctopusReefRunChallenge.Received += (runId, score) => fires++;
        _sdk.EmitNavigate(OctopusReefRunShare.ObjectId("00abcdef", 2));
        Assert.AreEqual(1, fires);
    }

    // ---------------------------------------------------------------- the screen

    [Test]
    public void TheScreenCarriesEveryQaIdAndOnlyOneScreenExists()
    {
        var view = Open();
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.ScreenId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.BackId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.StageId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.HudId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.TapId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.ActionId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.ResultId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.ShareId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.MemberId));
        Assert.IsNotNull(Find(view.transform, OctopusReefRunView.ChallengeBannerId));
        Assert.AreSame(view, OctopusReefRunView.Open(), "A second Reef Run screen was opened.");
        // A scenario screen sorts at 1100 and About at 1100 too; Reef Run is opened over both and
        // has to cover them.
        Assert.Greater(OctopusReefRunView.SortingOrder, 1100,
            "Reef Run must sort above the screens it can be opened over.");
    }

    [Test]
    public void ATapStartsTheRunAndLosingFocusHoldsIt()
    {
        var view = Open();
        Assert.IsFalse(view.Run.Running);
        view.Tap();
        Assert.IsTrue(view.Run.Running);

        view.SetFocused(false);
        Assert.IsTrue(view.Run.Paused, "Losing focus did not hold the run.");
        view.Advance(1f);
        Assert.AreEqual(0, view.Run.Steps, "A held run advanced.");
        view.SetFocused(true);
        view.Tap();
        Assert.IsFalse(view.Run.Paused);

        view.SetSuspended(true);
        Assert.IsTrue(view.Run.Paused, "Backgrounding the app did not hold the run.");
    }

    [Test]
    public void ClosingTheScreenKeepsTheBestScoreAndReleasesTheStage()
    {
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 3);
        var score = view.Run.Score;
        Assert.GreaterOrEqual(score, 3);

        // The stage is an off-screen camera rendering into a RenderTexture. Neither is garbage
        // collected, so closing the screen has to destroy them explicitly or the sample leaks a
        // camera and a 960x720 target every time the game is opened (#238).
        var stage = Object.FindAnyObjectByType<OctopusReefRunStage>();
        Assert.IsNotNull(stage, "The screen never built its stage.");
        var camera = stage.Camera;
        var texture = stage.Texture;
        Assert.IsNotNull(camera, "The stage has no camera.");
        Assert.IsNotNull(texture, "The stage has no render texture.");

        view.Close();
        Assert.IsNull(OctopusReefRunView.Current);
        Assert.AreEqual(score, Progress().Best, "Closing the screen lost the score.");
        Assert.IsTrue(camera == null, "Closing the screen left the stage camera alive.");
        Assert.IsTrue(texture == null, "Closing the screen leaked the stage render texture.");
        Assert.IsNull(Object.FindAnyObjectByType<OctopusReefRunStage>(),
            "Closing the screen left the stage object in the scene.");
    }

    [Test]
    public void SharingWithoutASessionGoesThroughTheSamplesOwnSignIn()
    {
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 1);
        view.Share();

        Assert.IsNull(OctopusReefRunView.Current, "The game stayed above the sign-in screen.");
        CollectionAssert.DoesNotContain(_sdk.ScenarioMethods, "FetchOrCreateClientObjectRelatedPost");
        var screen = Object.FindAnyObjectByType<OctopusScenarioScreenView>();
        Assert.IsNotNull(screen, "Sharing signed out did not open the sample's connection scenario.");
        _spawned.Add(screen.gameObject);
    }

    [Test]
    public void SharingAScoreCreatesTheClientObjectPostAndOpensIt()
    {
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 2);
        var score = view.Run.Score;
        view.Share();

        Assert.IsNotNull(_sdk.LastClientObject);
        string runId;
        int shared;
        Assert.IsTrue(OctopusReefRunShare.TryParse(_sdk.LastClientObject.ObjectId, out runId, out shared));
        Assert.AreEqual(score, shared, "The shared post did not carry the score just played.");
        Assert.IsNotNull(_sdk.LastClientObject.SignBridgeShare, "The client object shipped unsigned.");
        CollectionAssert.Contains(_sdk.ScenarioMethods, "PrepareBundledShareImage");
        CollectionAssert.Contains(_sdk.ScenarioMethods, "OpenPost");
        Assert.AreEqual(_sdk.BridgePostId, OctopusScenarioSdk.LatestBridgePostId);
        StringAssert.Contains("Shared", view.Result);
        // Creating the post moves the bridge's state; that is not a CTA tap and must not be taken
        // for one — only a navigate fire reopens the game in challenge mode.
        Assert.IsNull(OctopusReefRunChallenge.Target, "A bridge state change was taken for a CTA tap.");

        // The CTA on that post comes back to this very screen, in challenge mode.
        _sdk.EmitNavigate(_sdk.LastClientObject.ObjectId);
        Assert.AreEqual(score, OctopusReefRunView.Current.Target);
        Assert.AreEqual(score, OctopusReefRunView.Current.Run.TargetScore);
    }

    [Test]
    public void AnEmptyScoreIsNotWorthAPost()
    {
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        var view = Open();
        view.Share();
        CollectionAssert.DoesNotContain(_sdk.ScenarioMethods, "FetchOrCreateClientObjectRelatedPost");
        StringAssert.Contains("one reef", view.Result);
    }

    [Test]
    public void AFailedShareSaysSoAndFreesTheSdkSlot()
    {
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        _sdk.BridgeError = new OctopusClientPostError(OctopusClientPostErrorCode.Other, "no network");
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 1);
        view.Share();
        StringAssert.Contains("no network", view.Result);
        string busy;
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("next", out busy),
            "A failed share kept the SDK slot: " + busy);
        OctopusScenarioSdk.ResetOperationSlot();
    }

    [Test]
    public void AShareThatCannotReadItsImageSaysSoAndFreesTheSdkSlot()
    {
        // Preparing the bundled image is part of the share, so it belongs inside the guarded block:
        // when it threw, the operation slot stayed held and every later SDK call in the sample was
        // refused as busy until the app restarted (#238).
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        _sdk.ThrowShareImage = true;
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 1);
        view.Share();

        StringAssert.Contains("Bundled image unavailable", view.Result);
        CollectionAssert.DoesNotContain(_sdk.ScenarioMethods, "FetchOrCreateClientObjectRelatedPost");
        string busy;
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("next", out busy),
            "A share that could not read its image kept the SDK slot: " + busy);
        OctopusScenarioSdk.ResetOperationSlot();
    }

    [Test]
    public void EachAttemptSharesUnderItsOwnRunId()
    {
        // A run id names one attempt. Keeping it across a replay made two runs that end on the same
        // score collide on one object id, so FetchOrCreateClientObjectRelatedPost answered with the
        // first attempt's post instead of creating a second one (#237).
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 2);
        view.Share();
        var first = _sdk.LastClientObject.ObjectId;

        // Retrying the share of that same attempt must reach the post it already created.
        view.Share();
        Assert.AreEqual(first, _sdk.LastClientObject.ObjectId,
            "Retrying a share of the same attempt minted a second run id.");

        CrashTheRun(view);
        view.PrimaryAction();
        Assert.IsTrue(view.Run.Running, "Play again did not start a second attempt.");
        PlayThroughTheView(view, 2);
        view.Share();

        string firstRun, secondRun;
        int ignored;
        Assert.IsTrue(OctopusReefRunShare.TryParse(first, out firstRun, out ignored));
        Assert.IsTrue(OctopusReefRunShare.TryParse(_sdk.LastClientObject.ObjectId, out secondRun,
            out ignored));
        Assert.AreNotEqual(firstRun, secondRun,
            "A replay shared under the first attempt's run id, so two runs on one score collide.");
    }

    [Test]
    public void ConnectingInOrderToShareKeepsTheRunThatWasWaiting()
    {
        // Use case 3 must not cost the run. Sharing signed out closes the game to make room for the
        // sample's connection scenario, and the score used to die with the screen (#237).
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 2);
        var score = view.Run.Score;
        view.Share();
        Assert.IsNull(OctopusReefRunView.Current, "The game stayed above the sign-in screen.");
        var screen = Object.FindAnyObjectByType<OctopusScenarioScreenView>();
        Assert.IsNotNull(screen, "Sharing signed out did not open the sample's connection scenario.");
        _spawned.Add(screen.gameObject);

        // What the connection scenario reports when ConnectUser comes back.
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");

        var back = OctopusReefRunView.Current;
        Assert.IsNotNull(back, "Connecting in order to share left the player with nothing to share.");
        _spawned.Add(back.gameObject);
        StringAssert.Contains(OctopusReefRunShare.Reefs(score), back.Result);
        Assert.IsTrue(Find(back.transform, OctopusReefRunView.ShareId).gameObject.activeSelf,
            "The restored run had no Share button.");

        back.Share();
        Assert.IsNotNull(_sdk.LastClientObject, "The restored run never reached the SDK.");
        string runId;
        int shared;
        var restored = _sdk.LastClientObject.ObjectId;
        Assert.IsTrue(OctopusReefRunShare.TryParse(restored, out runId, out shared));
        Assert.AreEqual(score, shared, "The restored run shared a different score.");

        // The restored attempt keeps one id: a retry has to reach the post it just created.
        back.Share();
        Assert.AreEqual(restored, _sdk.LastClientObject.ObjectId,
            "Retrying the share of the restored run minted a second run id.");

        // And it is still one id per attempt — the next run is a different attempt.
        CrashTheRun(back);
        back.PrimaryAction();
        PlayThroughTheView(back, 1);
        back.Share();
        string replayed;
        int ignored;
        Assert.IsTrue(OctopusReefRunShare.TryParse(_sdk.LastClientObject.ObjectId, out replayed,
            out ignored));
        Assert.AreNotEqual(runId, replayed,
            "The attempt played after the detour shared under the restored attempt's run id.");
    }

    [Test]
    public void AnAbandonedSignInDetourNeverRestoresItsRunOnALaterAttempt()
    {
        // The run set aside for the detour is process-scoped state, so it has to expire with the
        // detour: a player who leaves the sign-in screen and plays again must not have the earlier
        // attempt's id and score pasted onto the attempt on screen when a session finally opens.
        var first = Open();
        first.Tap();
        PlayThroughTheView(first, 2);
        first.Share();
        var screen = Object.FindAnyObjectByType<OctopusScenarioScreenView>();
        Assert.IsNotNull(screen, "Sharing signed out did not open the sample's connection scenario.");

        // The player backs out of the detour and plays another attempt instead.
        screen.Dismiss();
        var second = Open();
        second.Tap();
        PlayThroughTheView(second, 1);

        // Connecting later, for whatever reason, finds nothing to hand back.
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        Assert.AreSame(second, OctopusReefRunView.Current,
            "A stale hand-back replaced the attempt the player was on.");
        StringAssert.DoesNotContain("are still here", second.Result,
            "The abandoned detour restored its run onto a later attempt.");
    }

    [Test]
    public void AConnectionOpenedLongAfterTheDetourRestoresNothing()
    {
        // Leaving the sign-in screen ends the detour even when the player never plays again: the
        // next visit to Connection is its own errand, and must not resurrect an attempt from an
        // arbitrary earlier moment nor open the game over the screen the player chose.
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 2);
        view.Share();
        var detour = Object.FindAnyObjectByType<OctopusScenarioScreenView>();
        Assert.IsNotNull(detour, "Sharing signed out did not open the sample's connection scenario.");
        detour.Dismiss();

        // A later, unrelated visit to the same scenario, connecting for its own sake.
        var again = OctopusScenarioScreenView.Open(OctopusScenarioPilots.Create(
            OctopusReefRunView.SignInScenarioId));
        _spawned.Add(again.gameObject);
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");

        Assert.IsNull(OctopusReefRunView.Current,
            "Connecting after the detour was abandoned reopened the game over the connection screen.");
    }

    [Test]
    public void TheSignInDetourReplacesAScreenThatCannotSignIn()
    {
        // A challenge CTA can open the game above another scenario's screen. That screen is still
        // there when the game steps aside for the detour, and OctopusScenarioScreenView.Open answers
        // with whatever screen it finds — so the detour has to clear it, or the player is handed
        // back a screen with no way to sign in.
        var other = OctopusScenarioScreenView.Open(new CustomEventsScenario());
        _spawned.Add(other.gameObject);
        var view = Open();
        view.Tap();
        PlayThroughTheView(view, 1);
        view.Share();

        var screen = Object.FindAnyObjectByType<OctopusScenarioScreenView>();
        Assert.IsNotNull(screen, "The detour left no screen at all.");
        _spawned.Add(screen.gameObject);
        Assert.AreEqual(OctopusReefRunView.SignInScenarioId, screen.ScenarioId,
            "Sharing signed out handed the player a screen that cannot sign them in.");
    }

    [Test]
    public void AChallengeSharesUnderItsOwnRunIdNotThePostsOne()
    {
        // The id carried by the challenge names the post that was tapped. The run about to be
        // played is a new attempt, so sharing it must mint a new id rather than answer with the
        // challenger's post (#237).
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        OctopusReefRunView.EnsureRouted();
        _sdk.EmitNavigate(OctopusReefRunShare.ObjectId("abcdef12", 9));
        var view = OctopusReefRunView.Current;
        Assert.IsNotNull(view, "The challenge routed nowhere.");
        _spawned.Add(view.gameObject);

        view.Tap();
        PlayThroughTheView(view, 1);
        view.Share();
        string runId;
        int score;
        Assert.IsTrue(OctopusReefRunShare.TryParse(_sdk.LastClientObject.ObjectId, out runId,
            out score));
        Assert.AreNotEqual("abcdef12", runId,
            "The answer to a challenge shared under the challenger's run id.");
    }

    [Test]
    public void TheResultNamesWhoIsPlayingAsFarAsTheApiAllows()
    {
        // API gap: the public surface carries no nickname and no avatar, so the host's own user id
        // is the only identity the sample can honestly print. See OctopusReefRunView.MemberLine.
        StringAssert.Contains("Not signed in", OctopusReefRunView.MemberLine());
        OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted, "connected");
        _sdk.CurrentProfile = new OctopusProfile(new[] { "premium" }, "player-42");
        StringAssert.Contains("player-42", OctopusReefRunView.MemberLine());
    }

    [Test]
    public void ChallengeModeShowsItsTargetAndStartsAFreshRun()
    {
        var view = OctopusReefRunView.Open(Progress(), 6);
        _spawned.Add(view.gameObject);
        Assert.AreEqual(6, view.Target);
        Assert.IsTrue(Find(view.transform, OctopusReefRunView.ChallengeBannerId).gameObject.activeSelf);
        view.Tap();
        Assert.IsTrue(view.Run.Running);
        Assert.AreEqual(6, view.Run.TargetScore);
        Assert.IsNull(OctopusReefRunChallenge.Target, "Starting the challenge did not consume it.");
    }

    // ---------------------------------------------------------------- helpers

    private OctopusReefRunView Open()
    {
        var view = OctopusReefRunView.Open(Progress());
        _spawned.Add(view.gameObject);
        return view;
    }

    private OctopusReefRunProgress Progress()
    {
        return new OctopusReefRunProgress(
            (key, fallback) => _memory.ContainsKey(key) ? _memory[key] : fallback,
            (key, value) => _memory[key] = value,
            () => { });
    }

    /// <summary>
    /// Plays a run to at least <paramref name="score"/> with the simplest autopilot that works: tap
    /// whenever the octopus is sinking and has dropped below the opening ahead of it. Deterministic,
    /// like the game itself — and good enough to clear a hundred reefs on every seed, which is the
    /// property that lets a test ask for a score instead of hoping for one.
    /// </summary>
    private static OctopusReefRun Played(OctopusReefRun run, int score)
    {
        if (!run.Running) run.Start();
        for (var step = 0; step < 12000 && run.Running && run.Score < score; step++)
        {
            if (Sinking(run)) run.Flap();
            run.Advance(OctopusReefRun.StepSeconds);
        }
        Assert.IsTrue(run.Running, "The autopilot crashed at " + run.Score + ", before scoring " + score + ".");
        return run;
    }

    private static void PlayThroughTheView(OctopusReefRunView view, int score)
    {
        for (var step = 0; step < 12000 && view.Run.Running && view.Run.Score < score; step++)
        {
            if (Sinking(view.Run)) view.Tap();
            view.Advance(OctopusReefRun.StepSeconds);
        }
        Assert.IsTrue(view.Run.Running, "The autopilot crashed before scoring " + score + ".");
    }

    /// <summary>Lets the octopus sink until it crashes, which is how an attempt ends.</summary>
    private static void CrashTheRun(OctopusReefRunView view)
    {
        for (var step = 0; step < 2000 && view.Run.Running; step++)
            view.Advance(OctopusReefRun.StepSeconds);
        Assert.IsFalse(view.Run.Running, "The run never ended.");
    }

    /// <summary>The autopilot's whole brain: is the octopus falling below the opening it is aiming at?</summary>
    private static bool Sinking(OctopusReefRun run)
    {
        // Only tap on the way down. Tapping while already rising stacks impulses and throws the
        // octopus into the reef it is halfway through.
        return run.PlayerVelocity <= 0f && run.PlayerY < TargetY(run);
    }

    private static float TargetY(OctopusReefRun run)
    {
        var center = 0f;
        var nearest = float.MaxValue;

        // A reef stops being the target only once the octopus is fully past it, radius included.
        // Switching a moment early lets the autopilot chase the next opening while still inside the
        // current one, and clip its upper lip.
        var behind = OctopusReefRun.PlayerX - OctopusReefRun.PlayerRadius;
        for (var i = 0; i < OctopusReefRun.ReefCount; i++)
        {
            var reef = run.GetReef(i);
            if (reef.X + OctopusReefRun.ReefHalfWidth < behind) continue;
            if (reef.X >= nearest) continue;
            nearest = reef.X;
            center = reef.GapCenter;
        }
        return center - 0.3f;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        for (var i = 0; i < root.childCount; i++)
        {
            var hit = Find(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
