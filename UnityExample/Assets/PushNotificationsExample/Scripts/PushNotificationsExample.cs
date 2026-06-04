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
#if UNITY_IOS && !UNITY_EDITOR
        StartCoroutine(RequestIOSAuthorization());
        iOSNotificationCenter.OnRemoteNotificationReceived += OnIOSRemoteNotification;
#elif UNITY_ANDROID && !UNITY_EDITOR
        InitializeFirebaseForAndroid();
#endif
    }

#if UNITY_IOS
    System.Collections.IEnumerator RequestIOSAuthorization()
    {
        using (var req = new AuthorizationRequest(
            AuthorizationOption.Alert | AuthorizationOption.Sound | AuthorizationOption.Badge,
            registerForRemoteNotifications: true))
        {
            while (!req.IsFinished) yield return null;
            if (req.Granted && !string.IsNullOrEmpty(req.DeviceToken))
                OctopusSDK.RegisterNotificationsToken(req.DeviceToken);
            else
                Debug.LogWarning("iOS notification authorization denied or token unavailable. Error: " + req.Error);
        }
    }

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
        try { RequestAndroidNotificationPermission(); }
        catch (System.Exception e) { Debug.LogWarning("Notification permission request failed: " + e.Message); }

        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            if (task.Result == Firebase.DependencyStatus.Available)
            {
                Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
                Firebase.Messaging.FirebaseMessaging.MessageReceived += OnMessageReceived;
            }
            else Debug.LogError(string.Format("Could not resolve Firebase dependencies: {0}", task.Result));
        });
    }

    void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
    {
        // Logged so you can copy it for a Firebase Console "Send test message" push.
        Debug.Log("FCM registration token: " + token.Token);
        OctopusSDK.RegisterNotificationsToken(token.Token);
    }

    void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs e)
    {
        if (e.Message.NotificationOpened)
            HandleTappedPayload(e.Message.Data);
    }

    void RequestAndroidNotificationPermission()
    {
        if (GetAndroidSDKInt() >= 33)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                const string permission = "android.permission.POST_NOTIFICATIONS";
                if (activity.Call<int>("checkSelfPermission", permission) != 0)
                    activity.Call("requestPermissions", new string[] { permission }, 0);
            }
        }
    }

    int GetAndroidSDKInt()
    {
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            return version.GetStatic<int>("SDK_INT");
    }
#endif
}
