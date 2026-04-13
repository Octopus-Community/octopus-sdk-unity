// Push Notifications Example
//
// This example shows how to handle Octopus push notifications on both Android and iOS.
//
// IMPORTANT — iOS requires an additional native file:
//   Plugins/iOS/OctopusAppController.mm (included in this sample)
//
// iOS flow:
//   1. OctopusAppController.mm intercepts every UNNotificationResponse at the native level
//      and stores it via OctopusNotificationHelper.handleNotificationResponse().
//   2. The SDK fires OnNotificationTapped for every Octopus notification tap
//      (cold start, background, or foreground). No Firebase dependency.
//   3. Call OctopusSDK.Open() in your handler — OctopusHomeScreen picks up the stored
//      response and navigates to the correct screen.
//
// Android flow:
//   1. Firebase fires OnMessageReceived with NotificationOpened=true.
//   2. Parse the notification and call OctopusSDK.Open(notification) — the DeepLink
//      is passed to the native Android SDK which navigates via Jetpack Compose NavHost.

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

#if UNITY_ANDROID
using Firebase.Extensions;
#endif

using UnityEngine;
using UnityEngine.UI;

public class PushNotificationsExample : MonoBehaviour
{
    [SerializeField] Text Message;

    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());
        OctopusSDK.OnNotSeenNotificationsCount += OnNotSeenNotificationCount;

        // iOS: Fires when the user taps ANY Octopus notification (cold start, background,
        // or foreground). The native layer already captured the UNNotificationResponse.
        // Call Open() to navigate to the notification's content.
        OctopusSDK.OnNotificationTapped += OnOctopusNotificationTapped;

        RegisterForPushNotifications();
    }

    void OnDestroy()
    {
        OctopusSDK.OnNotificationTapped -= OnOctopusNotificationTapped;
        OctopusSDK.OnNotSeenNotificationsCount -= OnNotSeenNotificationCount;
    }

    void OnOctopusNotificationTapped()
    {
        OctopusSDK.Open();
    }

    public void OpenOctopus()
    {
        OctopusSDK.Open();
    }

    public void UpdateNotificationCount()
    {
        OctopusSDK.UpdateNotSeenNotificationsCount();
    }

    public void OnNotSeenNotificationCount(int count)
    {
        Message.text = string.Format("There are {0} unseen notification(s)", count);
        Debug.Log(string.Format("There are {0} unseen notification(s)", count));
    }

    void RegisterForPushNotifications()
    {
#if UNITY_IOS && !UNITY_EDITOR
        StartCoroutine(RequestIOSAuthorization());
#elif UNITY_ANDROID && !UNITY_EDITOR
        InitializeFirebaseForAndroid();
#endif
    }

    // ──────────────────────────────────────────────────────────────────────
    // iOS — Uses Unity Mobile Notifications for permission + APNs token.
    //        No Firebase dependency.
    // ──────────────────────────────────────────────────────────────────────

#if UNITY_IOS
    System.Collections.IEnumerator RequestIOSAuthorization()
    {
        using (var req = new AuthorizationRequest(
            AuthorizationOption.Alert | AuthorizationOption.Sound | AuthorizationOption.Badge,
            registerForRemoteNotifications: true))
        {
            while (!req.IsFinished)
                yield return null;

            if (req.Granted && !string.IsNullOrEmpty(req.DeviceToken))
            {
                Debug.Log("APNs Device Token: " + req.DeviceToken);
                OctopusSDK.RegisterNotificationsToken(req.DeviceToken);
            }
            else
            {
                Debug.LogWarning("Notification authorization denied or token unavailable. Error: " + req.Error);
            }
        }
    }
#endif

    // ──────────────────────────────────────────────────────────────────────
    // Android — Uses Firebase Messaging for FCM token and tap detection.
    //           FCM is the standard push mechanism on Android.
    // ──────────────────────────────────────────────────────────────────────

#if UNITY_ANDROID
    void InitializeFirebaseForAndroid()
    {
        try
        {
            RequestAndroidNotificationPermission();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Notification permission request failed: " + e.Message);
        }

        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
                Firebase.Messaging.FirebaseMessaging.MessageReceived += OnMessageReceived;
                Debug.Log("Firebase ready (Android)");
            }
            else
            {
                Debug.LogError(string.Format("Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
    }

    void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
    {
        Debug.Log("FCM Token: " + token.Token);
        OctopusSDK.RegisterNotificationsToken(token.Token);
    }

    void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs e)
    {
        if (OctopusSDK.IsOctopusNotification(e.Message.Data) && e.Message.NotificationOpened)
        {
            var notification = OctopusSDK.GetOctopusNotification(e.Message.Data);
            Debug.Log(string.Format("Notification tapped, DeepLink: {0}", notification.DeepLink));
            OctopusSDK.Open(notification);
        }
    }

    void RequestAndroidNotificationPermission()
    {
        if (GetAndroidSDKInt() >= 33)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                string permission = "android.permission.POST_NOTIFICATIONS";
                int granted = activity.Call<int>("checkSelfPermission", permission);

                if (granted != 0)
                {
                    activity.Call("requestPermissions", new string[] { permission }, 0);
                }
            }
        }
    }

    int GetAndroidSDKInt()
    {
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            return version.GetStatic<int>("SDK_INT");
        }
    }
#endif
}
