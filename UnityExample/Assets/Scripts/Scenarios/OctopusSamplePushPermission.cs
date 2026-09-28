using System;

/// <summary>
/// Decides when the sample asks the OS for notification permission (#233).
///
/// Not at launch: the iOS prompt used to appear over the first screen, before the tester had
/// chosen a profile or run anything. The Android sample asks once the SDK is initialised
/// (`MainActivity`, on `isSdkInitialized`), and this does the same, with the Push registration
/// switch as the second condition — a device whose tokens stay in the app has no reason to ask.
/// So the request goes out the first time both hold: right after the first Start in
/// Configuration, at launch on a later run that replays a saved profile, or when the switch is
/// turned back on. At most once per process; the OS itself only prompts once per install.
/// </summary>
public static class OctopusSamplePushPermission
{
    private static Action _request;
    private static bool _requested;

    static OctopusSamplePushPermission()
    {
        OctopusSampleState.Changed += Evaluate;
    }

    /// <summary>Whether the platform request has already been made in this process.</summary>
    public static bool Requested { get { return _requested; } }

    /// <summary>
    /// Registers the platform request (the iOS authorization, the Android 13 runtime permission)
    /// and fires it now if the conditions already hold, otherwise the first time they do.
    /// </summary>
    public static void RequestWhenReady(Action request)
    {
        _request = request;
        Evaluate();
    }

    /// <summary>Re-checks the conditions; called on every sample-state change and switch flip.</summary>
    public static void Evaluate()
    {
        if (_requested || _request == null) return;
        if (!OctopusSampleState.IsInitialized || !OctopusSampleFeatureToggles.PushRegistration) return;
        // The flag goes up before the call so a re-entrant Changed raised by the request itself
        // cannot fire it twice. A request that throws never reached the OS, so it is put back and
        // the next state change or switch flip tries again, instead of being dropped silently.
        _requested = true;
        var request = _request;
        _request = null;
        try
        {
            request();
        }
        catch (Exception e)
        {
            _requested = false;
            if (_request == null) _request = request;
            UnityEngine.Debug.LogWarning("Octopus sample: the notification permission request failed (" +
                e.GetType().Name + "); it will be retried on the next state change.");
        }
    }

    /// <summary>Forgets the pending request between EditMode tests.</summary>
    public static void Reset()
    {
        _request = null;
        _requested = false;
    }
}
