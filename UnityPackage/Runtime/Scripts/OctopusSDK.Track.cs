using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static void Track(string name, IDictionary<string,string> properties)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("track", name, properties.Keys.ToArray(), properties.Values.ToArray());
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkTrack(name, properties.Keys.ToArray(), properties.Values.ToArray(), properties.Count);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkTrack(string eventName, string[] keys, string[] values, int count);
#endif
}
