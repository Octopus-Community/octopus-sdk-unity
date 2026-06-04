// Routes an Octopus notification TAP to the Push Notifications example scene.
//
// Why this shim exists in the example app:
//   - The app launches into MainMenu (build scene 0), which has no notification
//     handling — so a tap that opens the app from a closed/backgrounded state would
//     never be read there, and the user would just land on the menu.
//   - It only DETECTS the tap and loads the example scene; that scene initializes the
//     SDK and performs the actual navigation (iOS via GetLastRespondedNotification,
//     Android via Firebase's MessageReceived). We deliberately do NOT consume the
//     notification here so the scene's own handler still receives it.
//
// A real integration handles the tapped notification at its own startup/resume and does
// not need this scene-routing shim.
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

public class PushNotificationLauncher : MonoBehaviour
{
    const string PushScene = "PushNotificationsExample";
    static bool _created;
    bool _routing; // guard the 1-frame window between LoadScene and the scene becoming active.
#if UNITY_ANDROID
    string _lastRoutedMessageId; // don't yank the user back to the scene on a stale launch intent.
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_created) return;
        _created = true;
        var go = new GameObject("PushNotificationLauncher");
        go.AddComponent<PushNotificationLauncher>();
        DontDestroyOnLoad(go);
    }

    void Start() => RouteIfTapped();                                  // cold start
    void OnApplicationFocus(bool hasFocus) { if (hasFocus) RouteIfTapped(); } // resume

    void RouteIfTapped()
    {
        if (!WasOpenedFromOctopusNotification()) return;
        if (SceneManager.GetActiveScene().name == PushScene) { _routing = false; return; }
        if (_routing) return;
        _routing = true;
        // PushNotificationsExample.Start initializes the SDK; its push handler opens the deep link.
        SceneManager.LoadScene(PushScene);
    }

#if UNITY_IOS
    bool WasOpenedFromOctopusNotification()
    {
        var responded = iOSNotificationCenter.GetLastRespondedNotification();
        return responded != null && OctopusSDK.IsOctopusNotification(responded.UserInfo);
    }
#elif UNITY_ANDROID
    // FCM puts the data-payload keys (and google.message_id) into the launching Activity's
    // intent extras; the generated MessagingUnityPlayerActivity calls setIntent() so this is
    // also up to date after onNewIntent (a tap while the app is alive).
    bool WasOpenedFromOctopusNotification()
    {
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            if (activity == null) return false;
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                if (intent == null) return false;
                using (var extras = intent.Call<AndroidJavaObject>("getExtras"))
                {
                    if (extras == null) return false;
                    var isOctopus = extras.Call<string>("getString", "is_octopus_notification");
                    if (string.IsNullOrEmpty(isOctopus) || isOctopus.ToLower() != "true") return false;

                    // Route once per tap. Prefer the FCM message id; fall back to the link path.
                    var messageId = extras.Call<string>("getString", "google.message_id");
                    var key = string.IsNullOrEmpty(messageId)
                        ? extras.Call<string>("getString", "link_path")
                        : messageId;
                    if (!string.IsNullOrEmpty(key) && key == _lastRoutedMessageId) return false;
                    _lastRoutedMessageId = key;
                    return true;
                }
            }
        }
    }
#endif
}
#endif
