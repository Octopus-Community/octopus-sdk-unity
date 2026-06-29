using System;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Raised when the user does something inside the Octopus UI (post/comment/reaction created,
    /// gamification points, screen displayed, …) — for analytics, gamification, or backend sync.
    /// See <see cref="OctopusEvent"/> for the full catalogue.
    ///
    /// IMPORTANT: handlers are invoked on a dedicated **background thread**, in emission order,
    /// on **both iOS and Android** (real-time, even while the Octopus UI is on screen). They must
    /// be thread-safe and must NOT call Unity APIs directly. To touch Unity (UI, scene), use
    /// <see cref="OctopusMainThread.Post"/>, which runs the action on the Unity main thread.
    /// (In the Editor mock, events are raised synchronously on the calling thread.)
    /// </summary>
    public static event Action<OctopusEvent> OnOctopusEvent;

    internal static void TriggerOnOctopusEvent(OctopusEvent e) => OnOctopusEvent?.Invoke(e);

    public partial class OctopusChannel : MonoBehaviour
    {
        // iOS path: native forwards events here via UnitySendMessage (Unity main thread). Hand to
        // the shared dispatcher so delivery is uniform with Android (background thread, ordered).
        public void OnOctopusEventJson(string json) => OctopusEventDispatcher.Enqueue(json);
    }
}
