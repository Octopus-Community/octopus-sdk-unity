using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>What one press of the hardware back did. Returned for the tests and logged for QA.</summary>
public enum OctopusSampleBackOutcome
{
    /// <summary>The topmost screen's Back exists but is not interactable: nothing happened.</summary>
    Ignored,
    /// <summary>An open confirmation was cancelled through its own Cancel button.</summary>
    Cancelled,
    /// <summary>The topmost screen's on-screen Back was pressed: one level popped.</summary>
    Popped,
    /// <summary>A root tab other than Home was showing; Home is selected.</summary>
    Home,
    /// <summary>Nothing left to pop: the app was asked to go to the background.</summary>
    Background
}

/// <summary>
/// The sample's one hardware-back handler, owned by <see cref="OctopusSampleShell"/>.
///
/// On Android the system back (button or gesture) reaches the Input System as
/// <c>Keyboard.escapeKey</c>. The key is only listened to in an Android player
/// (<see cref="ListensForHardwareBack"/>): iOS has no system back, and on an iPad with a hardware
/// keyboard, or in the Editor, Escape would otherwise close screens and send tabs to Home. One
/// press does what the visible topmost screen's own controls would, in the order the native
/// Android sample follows:
/// <list type="number">
/// <item>an open confirmation on that screen is cancelled — any active, interactable button
/// carrying <see cref="SampleUiBackCancel"/> (the confirmation's Cancel, e.g.
/// <c>settings-reset-cancel</c>);</item>
/// <item>otherwise the screen's on-screen Back is pressed — the same <see cref="Button"/>, so the
/// same code path, including a view's own nested pop (Developer tools sub-screen → index);</item>
/// <item>otherwise, on the shell, a root tab other than Home goes to Home;</item>
/// <item>otherwise (Home root, or a screen with no Back such as first-launch configuration) the app
/// goes to the background, as a root Android activity does. It never quits.</item>
/// </list>
///
/// "Topmost" is the <see cref="SampleUiBackTarget"/> whose root canvas has the highest sorting
/// order; screens sharing an order (the 1100 detail layer) are ranked by their root object's
/// position in the scene, which is their opening order and does not move when a view rebuilds.
///
/// While an Octopus SDK screen is in front, the Unity player is paused and receives no key, so the
/// native screen keeps its own back and this handler cannot fire behind it.
/// </summary>
public sealed class OctopusSampleBackHandler : MonoBehaviour
{
    /// <summary>
    /// Forces <see cref="ListensForHardwareBack"/> on or off; null (the default) means "Android
    /// players only". A seam for the EditMode tests, which run in the Editor.
    /// </summary>
    public static bool? ListenOverride;

    /// <summary>
    /// Whether the handler binds the hardware back at all: in an Android player only, unless
    /// <see cref="ListenOverride"/> says otherwise. <see cref="HandleBack"/> works regardless.
    /// </summary>
    public static bool ListensForHardwareBack
    {
        get
        {
            return ListenOverride.HasValue ? ListenOverride.Value
                : Application.platform == RuntimePlatform.Android;
        }
    }

    /// <summary>
    /// The platform call behind <see cref="OctopusSampleBackOutcome.Background"/>. A seam so the
    /// EditMode tests can observe the request instead of reaching Android.
    /// </summary>
    public static System.Action BackgroundRequest = MoveTaskToBack;

    private OctopusSampleShell _shell;

    /// <summary>Adds the handler to <paramref name="shell"/>'s object, once.</summary>
    public static OctopusSampleBackHandler Attach(OctopusSampleShell shell)
    {
        var handler = shell.GetComponent<OctopusSampleBackHandler>();
        if (handler == null) handler = shell.gameObject.AddComponent<OctopusSampleBackHandler>();
        handler._shell = shell;
        return handler;
    }

#if ENABLE_INPUT_SYSTEM
    // An action rather than Keyboard.escapeKey.wasPressedThisFrame: the system back arrives as a
    // key-down and key-up inside one input update, which a per-frame state read never sees as a
    // press. The action is fed every event, so each press is counted.
    private InputAction _back;
    private int _pending;

    private void OnEnable() { StartListening(); }
    private void OnDisable() { StopListening(); }

    /// <summary>
    /// Binds the hardware back if <see cref="ListensForHardwareBack"/>. Called from
    /// <c>OnEnable</c>; public because EditMode never enables a plain component, so the input tests
    /// call it themselves.
    /// </summary>
    public void StartListening()
    {
        if (_back != null || !ListensForHardwareBack) return;
        _back = new InputAction("OctopusSampleBack", InputActionType.Button, "<Keyboard>/escape");
        _back.performed += OnBackPerformed;
        _back.Enable();
    }

    /// <summary>Unbinds the hardware back and drops any press not yet handled.</summary>
    public void StopListening()
    {
        _pending = 0;
        if (_back == null) return;
        _back.performed -= OnBackPerformed;
        _back.Dispose();
        _back = null;
    }

    /// <summary>Presses received and not yet handled.</summary>
    public int PendingPresses { get { return _pending; } }

    private void OnBackPerformed(InputAction.CallbackContext context) { _pending++; }

    /// <summary>
    /// Handles one received press, if any, and reports whether it did. Called once per frame by
    /// <c>Update</c>: a closed view is destroyed at the end of the frame, so a second press handled
    /// in the same frame would find the same screen on top again.
    /// </summary>
    public bool HandlePendingPress()
    {
        if (_pending == 0) return false;
        _pending--;
        HandleBack();
        return true;
    }

    private void Update() { HandlePendingPress(); }
#endif

    /// <summary>Handles one back press. Called by <c>Update</c> on the key, and by the tests.</summary>
    public OctopusSampleBackOutcome HandleBack()
    {
        var outcome = Resolve();
        OctopusSampleQaLaunch.Log("back=" + outcome.ToString().ToLowerInvariant());
        return outcome;
    }

    private OctopusSampleBackOutcome Resolve()
    {
        var screen = Topmost();
        if (screen != null)
        {
            var cancel = OpenCancel(screen.GetComponentInParent<Canvas>().rootCanvas);
            if (cancel != null)
            {
                cancel.onClick.Invoke();
                return OctopusSampleBackOutcome.Cancelled;
            }
            if (screen.Back != null)
            {
                if (!screen.Back.IsInteractable()) return OctopusSampleBackOutcome.Ignored;
                screen.Back.onClick.Invoke();
                return OctopusSampleBackOutcome.Popped;
            }
            if (_shell != null && screen.transform.IsChildOf(_shell.transform) &&
                _shell.Selected != OctopusSampleTab.Home)
            {
                _shell.Select(OctopusSampleTab.Home);
                return OctopusSampleBackOutcome.Home;
            }
        }
        if (BackgroundRequest != null) BackgroundRequest();
        return OctopusSampleBackOutcome.Background;
    }

    private static SampleUiBackTarget Topmost()
    {
        SampleUiBackTarget best = null;
        var bestOrder = int.MinValue;
        var bestIndex = -1;
        foreach (var target in FindObjectsByType<SampleUiBackTarget>(FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (!target.isActiveAndEnabled) continue;
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) continue;
            var root = canvas.rootCanvas;
            var order = root.sortingOrder;
            var index = root.transform.root.GetSiblingIndex();
            if (order < bestOrder || (order == bestOrder && index <= bestIndex)) continue;
            best = target;
            bestOrder = order;
            bestIndex = index;
        }
        return best;
    }

    private static Button OpenCancel(Canvas canvas)
    {
        foreach (var marker in canvas.GetComponentsInChildren<SampleUiBackCancel>())
        {
            var button = marker.GetComponent<Button>();
            if (button != null && button.IsInteractable()) return button;
        }
        return null;
    }

    private static void MoveTaskToBack()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            activity.Call<bool>("moveTaskToBack", true);
        }
#endif
    }
}
