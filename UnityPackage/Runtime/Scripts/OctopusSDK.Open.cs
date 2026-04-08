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
        OctopusSdkOpen(notification?.PostId ?? "");
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkOpen(string postId);
#endif
}

