using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Marshals work onto the Unity main thread for code running off it — notably
/// <see cref="OctopusSDK.OnOctopusEvent"/> handlers, which fire on a background thread.
/// Posted actions run on the next <c>Update()</c>.
///
/// NOTE: while the Octopus UI is foregrounded on Android the player loop is paused, so posted
/// actions run when the user returns to the game. Do data/backend work in the event handler;
/// post only the Unity-side refresh.
/// </summary>
public sealed class OctopusMainThread : MonoBehaviour
{
    static OctopusMainThread _instance;
    static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

    /// <summary>Queue an action to run on the Unity main thread (next Update). Thread-safe.</summary>
    public static void Post(Action action)
    {
        if (action != null) _queue.Enqueue(action);
    }

    /// <summary>Create the singleton drainer if absent. Called on the main thread at Initialize.</summary>
    internal static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject("OctopusMainThread");
        _instance = go.AddComponent<OctopusMainThread>();
    }

    void Awake()
    {
        // Persists the drainer in Play mode / on device. Done in Awake (not EnsureExists) because
        // DontDestroyOnLoad is illegal in edit mode, and AddComponent does not invoke Awake there.
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        while (_queue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
