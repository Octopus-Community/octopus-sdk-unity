using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID
using Firebase.Extensions;
#endif

/// <summary>One persistent token source shared by the shell and the legacy push scene.</summary>
public sealed class OctopusSamplePushTokenSource : MonoBehaviour
{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private static bool _started;
#endif
#if UNITY_ANDROID
    private static System.Threading.Tasks.Task<Firebase.DependencyStatus> _dependencies;

    // The legacy scene also needs Firebase for notification taps. Share the dependency check
    // when that scene is the entry point and both initialisation paths start together.
    public static System.Threading.Tasks.Task<Firebase.DependencyStatus> CheckFirebaseDependencies()
    {
        if (_dependencies == null)
            _dependencies = Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        return _dependencies;
    }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
    private bool _subscribed;
#endif

    public static void EnsureExists()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (_started) return;
        _started = true;
        var host = new GameObject("OctopusSamplePushTokenSource");
        DontDestroyOnLoad(host);
        host.AddComponent<OctopusSamplePushTokenSource>();
#endif
    }

    private void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        StartCoroutine(RequestIOSAuthorization());
#elif UNITY_ANDROID && !UNITY_EDITOR
        InitializeFirebaseForAndroid();
#endif
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_subscribed)
            Firebase.Messaging.FirebaseMessaging.TokenReceived -= OnTokenReceived;
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        _started = false;
#endif
    }

#if UNITY_IOS
    private System.Collections.IEnumerator RequestIOSAuthorization()
    {
        using (var request = new AuthorizationRequest(
            AuthorizationOption.Alert | AuthorizationOption.Sound | AuthorizationOption.Badge,
            registerForRemoteNotifications: true))
        {
            while (!request.IsFinished) yield return null;
            if (request.Granted && !string.IsNullOrEmpty(request.DeviceToken))
                OctopusSamplePushRegistration.Register(request.DeviceToken);
            else
                Debug.LogWarning("[Octopus Sample] iOS notification authorization denied or token unavailable.");
        }
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private void InitializeFirebaseForAndroid()
    {
        try { RequestAndroidNotificationPermission(); }
        catch (System.Exception)
        {
            Debug.LogWarning("[Octopus Sample] Notification permission request failed.");
        }

        CheckFirebaseDependencies().ContinueWithOnMainThread(task =>
        {
            if (this == null) return;
            if (task.IsCanceled || task.IsFaulted || task.Result != Firebase.DependencyStatus.Available)
            {
                Debug.LogWarning("[Octopus Sample] Could not resolve Firebase dependencies.");
                return;
            }

            Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
            _subscribed = true;
            // A stable token may not trigger TokenReceived on a cold start.
            Firebase.Messaging.FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(tokenTask =>
            {
                if (this == null || tokenTask.IsCanceled || tokenTask.IsFaulted ||
                    string.IsNullOrEmpty(tokenTask.Result)) return;
                var value = tokenTask.Result;
                OctopusMainThread.Post(() => OctopusSamplePushRegistration.Register(value));
            });
        });
    }

    private void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
    {
        var value = token.Token;
        OctopusMainThread.Post(() => OctopusSamplePushRegistration.Register(value));
    }

    private void RequestAndroidNotificationPermission()
    {
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            if (version.GetStatic<int>("SDK_INT") < 33) return;
        }
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            const string permission = "android.permission.POST_NOTIFICATIONS";
            if (activity.Call<int>("checkSelfPermission", permission) != 0)
                activity.Call("requestPermissions", new string[] { permission }, 0);
        }
    }
#endif
}
