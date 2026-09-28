using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// The hardware back, driven through <see cref="OctopusSampleBackHandler.HandleBack"/> so no Input
/// System device is needed: one level per press, topmost screen first, a confirmation cancelled
/// before its screen is left, a root tab back to Home, and Home asking for the background.
/// </summary>
public class OctopusSampleBackHandlerTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private OctopusSampleShell _shell;
    private OctopusSampleBackHandler _handler;
    private System.Action _background;
    private int _backgroundRequests;

    [SetUp]
    public void SetUp()
    {
        _background = OctopusSampleBackHandler.BackgroundRequest;
        OctopusSampleBackHandler.BackgroundRequest = () => _backgroundRequests++;
        _backgroundRequests = 0;
        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        _handler = _shell.GetComponent<OctopusSampleBackHandler>();
    }

    [TearDown]
    public void TearDown()
    {
        SampleUiDebugEntryHost.ReleaseAll();
        if (_shell != null) _shell.Shutdown();
        foreach (var item in _objects) if (item != null) Object.DestroyImmediate(item);
        _objects.Clear();
        OctopusReefRunView.ResetRouting();
        OctopusSampleBackHandler.BackgroundRequest = _background;
    }

    [Test]
    public void TheShellOwnsExactlyOneHandler()
    {
        Assert.IsNotNull(_handler);
        OctopusSampleBackHandler.Attach(_shell);
        Assert.AreEqual(1, _shell.GetComponents<OctopusSampleBackHandler>().Length);
    }

    [Test]
    public void TheTopmostScreenClosesThroughItsOwnBack()
    {
        var about = Track(OctopusSampleAboutView.Open());

        Assert.AreEqual(OctopusSampleBackOutcome.Popped, _handler.HandleBack());

        Assert.IsTrue(about == null, "About stayed open.");
        Assert.AreEqual(OctopusSampleTab.Home, _shell.Selected);
        Assert.AreEqual(0, _backgroundRequests);
    }

    [Test]
    public void TheHigherLayerClosesFirstAndSameLayerScreensCloseInReverseOpeningOrder()
    {
        _shell.Select(OctopusSampleTab.Scenarios);
        var scenario = Track(OctopusScenarioScreenView.Open(new ConnectionScenario()));
        var reef = Track(OctopusReefRunView.Open());

        Assert.AreEqual(OctopusSampleBackOutcome.Popped, _handler.HandleBack());
        Assert.IsTrue(reef == null, "Reef Run (1200) did not close before the scenario (1100).");
        Assert.IsTrue(scenario != null, "The scenario closed on the same press.");

        Assert.AreEqual(OctopusSampleBackOutcome.Popped, _handler.HandleBack());
        Assert.IsTrue(scenario == null, "The scenario stayed open.");
        Assert.AreEqual(OctopusSampleTab.Scenarios, _shell.Selected);

        var about = Track(OctopusSampleAboutView.Open());
        var appearance = Track(OctopusSampleAppearanceView.Open());
        Assert.AreEqual(OctopusSampleBackOutcome.Popped, _handler.HandleBack());
        Assert.IsTrue(appearance == null, "The later same-layer screen did not close first.");
        Assert.IsTrue(about != null, "Both same-layer screens closed on one press.");
    }

    [Test]
    public void ASubScreenPopsOneLevelBeforeItsViewCloses()
    {
        var developer = Track(OctopusSampleDeveloperToolsView.Open(() => 0,
            () => new List<OctopusSampleDeveloperToolsView.LogLine>(),
            () => new List<OctopusSampleDeveloperToolsView.InfoFact>(), () => { }));
        var index = developer.CurrentScreen;
        developer.ShowEvents();
        Assert.AreNotEqual(index, developer.CurrentScreen);

        Assert.AreEqual(OctopusSampleBackOutcome.Popped, _handler.HandleBack());
        Assert.IsTrue(developer != null, "Back from a sub-screen closed the whole view.");
        Assert.AreEqual(index, developer.CurrentScreen);

        Assert.AreEqual(OctopusSampleBackOutcome.Popped, _handler.HandleBack());
        Assert.IsTrue(developer == null, "Back from the index did not close the view.");
    }

    [Test]
    public void AnOpenConfirmationIsCancelledBeforeTheTabIsLeft()
    {
        _shell.Select(OctopusSampleTab.Settings);
        Click(OctopusSampleSettingsView.ResetButtonId);
        var settings = _shell.GetComponentInChildren<OctopusSampleSettingsView>();
        Assert.IsTrue(settings.IsConfirmingReset);

        Assert.AreEqual(OctopusSampleBackOutcome.Cancelled, _handler.HandleBack());
        Assert.IsFalse(settings.IsConfirmingReset, "Back left the reset armed.");
        Assert.AreEqual(OctopusSampleTab.Settings, _shell.Selected);

        Assert.AreEqual(OctopusSampleBackOutcome.Home, _handler.HandleBack());
        Assert.AreEqual(OctopusSampleTab.Home, _shell.Selected);
    }

    [TestCase(OctopusSampleTab.Scenarios)]
    [TestCase(OctopusSampleTab.Community)]
    [TestCase(OctopusSampleTab.Settings)]
    public void ARootTabOtherThanHomeGoesHome(OctopusSampleTab tab)
    {
        _shell.Select(tab);

        Assert.AreEqual(OctopusSampleBackOutcome.Home, _handler.HandleBack());

        Assert.AreEqual(OctopusSampleTab.Home, _shell.Selected);
        Assert.AreEqual(0, _backgroundRequests);
    }

    [Test]
    public void HomeRootAsksForTheBackgroundAndLeavesTheShellAsItIs()
    {
        Assert.AreEqual(OctopusSampleBackOutcome.Background, _handler.HandleBack());

        Assert.AreEqual(1, _backgroundRequests);
        Assert.AreEqual(OctopusSampleTab.Home, _shell.Selected);
        Assert.IsTrue(_shell != null);
    }

    [Test]
    public void AScreenWithoutBackAboveTheShellAsksForTheBackground()
    {
        // First-launch configuration's shape: a detail-layer screen whose header has no Back.
        var host = new GameObject("No-back screen");
        _objects.Add(host);
        SampleUi.OverlayCanvas(host, SampleUi.DetailSortingOrder);
        SampleUi.AppBar("Header", (RectTransform)host.transform, "Configuration");
        _shell.Select(OctopusSampleTab.Settings);

        Assert.AreEqual(OctopusSampleBackOutcome.Background, _handler.HandleBack());

        Assert.AreEqual(1, _backgroundRequests);
        Assert.AreEqual(OctopusSampleTab.Settings, _shell.Selected, "Back went through the screen.");
    }

    [Test]
    public void ANonInteractableBackIsIgnored()
    {
        var about = Track(OctopusSampleAboutView.Open());
        var back = about.GetComponentsInChildren<SampleUiBackTarget>().Single().Back;
        back.interactable = false;

        Assert.AreEqual(OctopusSampleBackOutcome.Ignored, _handler.HandleBack());
        Assert.IsTrue(about != null);
        Assert.AreEqual(0, _backgroundRequests);
    }

    private T Track<T>(T view) where T : Component
    {
        _objects.Add(view.gameObject);
        return view;
    }

    private void Click(string id)
    {
        var target = _shell.GetComponentsInChildren<Transform>().FirstOrDefault(item => item.name == id);
        Assert.IsNotNull(target, id);
        target.GetComponent<Button>().onClick.Invoke();
    }
}
