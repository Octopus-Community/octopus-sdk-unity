using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static void Open(OctopusNotification notification = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("open", notification?.DeepLink ?? "");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // On iOS, notification navigation is handled natively: the UNNotificationResponse
        // is stored by the developer's AppController (via OctopusNotificationHelper) and
        // passed to OctopusHomeScreen via a binding. The C# notification data is not needed.
        OctopusSdkOpen("");
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkOpen(string postId);
#endif
}

