using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private static TaskCompletionSource<bool> DisconnectUserTaskCompleter;
    public static async Task DisconnectUser()
    {
        DisconnectUserTaskCompleter = new TaskCompletionSource<bool>();
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("disconnectUser");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkDisconnectUser();
#endif
        await DisconnectUserTaskCompleter.Task;
    }

    private static void TriggerOnDisconnectUserCompleted()
    {
        DisconnectUserTaskCompleter.SetResult(true);
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnDisconnectUserCompleted(string _)
        {
            OctopusSDK.TriggerOnDisconnectUserCompleted();
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkDisconnectUser();
#endif    
}

