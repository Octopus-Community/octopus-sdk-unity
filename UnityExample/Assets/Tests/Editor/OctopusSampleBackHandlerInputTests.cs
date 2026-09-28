using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

/// <summary>
/// The hardware back through the Input System itself, on a test keyboard: the Android system back
/// arrives as an Escape key-down and key-up inside one input update, which must still count as one
/// press. <see cref="OctopusSampleBackHandlerTests"/> covers what a press does.
/// </summary>
public class OctopusSampleBackHandlerInputTests : InputTestFixture
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private OctopusSampleShell _shell;
    private OctopusSampleBackHandler _handler;
    private Keyboard _keyboard;
    private System.Action _background;

    public override void Setup()
    {
        base.Setup();
        _background = OctopusSampleBackHandler.BackgroundRequest;
        OctopusSampleBackHandler.BackgroundRequest = () => { };
        _keyboard = InputSystem.AddDevice<Keyboard>();
        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        _handler = _shell.GetComponent<OctopusSampleBackHandler>();
    }

    public override void TearDown()
    {
        if (_handler != null) _handler.StopListening();
        SampleUiDebugEntryHost.ReleaseAll();
        if (_shell != null) _shell.Shutdown();
        foreach (var item in _objects) if (item != null) Object.DestroyImmediate(item);
        _objects.Clear();
        OctopusReefRunView.ResetRouting();
        OctopusSampleBackHandler.BackgroundRequest = _background;
        OctopusSampleBackHandler.ListenOverride = null;
        base.TearDown();
    }

    [Test]
    public void APressAndReleaseInTheSameUpdateIsOnePress()
    {
        OctopusSampleBackHandler.ListenOverride = true;
        _handler.StartListening();
        var about = OctopusSampleAboutView.Open();
        _objects.Add(about.gameObject);

        Press(_keyboard.escapeKey, queueEventOnly: true);
        Release(_keyboard.escapeKey, queueEventOnly: true);
        InputSystem.Update();

        Assert.IsFalse(_keyboard.escapeKey.isPressed, "The key should read as released after the update.");
        Assert.AreEqual(1, _handler.PendingPresses, "The same-update press was lost.");
        Assert.IsTrue(_handler.HandlePendingPress());
        Assert.IsTrue(about == null, "The press did not close the topmost screen.");
        Assert.AreEqual(0, _handler.PendingPresses);
        Assert.IsFalse(_handler.HandlePendingPress());
    }

    [Test]
    public void TwoPressesInOneUpdateAreHandledOnePerFrame()
    {
        OctopusSampleBackHandler.ListenOverride = true;
        _handler.StartListening();
        _shell.Select(OctopusSampleTab.Scenarios);
        var about = OctopusSampleAboutView.Open();
        _objects.Add(about.gameObject);

        Press(_keyboard.escapeKey, queueEventOnly: true);
        Release(_keyboard.escapeKey, queueEventOnly: true);
        Press(_keyboard.escapeKey, queueEventOnly: true);
        Release(_keyboard.escapeKey, queueEventOnly: true);
        InputSystem.Update();

        Assert.AreEqual(2, _handler.PendingPresses);
        Assert.IsTrue(_handler.HandlePendingPress());
        Assert.IsTrue(about == null);
        Assert.AreEqual(OctopusSampleTab.Scenarios, _shell.Selected, "Both presses were handled at once.");
        Assert.IsTrue(_handler.HandlePendingPress());
        Assert.AreEqual(OctopusSampleTab.Home, _shell.Selected);
    }

    [Test]
    public void OutsideAnAndroidPlayerEscapeIsNotBound()
    {
        // The Editor (and iOS) default: Escape on a hardware keyboard must not pop screens.
        OctopusSampleBackHandler.ListenOverride = null;
        Assert.IsFalse(OctopusSampleBackHandler.ListensForHardwareBack);
        _handler.StartListening();

        Press(_keyboard.escapeKey, queueEventOnly: true);
        Release(_keyboard.escapeKey, queueEventOnly: true);
        InputSystem.Update();

        Assert.AreEqual(0, _handler.PendingPresses);
        Assert.IsFalse(_handler.HandlePendingPress());
    }
}
