#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Editor-only mock control &amp; inspection surface. Lets you iterate and write
    /// automated EditMode tests without building for a device. Compiled out of
    /// player builds.
    /// </summary>
    public static class Mock
    {
        public readonly struct Call
        {
            public readonly string Method;
            public readonly object[] Args;
            public Call(string method, object[] args) { Method = method; Args = args ?? Array.Empty<object>(); }
            public override string ToString() =>
                Args.Length == 0 ? $"{Method}()" : $"{Method}({string.Join(", ", Args)})";
        }

        static readonly List<Call> _calls = new List<Call>();

        /// <summary>Ordered log of every recorded SDK call since the last Reset().</summary>
        public static IReadOnlyList<Call> Calls => _calls;

        /// <summary>Master switch. Gates recording/overlay/auto-events. Async ops still complete when false.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Whether the in-Editor overlay is drawn. Toggle any time (also seeded from settings at Initialize).</summary>
        public static bool ShowOverlay { get; set; } = true;

        /// <summary>The simulated screen the overlay shows. null = nothing opened.</summary>
        public static string CurrentScreen { get; set; }

        public static void Record(string method, params object[] args)
        {
            if (!Enabled)
            {
                Debug.Log($"[Octopus Mock] {method} suppressed (mock disabled)");
                return;
            }
            var call = new Call(method, args);
            _calls.Add(call);
            Debug.Log($"[Octopus Mock] {call}");
        }

        /// <summary>Clears the call log, current screen, and typed accessors. Call between tests.</summary>
        public static void Reset()
        {
            _calls.Clear();
            CurrentScreen = null;
            LastOpenedPost = null;
            LastPrefilledPost = null;
            LastTrackedEvent = null;
        }

        /// <summary>Most recent recorded call with the given method name, or null.</summary>
        public static Call? LastCall(string method)
        {
            for (int i = _calls.Count - 1; i >= 0; i--)
                if (_calls[i].Method == method) return _calls[i];
            return null;
        }

        // Typed convenience accessors (set by MockBackend in later tasks).
        public static string LastOpenedPost { get; set; }
        public static OctopusPrefilledPost LastPrefilledPost { get; set; }
        public static string LastTrackedEvent { get; set; }

        // Drivers below simulate native callbacks on demand. They fire unconditionally,
        // independent of Enabled — Enabled gates recording/overlay/auto-emit, but a driver
        // is an explicit "fire this event now" command (used by tests and the overlay buttons).

        /// <summary>Simulate a not-seen-notifications count update from native.</summary>
        public static void EmitNotSeenCount(int count) => OctopusSDK.TriggerOnNotSeenNotificationsCount(count);

        /// <summary>Simulate a groups-changed callback from native.</summary>
        public static void EmitGroupsChanged(IList<OctopusGroup> groups) =>
            OctopusSDK.TriggerOnGroupsChanged(groups);

        /// <summary>Simulate the SDK requesting login.</summary>
        public static void EmitLoginRequired() => OctopusSDK.TriggerOnLoginRequired();

        /// <summary>Simulate a CTA tap that navigates to one of your client objects.</summary>
        public static void EmitNavigateToClientObject(string clientObjectId) =>
            OctopusSDK.TriggerOnNavigateToClientObject(clientObjectId);

        /// <summary>Simulate the user editing a profile field inside Octopus.</summary>
        public static void EmitModifyUser(ProfileField? field) => OctopusSDK.TriggerOnModifyUser(field);
    }
}
#endif
