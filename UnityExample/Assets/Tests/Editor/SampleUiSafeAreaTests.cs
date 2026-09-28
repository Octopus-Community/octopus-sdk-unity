using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The sample's safe area on Android: Unity's own safe area tightened by the visible system bars,
/// which Unity misses once the player draws edge to edge under them.
/// </summary>
public class SampleUiSafeAreaTests
{
    private static readonly Vector2 Portrait = new Vector2(1080f, 2400f);

    [Test]
    public void AStatusBarUnityDoesNotReportPushesTheTopDown()
    {
        // Emulator shape: Unity reports the navigation bar but not the 63 px status bar.
        var safe = SampleUiSafeArea.Inset(new Rect(0f, 63f, 1080f, 2337f), new Vector4(0f, 63f, 0f, 63f), Portrait);
        Assert.AreEqual(Rect.MinMaxRect(0f, 63f, 1080f, 2337f), safe);
    }

    [Test]
    public void AnInsetUnityAlreadyReportsIsNotCountedTwice()
    {
        // A cutout deeper than the status bar: Unity's edge is the tighter one and wins.
        var safe = SampleUiSafeArea.Inset(Rect.MinMaxRect(0f, 63f, 1080f, 2300f), new Vector4(0f, 63f, 0f, 63f), Portrait);
        Assert.AreEqual(Rect.MinMaxRect(0f, 63f, 1080f, 2300f), safe);
    }

    [Test]
    public void LandscapeBarsInsetTheirOwnSides()
    {
        var landscape = new Vector2(2400f, 1080f);
        var safe = SampleUiSafeArea.Inset(new Rect(0f, 0f, 2400f, 1080f), new Vector4(0f, 63f, 126f, 0f), landscape);
        Assert.AreEqual(Rect.MinMaxRect(0f, 0f, 2274f, 1017f), safe);
    }

    [Test]
    public void NoInsetsLeavesUnitysSafeAreaAsIs()
    {
        var unity = Rect.MinMaxRect(12f, 40f, 1068f, 2300f);
        Assert.AreEqual(unity, SampleUiSafeArea.Inset(unity, Vector4.zero, Portrait));
    }

    [Test]
    public void InsetsLargerThanTheScreenCollapseInsteadOfInverting()
    {
        var safe = SampleUiSafeArea.Inset(new Rect(0f, 0f, 1080f, 2400f), new Vector4(700f, 1500f, 700f, 1500f), Portrait);
        Assert.GreaterOrEqual(safe.width, 0f);
        Assert.GreaterOrEqual(safe.height, 0f);
    }

    [Test]
    public void OnlyTheBarsOverThePlayerViewCount()
    {
        var window = new Vector2(1080f, 2400f);
        var bars = new Vector4(0f, 63f, 0f, 63f);
        // Emulator shape: the view runs under the status bar but stops above the navigation bar.
        Assert.AreEqual(new Vector4(0f, 63f, 0f, 0f),
                        SampleUiSafeArea.BarsOverView(Rect.MinMaxRect(0f, 0f, 1080f, 2337f), window, bars));
        // Fully edge to edge: both bars overlap.
        Assert.AreEqual(bars, SampleUiSafeArea.BarsOverView(Rect.MinMaxRect(0f, 0f, 1080f, 2400f), window, bars));
        // A view already laid out between the bars: nothing to add.
        Assert.AreEqual(Vector4.zero,
                        SampleUiSafeArea.BarsOverView(Rect.MinMaxRect(0f, 63f, 1080f, 2337f), window, bars));
    }

    [Test]
    public void InsetsScaleByTheSurfaceSizeNotItsVisibleRect()
    {
        var bars = new Vector4(0f, 63f, 0f, 0f);
        // A surface laid out at 1080x2337 rendering a 540x1168 screen: half scale on both axes.
        Assert.AreEqual(new Vector4(0f, 31.5f, 0f, 0f),
                        SampleUiSafeArea.ToScreen(bars, new Vector2(1080f, 2337f), new Vector2(540f, 1168.5f)));
        // A same-size screen keeps the pixels as they are.
        Assert.AreEqual(bars, SampleUiSafeArea.ToScreen(bars, new Vector2(1080f, 2337f), new Vector2(1080f, 2337f)));
        // An unknown size yields no inset instead of an infinite one.
        Assert.AreEqual(Vector4.zero, SampleUiSafeArea.ToScreen(bars, Vector2.zero, new Vector2(1080f, 2337f)));
    }

    [Test]
    public void InTheEditorTheSafeAreaIsUnitys()
    {
        Assert.AreEqual(Screen.safeArea, SampleUiSafeArea.ScreenSafeArea());
    }
}
