// Push Notifications Example
//
// Handles Octopus push notifications symmetrically on iOS and Android:
//   tap -> read payload from your push library -> OctopusSDK.GetOctopusNotification(payload)
//        -> OctopusSDK.Open(notification)
//
// iOS uses Unity Mobile Notifications (no native AppController file, no Firebase required).
// Android uses Firebase Messaging (the standard FCM mechanism).

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID
using Firebase.Extensions;
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PushNotificationsExample : MonoBehaviour
{
    [SerializeField] Text Message;

#if UNITY_IOS && !UNITY_EDITOR
    // The deep link we last opened, so re-checking the responded notification
    // (on cold start and again when the app regains focus) doesn't reopen it.
    string _lastHandledDeepLink;
#endif

    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());
        OctopusSampleState.ReportInitialized("OctopusAuth");
        OctopusSDK.OnNotSeenNotificationsCount += OnNotSeenNotificationCount;
        RegisterForPushNotifications();
#if UNITY_IOS && !UNITY_EDITOR
        HandleRespondedNotification();
#endif
    }

    void OnDestroy()
    {
        OctopusSDK.OnNotSeenNotificationsCount -= OnNotSeenNotificationCount;
#if UNITY_IOS && !UNITY_EDITOR
        iOSNotificationCenter.OnRemoteNotificationReceived -= OnIOSRemoteNotification;
#endif
    }

    public void OpenOctopus() => OctopusSDK.Open();
    public void UpdateNotificationCount() => OctopusSDK.UpdateNotSeenNotificationsCount();

    public void OnNotSeenNotificationCount(int count)
    {
        Message.text = string.Format("There are {0} unseen notification(s)", count);
        Debug.Log(string.Format("There are {0} unseen notification(s)", count));
    }

    // Shared handler: detect Octopus notification and open it.
    void HandleTappedPayload(IDictionary<string, string> payload)
    {
        if (!OctopusSDK.IsOctopusNotification(payload)) return;
        var notification = OctopusSDK.GetOctopusNotification(payload);
#if UNITY_IOS && !UNITY_EDITOR
        // iOS keeps returning the same responded notification for the whole foreground
        // session, and we re-check it on focus — so skip a deep link we already opened.
        if (notification.DeepLink == _lastHandledDeepLink) return;
        _lastHandledDeepLink = notification.DeepLink;
#endif
        Debug.Log("Octopus notification tapped, DeepLink: " + notification.DeepLink);
        OctopusSDK.Open(notification);
    }

    void RegisterForPushNotifications()
    {
        // Shares the persistent subscription with the shell, regardless of scene load order.
        OctopusSamplePushTokenSource.EnsureExists();
#if UNITY_IOS && !UNITY_EDITOR
        iOSNotificationCenter.OnRemoteNotificationReceived += OnIOSRemoteNotification;
#elif UNITY_ANDROID && !UNITY_EDITOR
        InitializeFirebaseForAndroid();
#endif
    }

#if UNITY_IOS
    // Reads the notification the user tapped (the one that launched the app on a cold
    // start, or the one tapped while the app was backgrounded). Unity Mobile Notifications
    // does NOT raise OnRemoteNotificationReceived for a tap, so this is how taps are handled.
    void HandleRespondedNotification()
    {
        var responded = iOSNotificationCenter.GetLastRespondedNotification();
        if (responded != null) HandleTappedPayload(responded.UserInfo);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // A tap on a backgrounded notification resumes the app without raising
        // OnRemoteNotificationReceived, so re-check the responded notification on resume.
        if (hasFocus) HandleRespondedNotification();
#if !UNITY_EDITOR
        else _lastHandledDeepLink = null; // iOS clears the responded notification on background; allow the next tap.
#endif
    }

    void OnIOSRemoteNotification(iOSNotification notification)
    {
        // Fired only when a remote notification ARRIVES while the app is in the foreground.
        // Taps are handled by HandleRespondedNotification (cold start + OnApplicationFocus).
        HandleTappedPayload(notification.UserInfo);
    }
#endif

#if UNITY_ANDROID
    void InitializeFirebaseForAndroid()
    {
        OctopusSamplePushTokenSource.CheckFirebaseDependencies().ContinueWithOnMainThread(task => {
            if (task.IsCanceled || task.IsFaulted || task.Result != Firebase.DependencyStatus.Available)
            {
                Debug.LogError("[Octopus Sample] Could not resolve Firebase dependencies.");
                return;
            }
            Firebase.Messaging.FirebaseMessaging.MessageReceived += OnMessageReceived;
        });
    }

    void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs e)
    {
        if (e.Message.NotificationOpened)
            HandleTappedPayload(e.Message.Data);
    }
#endif
}
