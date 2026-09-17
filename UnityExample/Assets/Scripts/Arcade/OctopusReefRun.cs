using System;

/// <summary>
/// The Reef Run mini-game, as pure C#: no <c>UnityEngine</c> type, no clock of its own, no SDK.
///
/// The rules are the public-domain "one tap, one impulse" endless runner. The octopus holds a
/// fixed <see cref="PlayerX"/> while a line of reefs scrolls past it; a tap sets the vertical
/// velocity to <see cref="Impulse"/>, gravity does the rest, and every reef whose trailing edge
/// passes the octopus scores one point. Touching a reef or the sea bed ends the run.
///
/// Everything advances in fixed <see cref="StepSeconds"/> steps, and <see cref="Advance"/> is the
/// only door in. Two consequences, both deliberate: the game plays identically at 30 and at 120
/// frames per second, and an EditMode test drives exactly the code the player's finger drives —
/// no view, no frame, no Unity player loop. The single <see cref="Random"/> is seeded once, so a
/// given seed replays a given sequence of reefs.
/// </summary>
public sealed class OctopusReefRun
{
    /// <summary>One simulation step. The view may run at any frame rate; the physics does not.</summary>
    public const float StepSeconds = 1f / 60f;

    /// <summary>World units, top to bottom. The stage camera is sized on this and nothing else.</summary>
    public const float WorldHeight = 10f;

    /// <summary>World units, centre to either edge.</summary>
    public const float WorldHalfWidth = 6f;

    /// <summary>The octopus never moves horizontally; the reefs come to it.</summary>
    public const float PlayerX = -3f;

    /// <summary>Collision radius of the octopus, in world units.</summary>
    public const float PlayerRadius = 0.42f;

    public const float Gravity = -21f;
    public const float Impulse = 7f;

    /// <summary>World units per second the reefs travel to the left.</summary>
    public const float Speed = 4.2f;

    /// <summary>
    /// Distance between two reefs — a third of a second more than <see cref="ReefSpacing"/> = <see
    /// cref="Speed"/> would give. One tap lifts the octopus 1.17 units and a gap leaves it 1.28 to
    /// play with, so at one reef per second the line can demand a correction the octopus cannot
    /// finish before the next opening arrives: unplayable, not hard. At 1.33 s it is the ordinary
    /// runner difficulty — an ideal pilot clears a hundred reefs on every seed, a human does not.
    /// </summary>
    public const float ReefSpacing = 5.6f;
    public const float ReefHalfWidth = 0.6f;

    /// <summary>Vertical opening of the first reefs; it narrows with the score, never below <see cref="MinGapHeight"/>.</summary>
    public const float GapHeight = 3.4f;
    public const float MinGapHeight = 2.5f;

    /// <summary>
    /// How far a new opening may sit from the one before it. Unconstrained, two consecutive reefs
    /// can ask for a five-unit climb in the second and a third between them — reachable only by
    /// holding the tap down and arriving at full speed, which is a coin flip rather than a skill.
    /// Capping the shift is what every runner of this family does, and what makes the line readable.
    /// </summary>
    public const float MaxGapShift = 2.2f;

    /// <summary>
    /// How many reefs exist at once. Four covers the visible width plus one off-screen, which is
    /// why nothing is ever instantiated or destroyed while the run is going: a cleared reef is
    /// moved back to the front of the queue instead.
    /// </summary>
    public const int ReefCount = 4;

    /// <summary>
    /// The most simulated time a single <see cref="Advance"/> may add. A frame lost to a garbage
    /// collection or to the app being backgrounded must not teleport the octopus through a reef.
    /// </summary>
    public const float MaxAdvance = 0.25f;

    /// <summary>One reef: a full-height wall with a hole in it.</summary>
    public struct Reef
    {
        /// <summary>Centre of the wall, in world units.</summary>
        public float X;

        /// <summary>Centre of the hole, in world units from the middle of the screen.</summary>
        public float GapCenter;

        /// <summary>Half the height of the hole, in world units.</summary>
        public float GapHalf;

        /// <summary>True once the octopus has passed it and the point has been counted.</summary>
        public bool Cleared;
    }

    private readonly Reef[] _reefs = new Reef[ReefCount];
    private readonly Random _random;
    private float _carry;
    private float _lastGapCenter;

    public int Score { get; private set; }
    public float PlayerY { get; private set; }
    public float PlayerVelocity { get; private set; }

    /// <summary>True between <see cref="Start"/> and the crash — or the <see cref="Stop"/> — that ends it.</summary>
    public bool Running { get; private set; }

    /// <summary>True while the run is held for a lost focus or a backgrounded app. Time does not pass.</summary>
    public bool Paused { get; private set; }

    /// <summary>True when the last run ended by hitting something. <see cref="Stop"/> does not set it.</summary>
    public bool Finished { get; private set; }

    /// <summary>Fixed steps simulated since construction. The determinism a test asserts on.</summary>
    public int Steps { get; private set; }

    /// <summary>The score to beat when the run was opened from a shared post, otherwise null.</summary>
    public int? TargetScore { get; private set; }

    /// <summary>True once a challenge run has passed the score it was opened with.</summary>
    public bool Beaten { get { return TargetScore.HasValue && Score > TargetScore.Value; } }

    public OctopusReefRun(int seed)
    {
        _random = new Random(seed);
        Layout();
    }

    public Reef GetReef(int index) { return _reefs[index]; }

    /// <summary>
    /// Begins a run. <paramref name="targetScore"/> carries the score of the post the player
    /// arrived from, which only changes what the HUD says: the rules are the same.
    /// </summary>
    public void Start(int? targetScore = null)
    {
        TargetScore = targetScore.HasValue && targetScore.Value >= 0 ? targetScore : null;
        Score = 0;
        PlayerY = 0f;
        PlayerVelocity = 0f;
        _carry = 0f;
        Running = true;
        Paused = false;
        Finished = false;
        Layout();
    }

    public void Pause() { if (Running) Paused = true; }
    public void Resume() { if (Running) Paused = false; }

    /// <summary>Abandons the run without finishing it — the screen was closed, nothing was scored.</summary>
    public void Stop()
    {
        Running = false;
        Paused = false;
        _carry = 0f;
    }

    /// <summary>One tap. Returns false when the run is not accepting input, so a view can stay silent.</summary>
    public bool Flap()
    {
        if (!Running || Paused) return false;
        PlayerVelocity = Impulse;
        return true;
    }

    /// <summary>
    /// Adds <paramref name="delta"/> seconds of play, in whole <see cref="StepSeconds"/> steps.
    /// A non-finite or negative delta is ignored rather than propagated into the world state.
    /// </summary>
    public void Advance(float delta)
    {
        if (!Running || Paused) return;
        if (delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        _carry += Math.Min(delta, MaxAdvance);
        while (_carry >= StepSeconds && Running)
        {
            _carry -= StepSeconds;
            Step();
        }
    }

    /// <summary>The opening a reef spawned at <paramref name="score"/> gets. Tighter as the run goes on.</summary>
    public static float GapHeightAt(int score)
    {
        var height = GapHeight - 0.05f * Math.Max(0, score);
        return height < MinGapHeight ? MinGapHeight : height;
    }

    private void Layout()
    {
        // The octopus starts at y = 0, so the first opening is measured from there like any other.
        _lastGapCenter = 0f;
        for (int i = 0; i < ReefCount; i++)
        {
            _reefs[i].X = WorldHalfWidth + ReefHalfWidth + i * ReefSpacing;
            _reefs[i].GapCenter = NextGapCenter(GapHeight);
            _reefs[i].GapHalf = GapHeight * 0.5f;
            _reefs[i].Cleared = false;
        }
    }

    private float NextGapCenter(float gapHeight)
    {
        // Keep the whole opening inside the world, with room for the octopus at either lip.
        var reach = WorldHeight * 0.5f - gapHeight * 0.5f - PlayerRadius;
        if (reach < 0f) reach = 0f;
        var center = (float)(_random.NextDouble() * 2.0 - 1.0) * reach;

        var low = _lastGapCenter - MaxGapShift;
        var high = _lastGapCenter + MaxGapShift;
        if (center < low) center = low;
        if (center > high) center = high;

        // The clamp above may have pushed the opening past the surface or the sea bed; the world
        // wins over the shift cap, so the whole gap always stays inside what the stage films.
        if (center < -reach) center = -reach;
        if (center > reach) center = reach;

        _lastGapCenter = center;
        return center;
    }

    private void Step()
    {
        PlayerVelocity += Gravity * StepSeconds;
        PlayerY += PlayerVelocity * StepSeconds;
        Steps++;

        var travel = Speed * StepSeconds;
        for (int i = 0; i < ReefCount; i++)
        {
            var reef = _reefs[i];
            reef.X -= travel;
            if (!reef.Cleared && reef.X + ReefHalfWidth < PlayerX)
            {
                reef.Cleared = true;
                Score++;
            }
            if (reef.X + ReefHalfWidth < -WorldHalfWidth)
            {
                // Recycled to the back of the queue: the spacing of the line is preserved exactly,
                // so no reef is ever created or destroyed mid-run.
                reef.X += ReefCount * ReefSpacing;
                reef.GapHalf = GapHeightAt(Score) * 0.5f;
                reef.GapCenter = NextGapCenter(reef.GapHalf * 2f);
                reef.Cleared = false;
            }
            _reefs[i] = reef;
        }

        if (Hits()) Crash();
    }

    private bool Hits()
    {
        var ceiling = WorldHeight * 0.5f - PlayerRadius;
        if (PlayerY > ceiling)
        {
            // The surface is a wall, not a death: the classic rule, and the one that keeps a
            // mistimed first tap from ending the run before the first reef.
            PlayerY = ceiling;
            PlayerVelocity = 0f;
        }
        if (PlayerY < -ceiling) return true;

        for (int i = 0; i < ReefCount; i++)
        {
            var reef = _reefs[i];
            if (Math.Abs(reef.X - PlayerX) > ReefHalfWidth + PlayerRadius) continue;
            if (Math.Abs(PlayerY - reef.GapCenter) + PlayerRadius > reef.GapHalf) return true;
        }
        return false;
    }

    private void Crash()
    {
        Running = false;
        Paused = false;
        Finished = true;
        _carry = 0f;
    }
}
