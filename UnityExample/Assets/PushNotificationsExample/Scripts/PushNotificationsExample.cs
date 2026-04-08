#if !IOS_SIMULATOR
using Firebase.Extensions;
#endif // !IOS_SIMULATOR

using UnityEngine;
using UnityEngine.UI;

public class PushNotificationsExample : MonoBehaviour
{
#if !IOS_SIMULATOR
     Firebase.FirebaseApp app;
#endif // !IOS_SIMULATOR

    OctopusNotification notification = null;
    [SerializeField] Text Message;

    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());
        OctopusSDK.OnNotSeenNotificationsCount += OnNotSeenNotificationCount;
        InitializeFirebase();
    }

    public void OpenOctopus()
    {
        OctopusSDK.Open(notification);
        notification = null;
    }

    public void UpdateNotificationCount()
    {
        OctopusSDK.UpdateNotSeenNotificationsCount();
    }

#if !IOS_SIMULATOR
    public void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
    {
        Debug.Log("Received Registration Token: " + token.Token);
        OctopusSDK.RegisterNotificationsToken(token.Token);
    }

    public void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs e)
    {
        Debug.Log("From: " + e.Message.From);
        Debug.Log("Message ID: " + e.Message.MessageId);
        Debug.Log("Message Type: " + e.Message.MessageType);
        if (OctopusSDK.IsOctopusNotification(e.Message.Data))
        {
            Message.text = "An  Octopus notification was received";
            notification = OctopusSDK.GetOctopusNotification(e.Message.Data);
            Message.text = string.Format("Octopus notification received, title: {0}", notification.Title);
            Debug.Log(string.Format("Octopus Nottification Title: {0}", notification.Title));
            Debug.Log(string.Format("Octopus Nottification Body: {0}", notification.Body));
            if (e.Message.NotificationOpened)
            {
                Debug.Log(string.Format("App was open via notification, DeepLink: {0}", notification.DeepLink));
                OpenOctopus();
            }
        }
        else
        {
            Message.text = "Push Notification received but not from Octopus";
            Debug.Log("Message is not an octopus notification ");
        }
    }
#endif // !IOS_SIMULATOR    

    public void OnNotSeenNotificationCount(int count)
    {
        Message.text = string.Format("There are {0} unseen notification(s)", count);
        Debug.Log(string.Format("There are {0} unseen notification(s)", count));
    }

    public void InitializeFirebase()
    {
#if !IOS_SIMULATOR        
        RequestNotificationPermission();
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                app = Firebase.FirebaseApp.DefaultInstance;
                Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
                Firebase.Messaging.FirebaseMessaging.MessageReceived += OnMessageReceived;
                Debug.Log("firebase is ready to be used");
            }
            else
            {
                Debug.LogError(string.Format("Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
#endif // !IOS_SIMULATOR        
    }

    public void RequestNotificationPermission()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (GetAndroidSDKInt() >= 33)
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (var permissionChecker = new AndroidJavaClass("androidx.core.content.ContextCompat"))
                using (var activityCompat = new AndroidJavaClass("androidx.core.app.ActivityCompat"))
                {
                    string permission = "android.permission.POST_NOTIFICATIONS";

                    int granted = permissionChecker.CallStatic<int>(
                        "checkSelfPermission",
                        context,
                        permission);

                    if (granted != 0)
                    {
                        activityCompat.CallStatic(
                            "requestPermissions",
                            activity,
                            new string[] { permission },
                            0);
                    }
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
}
