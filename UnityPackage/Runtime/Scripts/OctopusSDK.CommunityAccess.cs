using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Octopus A/B testing — community access control.
//
// Two related but distinct surfaces:
//   • HasAccessToCommunity (reactive, read-only): whether the current user can access community
//     features. Respects both the SDK's internal A/B test configuration AND any status set via
//     OverrideCommunityAccess. Mirrors the native reactive value (iOS @Published, Android Flow).
//   • OverrideCommunityAccess(bool): lets the host app take FULL control of access, bypassing the
//     SDK's internal A/B test config and the analytics-only TrackAccessToCommunity signal.
//
// Not to be confused with TrackAccessToCommunity (see OctopusSDK.TrackAccessToCommunity.cs), which
// is analytics-only: it records which A/B group the user is in but does NOT grant or restrict access.
public partial class OctopusSDK
{
    /// <summary>
    /// Whether the current user has access to the community features.
    ///
    /// Respects both the SDK's internal A/B test configuration and any status set via
    /// <see cref="OverrideCommunityAccess"/>. Updated from native; subscribe to
    /// <see cref="OnHasAccessToCommunityChanged"/> to be notified when it changes.
    /// </summary>
    public static bool HasAccessToCommunity { get; private set; }

    /// <summary>
    /// Raised whenever the community access status changes (mirrors the native reactive value).
    /// The current value is also cached in <see cref="HasAccessToCommunity"/> for late readers.
    /// </summary>
    public static event Action<bool> OnHasAccessToCommunityChanged;

    // Completion callbacks for in-flight OverrideCommunityAccess requests, keyed by request id.
    // Error callbacks reuse the shared _errorCallbacks registry (see OctopusSDK.SyncFollowGroups.cs).
    static readonly Dictionary<int, Action> _overrideAccessCallbacks = new Dictionary<int, Action>();

    /// <summary>
    /// Overrides the community access status managed by Octopus.
    ///
    /// This bypasses both the SDK's internal A/B test configuration <b>and</b> any status previously
    /// set via <see cref="TrackAccessToCommunity"/>. It explicitly determines whether the user can
    /// access the community features, and takes <b>full precedence</b> over all other access control
    /// mechanisms.
    ///
    /// Use this when you want Octopus to control access to the community directly, instead of (or in
    /// addition to) managing A/B test groups in your own app.
    ///
    /// After the override is applied, <see cref="HasAccessToCommunity"/> reflects the new value and
    /// <see cref="OnHasAccessToCommunityChanged"/> fires.
    /// </summary>
    /// <param name="hasAccess"><c>true</c> to grant access to the community, <c>false</c> to block it.</param>
    /// <param name="onCompleted">Optional callback invoked when the override has been applied.</param>
    /// <param name="onError">Optional callback invoked with an error message if the override fails.</param>
    public static void OverrideCommunityAccess(
        bool hasAccess,
        Action onCompleted = null,
        Action<string> onError = null)
    {
        int id = _nextRequestId++;
        _overrideAccessCallbacks[id] = onCompleted;
        if (onError != null) _errorCallbacks[id] = onError;
#if UNITY_EDITOR
        // Mock invokes onCompleted directly; the entries registered above are never
        // consumed in-Editor, so drop them to avoid unbounded growth across a session.
        _overrideAccessCallbacks.Remove(id);
        _errorCallbacks.Remove(id);
        MockBackend.OverrideCommunityAccess(hasAccess, onCompleted);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("overrideCommunityAccess", id, hasAccess);
        }
#elif UNITY_IOS
        OctopusSdkOverrideCommunityAccess(id, hasAccess);
#endif
    }

    static void TriggerOnHasAccessToCommunity(bool hasAccess)
    {
        HasAccessToCommunity = hasAccess;
        OnHasAccessToCommunityChanged?.Invoke(hasAccess);
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        // Reactive push: native emits "true"/"false" on subscription and on every change.
        public void OnHasAccessToCommunity(string value)
        {
            OctopusSDK.TriggerOnHasAccessToCommunity(value == "true");
        }

        // payload is "<requestId>\n" (no data — success carries only completion).
        public void OnOverrideCommunityAccessResult(string payload)
        {
            if (!SplitRequest(payload, out int id, out _)) return;
            if (_overrideAccessCallbacks.TryGetValue(id, out var cb))
            {
                _overrideAccessCallbacks.Remove(id); _errorCallbacks.Remove(id);
                cb?.Invoke();
            }
        }

        // payload is "<requestId>\n<message>".
        public void OnOverrideCommunityAccessError(string payload)
        {
            if (!SplitRequest(payload, out int id, out string msg)) return;
            _overrideAccessCallbacks.Remove(id);
            if (_errorCallbacks.TryGetValue(id, out var cb)) { _errorCallbacks.Remove(id); cb?.Invoke(msg); }
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OctopusSdkOverrideCommunityAccess(int requestId, bool hasAccess);
#endif
}
