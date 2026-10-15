using System;
using UnityEngine;

/// <summary>
/// The sample's Play In-App Update check (pm-tools <c>shared/design/samples/AUTO_UPDATE.md</c>).
/// Android player only: in the Editor and on iOS (TestFlight prompts by itself) the status stays
/// <see cref="OctopusSampleUpdateState.Unsupported"/> and nothing is shown. Sample code only — the
/// SDK package never asks Play anything.
///
/// The check runs once at launch and again when the app resumes after an update flow it started;
/// it is silent on failure. The flow itself only ever starts from a tap (<see cref="StartUpdate"/>).
/// </summary>
public class OctopusSampleAppUpdate : MonoBehaviour
{
    /// <summary>PlayerPrefs key holding the versionCode whose launch announcement was dismissed.</summary>
    public const string DismissedVersionCodeKey = "octopus.sample.update.dismissed";

    private static OctopusSampleAppUpdate _instance;
    private static OctopusSampleUpdateStatus _status = DefaultStatus();
    private readonly object _gate = new object();
    private Action _pending;
    private bool _flowStarted;

    /// <summary>Raised on the Unity main thread whenever <see cref="Status"/> changes.</summary>
    public static event Action Changed;

    /// <summary>The latest known result.</summary>
    public static OctopusSampleUpdateStatus Status { get { return _status; } }

    /// <summary>Whether this player can ask Play at all.</summary>
    public static bool IsSupported
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    private static OctopusSampleUpdateStatus DefaultStatus()
    {
        return IsSupported ? OctopusSampleUpdateStatus.Unknown : OctopusSampleUpdateStatus.Unsupported;
    }

    /// <summary>Sets the status and notifies listeners. Public for the EditMode suite.</summary>
    public static void Publish(OctopusSampleUpdateStatus status)
    {
        _status = status ?? DefaultStatus();
        var handler = Changed;
        if (handler != null) handler();
    }

    /// <summary>Restores the platform default. For the EditMode suite.</summary>
    public static void ResetForTests() { _status = DefaultStatus(); }

    /// <summary>The versionCode whose announcement the tester dismissed, or 0.</summary>
    public static int DismissedVersionCode
    {
        get { return PlayerPrefs.GetInt(DismissedVersionCodeKey, 0); }
    }

    /// <summary>Dismisses the launch announcement for the build currently offered.</summary>
    public static void DismissAnnouncement()
    {
        PlayerPrefs.SetInt(DismissedVersionCodeKey, _status.AvailableVersionCode);
        PlayerPrefs.Save();
        Publish(_status);
    }

    /// <summary>Creates the checker and runs the launch check. Play mode, Android player only.</summary>
    public static void EnsureStarted()
    {
        if (!IsSupported || !Application.isPlaying || _instance != null) return;
        var host = new GameObject("OctopusSampleAppUpdate");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<OctopusSampleAppUpdate>();
        Check();
    }

    /// <summary>Runs a check now (launch, manual tap, or return from the update flow).</summary>
    public static void Check()
    {
        if (!IsSupported || _instance == null) return;
        Publish(OctopusSampleUpdateStatus.Checking);
        _instance.Ask();
    }

    /// <summary>
    /// The card's single button: starts Play's IMMEDIATE flow when an update is installable,
    /// otherwise checks again.
    /// </summary>
    public static void StartUpdate()
    {
        if (_status.State != OctopusSampleUpdateState.UpdateAvailable || _instance == null)
        {
            Check();
            return;
        }
        _instance.StartImmediate();
    }

    private void Update()
    {
        Action pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }
        if (pending != null) pending();
    }

    private void OnApplicationPause(bool paused)
    {
        // Re-check after the flow returns, so a cancelled update is offered again.
        if (paused || !_flowStarted) return;
        _flowStarted = false;
        Check();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Called from the Android main thread; hands the result to the Unity thread.</summary>
    private void Post(OctopusSampleUpdateStatus status)
    {
        lock (_gate) { _pending = () => Publish(status); }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string BridgeClass = "com.octopuscommunity.sdk.unity.sample.OctopusSampleAppUpdate";
    private const int NoErrorCode = int.MinValue;

    private sealed class Listener : AndroidJavaProxy
    {
        private readonly OctopusSampleAppUpdate _owner;

        public Listener(OctopusSampleAppUpdate owner) : base(BridgeClass + "$Listener") { _owner = owner; }

        // Names and signatures match the Java interface; AndroidJavaProxy dispatches by name.
        public void onResult(int availability, bool immediateAllowed, int availableVersionCode)
        {
            _owner.Post(OctopusSampleUpdateStatus.Of(availability, immediateAllowed, availableVersionCode));
        }

        public void onError(int errorCode)
        {
            _owner.Post(OctopusSampleUpdateStatus.StatusOf(errorCode == NoErrorCode ? (int?)null : errorCode));
        }
    }

    private void Ask()
    {
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass(BridgeClass))
                bridge.CallStatic("check", activity, new Listener(this));
        }
        catch (Exception)
        {
            Publish(OctopusSampleUpdateStatus.Failed(null));
        }
    }

    private void StartImmediate()
    {
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass(BridgeClass))
                _flowStarted = bridge.CallStatic<bool>("startImmediateUpdate", activity);
        }
        catch (Exception)
        {
            _flowStarted = false;
        }
        if (!_flowStarted) Check();
    }
#else
    private void Ask() { }

    private void StartImmediate() { }
#endif
}
