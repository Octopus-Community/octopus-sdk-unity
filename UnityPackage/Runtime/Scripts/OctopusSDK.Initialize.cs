using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    public static void Initialize(string apiKey, ConnectionMode mode)
    {
        OctopusChannel.Initialize();
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("initialize", apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkInitialize(apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray, mode.AppManagedFieldsAsIntArray.Length);
#endif
        SetUnityTheme();
    }

    // OctopusChannel enables communication from native code to C#
    // by creating a permanent object with "DontDestroyOnLoad"
    // this object will receive method calls from the native code
    // via the API "UnitySendMessage"
    // https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html
    public partial class OctopusChannel : MonoBehaviour
    {
        public const string OctopusChannelName = "OctopusChannel";
        public static void Initialize()
        {
            new GameObject(OctopusChannelName).AddComponent<OctopusChannel>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            gameObject.name = OctopusChannelName;
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkInitialize(string apiKey, string mode, int[] appManagedFields, int appManagedFieldsLength);
#endif
}
