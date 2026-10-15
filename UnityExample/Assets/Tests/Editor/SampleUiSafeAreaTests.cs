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
    public void AndroidNativeInsetsIncludeTheCutoutAndOwnEveryEdge()
    {
        // Native getInsets(systemBars | displayCutout) has the deeper cutout and side bars.
        var native = new Vector4(12f, 100f, 24f, 63f);
        var unity = Rect.MinMaxRect(0f, 0f, 1080f, 2400f);
        Assert.AreEqual(Rect.MinMaxRect(12f, 63f, 1056f, 2300f),
            SampleUiSafeArea.AndroidSafeArea(unity, native, Portrait, true));

        // A surface already below the bars needs no local inset, even if Unity retains one.
        Assert.AreEqual(new Rect(Vector2.zero, Portrait),
            SampleUiSafeArea.AndroidSafeArea(Rect.MinMaxRect(0f, 63f, 1080f, 2337f),
                Vector4.zero, Portrait, true));
    }

    [Test]
    public void MissingAndroidInsetsFallBackToUnityIncludingItsCutout()
    {
        var unity = Rect.MinMaxRect(12f, 40f, 1068f, 2300f);
        Assert.AreEqual(unity, SampleUiSafeArea.AndroidSafeArea(unity, Vector4.zero, Portrait, false));
    }

    // Values measured on an emulator (Pixel-class, 1080x2400 window, status and navigation bars of
    // 63 px): the player surface spans 0,0-1080,2337 and Unity reports a 1080x2337 screen.
    private static readonly Vector2 Window = new Vector2(1080f, 2400f);
    private static readonly Vector2 PlayerScreen = new Vector2(1080f, 2337f);
    private static readonly Vector4 Bars = new Vector4(0f, 63f, 0f, 63f);

    [Test]
    public void TheMeasuredDeviceInsetsOnlyTheStatusBarOffThePlayer()
    {
        var over = SampleUiSafeArea.BarsOverView(Rect.MinMaxRect(0f, 0f, 1080f, 2337f), Window, Bars);
        Assert.AreEqual(new Vector4(0f, 63f, 0f, 0f), over);
        var inScreen = SampleUiSafeArea.ToScreen(over, new Vector2(1080f, 2337f), PlayerScreen);
        // Unity's own safe area is the full screen there: the native insets alone bring yMax to 2274.
        var safe = SampleUiSafeArea.AndroidSafeArea(new Rect(Vector2.zero, PlayerScreen), inScreen, PlayerScreen, true);
        Assert.AreEqual(Rect.MinMaxRect(0f, 0f, 1080f, 2274f), safe);
    }

    [Test]
    public void TheFirstReadIsAlwaysDue()
    {
        Assert.IsTrue(SampleUiSafeArea.InsetsReadDue(0, -1, PlayerScreen, Vector2.zero, false, Vector4.zero));
    }

    [Test]
    public void AnEmptyColdLaunchReadIsRetriedOnTheNextFrame()
    {
        // Frame 0 of a cold launch: the read succeeds before Android dispatched the window insets.
        Assert.IsFalse(SampleUiSafeArea.InsetsReadDue(0, 0, PlayerScreen, PlayerScreen, true, Vector4.zero));
        Assert.IsTrue(SampleUiSafeArea.InsetsReadDue(1, 0, PlayerScreen, PlayerScreen, true, Vector4.zero));
    }

    [Test]
    public void ARealReadIsKeptForTheRefreshInterval()
    {
        var statusBar = new Vector4(0f, 63f, 0f, 0f);
        for (int frame = 1; frame < SampleUiSafeArea.InsetsRefreshFrames; frame++)
            Assert.IsFalse(SampleUiSafeArea.InsetsReadDue(frame, 0, PlayerScreen, PlayerScreen, true, statusBar), $"frame {frame}");
        Assert.IsTrue(SampleUiSafeArea.InsetsReadDue(SampleUiSafeArea.InsetsRefreshFrames, 0, PlayerScreen, PlayerScreen, true, statusBar));
    }

    [Test]
    public void AFailedReadKeepsTheRefreshInterval()
    {
        // No native insets: the Unity fallback is used, and JNI is not hammered every frame.
        Assert.IsFalse(SampleUiSafeArea.InsetsReadDue(1, 0, PlayerScreen, PlayerScreen, false, Vector4.zero));
        Assert.IsTrue(SampleUiSafeArea.InsetsReadDue(SampleUiSafeArea.InsetsRefreshFrames, 0, PlayerScreen, PlayerScreen, false, Vector4.zero));
    }

    [Test]
    public void AResizeForcesARead()
    {
        var statusBar = new Vector4(0f, 63f, 0f, 0f);
        Assert.IsTrue(SampleUiSafeArea.InsetsReadDue(1, 0, new Vector2(2337f, 1080f), PlayerScreen, true, statusBar));
    }

    [Test]
    public void InTheEditorTheSafeAreaIsUnitys()
    {
        Assert.AreEqual(Screen.safeArea, SampleUiSafeArea.ScreenSafeArea());
    }
}
