using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static event Action<int> OnNotSeenNotificationsCount;

    public static void UpdateNotSeenNotificationsCount()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("updateNotSeenNotificationsCount");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkUpdateNotSeenNotificationsCount();
#endif
    }

    static void TriggerOnNotSeenNotificationsCount(int count)
    {
        OnNotSeenNotificationsCount?.Invoke(count);
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnNotSeenNotificationsCount(string count)
        {
            OctopusSDK.TriggerOnNotSeenNotificationsCount(int.Parse(count));
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkUpdateNotSeenNotificationsCount();
#endif 
}