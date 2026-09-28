using NUnit.Framework;

/// <summary>
/// The rate policy of issue #294. Only <see cref="OctopusSampleFrameRate.TargetFor"/> is testable
/// here: the <c>[RuntimeInitializeOnLoadMethod]</c> that applies it never runs in EditMode, and
/// <c>Screen.currentResolution</c> reports nothing usable in batch mode.
/// </summary>
public class OctopusSampleFrameRateTests
{
    [Test]
    public void AnUnreadableRefreshRateFallsBackToSixty()
    {
        Assert.AreEqual(OctopusSampleFrameRate.Fallback, OctopusSampleFrameRate.TargetFor(0d));
        Assert.AreEqual(OctopusSampleFrameRate.Fallback, OctopusSampleFrameRate.TargetFor(-1d));
        Assert.AreEqual(OctopusSampleFrameRate.Fallback, OctopusSampleFrameRate.TargetFor(double.NaN));
        Assert.AreEqual(OctopusSampleFrameRate.Fallback,
            OctopusSampleFrameRate.TargetFor(double.PositiveInfinity));
    }

    [Test]
    public void ARateBelowSixtyIsRaisedToTheFloor()
    {
        // The 30 fps mobile default this whole type exists to replace, and a real 48 Hz low-power
        // mode: neither should pin the sample under 60.
        Assert.AreEqual(60, OctopusSampleFrameRate.TargetFor(30d));
        Assert.AreEqual(60, OctopusSampleFrameRate.TargetFor(48d));
    }

    [Test]
    public void AFractionalSixtyHertzPanelReadsAsSixty()
    {
        Assert.AreEqual(60, OctopusSampleFrameRate.TargetFor(59.94d));
        Assert.AreEqual(60, OctopusSampleFrameRate.TargetFor(60.0003d));
    }

    [Test]
    public void AHighRefreshPanelIsFollowedUpToTheCeiling()
    {
        Assert.AreEqual(90, OctopusSampleFrameRate.TargetFor(90d));
        Assert.AreEqual(120, OctopusSampleFrameRate.TargetFor(120d));
    }

    [Test]
    public void AboveTheCeilingTheSampleStopsFollowing()
    {
        Assert.AreEqual(OctopusSampleFrameRate.Ceiling, OctopusSampleFrameRate.TargetFor(144d));
        Assert.AreEqual(OctopusSampleFrameRate.Ceiling, OctopusSampleFrameRate.TargetFor(165d));
    }

    [Test]
    public void TheCeilingIsAboveTheFallback()
    {
        Assert.Greater(OctopusSampleFrameRate.Ceiling, OctopusSampleFrameRate.Fallback);
    }
}
