#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Editor-only stand-in for the native bridge. Every wrapper routes here in-Editor.
    /// Records calls, drives the overlay, and resolves async operations so nothing hangs.
    /// </summary>
    internal static class MockBackend
    {
        internal static void Initialize(string apiKey, ConnectionMode mode)
        {
            // An OctopusMockSettings asset, if present, seeds the toggles. Otherwise keep
            // whatever the host set in code (OctopusSDK.Mock.Enabled / .ShowOverlay) — defaults
            // are enabled + overlay on. Set those BEFORE Initialize for them to take effect.
            var settings = OctopusMockSettings.Instance;
            if (settings != null)
            {
                Mock.Enabled = settings.EnabledByDefault;
                Mock.ShowOverlay = settings.ShowOverlay;
            }
            // Note: when Enabled is false, Record() suppresses, so this Initialize
            // call won't appear in Mock.Calls — intentional (mirrors a no-op SDK).
            Mock.Record("Initialize", apiKey, mode.Mode);
            // Create the overlay whenever mock is on; ShowOverlay (checked in OnGUI) governs
            // whether it actually draws, so it can be toggled live during Play.
            if (Mock.Enabled) OctopusMockOverlay.EnsureExists();
        }

        internal static void Open(OctopusNotification notification)
        {
            Mock.Record("Open", notification?.DeepLink ?? "");
            if (Mock.Enabled) Mock.CurrentScreen = "Main feed";
        }

        internal static void OpenGroup(string groupId)
        {
            Mock.Record("OpenGroup", groupId ?? "");
            if (Mock.Enabled) Mock.CurrentScreen = $"Group {groupId}";
        }

        internal static void OpenPost(string postId)
        {
            Mock.Record("OpenPost", postId ?? "");
            if (Mock.Enabled) { Mock.LastOpenedPost = postId; Mock.CurrentScreen = $"Post {postId}"; }
        }

        internal static void OpenCreatePost(OctopusPrefilledPost prefilled)
        {
            Mock.Record("OpenCreatePost", prefilled?.Text ?? "", prefilled?.TopicId ?? "");
            if (Mock.Enabled) { Mock.LastPrefilledPost = prefilled; Mock.CurrentScreen = "Create post"; }
        }

        internal static async void ConnectUser(string userId, string nickname, string bio, string picture)
        {
            Mock.Record("ConnectUser", userId, nickname);
            // Mirror the real flow: request a token from the host, then complete.
            if (TokenProvider != null)
            {
                try { await TokenProvider.Invoke(); }
                catch (Exception e) { Debug.LogError($"[Octopus Mock] tokenProvider threw: {e}"); }
            }
            TriggerOnConnectUserCompleted();
        }

        internal static void DisconnectUser()
        {
            Mock.Record("DisconnectUser");
            TriggerOnDisconnectUserCompleted();
        }

        internal static void UpdateNotSeenNotificationsCount()
        {
            Mock.Record("UpdateNotSeenNotificationsCount");
            var settings = OctopusMockSettings.Instance;
            TriggerOnNotSeenNotificationsCount(settings != null ? settings.InitialNotSeenCount : 0);
        }

        internal static void RegisterNotificationsToken(string token) =>
            Mock.Record("RegisterNotificationsToken", token ?? "");

        internal static void Track(string name, IDictionary<string, string> properties)
        {
            Mock.Record("Track", name, properties == null ? 0 : properties.Count);
            if (Mock.Enabled) Mock.LastTrackedEvent = name;
        }

        internal static void TrackAccessToCommunity(bool hasAccess) =>
            Mock.Record("TrackAccessToCommunity", hasAccess);

        internal static void OverrideDefaultLocale(string languageCode) =>
            Mock.Record("OverrideDefaultLocale", languageCode ?? "");

        internal static void SetUrlInterceptionEnabled(bool enabled) =>
            Mock.Record("SetUrlInterceptionEnabled", enabled);

        internal static void OpenUrlInOctopus(string url) =>
            Mock.Record("OpenUrlInOctopus", url ?? "");

        // Theme setters — recorded as pass-throughs (no overlay effect).
        // Note: the color-scheme setters record unconditionally; on device a null
        // colorScheme is a no-op, so the mock over-records that (null-arg) case.
        internal static void SetLightColorScheme() => Mock.Record("SetLightColorScheme");
        internal static void SetDarkColorScheme() => Mock.Record("SetDarkColorScheme");
        internal static void SetLogo() => Mock.Record("SetLogo");
        internal static void SetAppName(string appName) => Mock.Record("SetAppName", appName ?? "");
        internal static void SetNavBarUsesPrimaryColor(bool usesPrimary) => Mock.Record("SetNavBarUsesPrimaryColor", usesPrimary);
        internal static void SetColorSchemeType(int colorSchemeType) => Mock.Record("SetColorSchemeType", colorSchemeType);
        internal static void SetForcedOrientation(int forcedOrientation) => Mock.Record("SetForcedOrientation", forcedOrientation);
        internal static void SetFonts() => Mock.Record("SetFonts");

        internal static void SyncFollowGroups(
            IList<OctopusSyncFollowGroupAction> actions,
            Action<IList<OctopusSyncFollowGroupResult>> onCompleted)
        {
            Mock.Record("SyncFollowGroups", actions == null ? 0 : actions.Count);
            var results = new List<OctopusSyncFollowGroupResult>();
            if (actions != null)
                foreach (var a in actions)
                    results.Add(new OctopusSyncFollowGroupResult { GroupId = a.GroupId });
            onCompleted?.Invoke(results);
            TriggerOnGroupsChanged(SeedGroups());
        }

        internal static void FetchGroups(Action<IList<OctopusGroup>> onCompleted)
        {
            Mock.Record("FetchGroups");
            onCompleted?.Invoke(SeedGroups());
        }

        static IList<OctopusGroup> SeedGroups() =>
            OctopusMockSettings.Instance == null
                ? new List<OctopusGroup>()
                : new List<OctopusGroup>(OctopusMockSettings.Instance.SeedGroups);
    }
}
#endif
