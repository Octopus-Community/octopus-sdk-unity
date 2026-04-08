using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private static Func<Task<string>> TokenProvider;
    private static TaskCompletionSource<bool> ConnectUserTaskCompleter;
    public static async Task ConnectUser(string userId, string nickname, string bio, string picture, Func<Task<string>> tokenProvider)
    {
        TokenProvider = tokenProvider;
        ConnectUserTaskCompleter = new TaskCompletionSource<bool>();
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("connectUser", userId, nickname, bio, picture);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkConnectUser(userId, nickname, bio, picture);
#endif
        await ConnectUserTaskCompleter.Task;
    }

    private static void TriggerOnConnectUserCompleted()
    {
        ConnectUserTaskCompleter.SetResult(true);
    }

    private static async void TriggerOnTokenRequested()
    {
        if (TokenProvider != null)
        {
            string token = await TokenProvider.Invoke();
            SetUserToken(token);
        }
        else
        {
            throw new Exception("OctopusSDK requested a token but host app has not set a token provider function");
        }
    }

    private static void SetUserToken(string token)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setUserToken", token);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetUserToken(token);
#endif
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnTokenRequested(string _)
        {
            OctopusSDK.TriggerOnTokenRequested();
        }

        public void OnConnectUserCompleted(string _)
        {
            OctopusSDK.TriggerOnConnectUserCompleted();
        }
    }
    
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkConnectUser(string userId, string nickname, string bio, string picture);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetUserToken(string token);
#endif
}