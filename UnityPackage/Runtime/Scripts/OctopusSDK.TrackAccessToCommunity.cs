using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static void TrackAccessToCommunity(bool hasAccess)
    {
#if UNITY_EDITOR
        MockBackend.TrackAccessToCommunity(hasAccess);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("trackAccessToCommunity", hasAccess);
        }
#elif UNITY_IOS
        OctopusSdkTrackAccessToCommunity(hasAccess);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkTrackAccessToCommunity(bool hasAccess);
#endif
}
