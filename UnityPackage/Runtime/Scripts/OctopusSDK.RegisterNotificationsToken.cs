using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static void RegisterNotificationsToken(string token)
    {
#if UNITY_EDITOR
        MockBackend.RegisterNotificationsToken(token);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("registerNotificationsToken", token);
        }
#elif UNITY_IOS
        OctopusSdkRegisterNotificationsToken(token);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkRegisterNotificationsToken(string token);
#endif    
}

