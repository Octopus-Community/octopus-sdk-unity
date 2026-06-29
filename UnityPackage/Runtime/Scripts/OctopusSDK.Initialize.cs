using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Initializes the SDK.
    /// </summary>
    /// <param name="apiKey">The API key identifying your community.</param>
    /// <param name="mode">The connection mode (SSO or Octopus auth).</param>
    /// <param name="apiServerHost">
    /// Optional custom gRPC server host (e.g. a staging endpoint like "api-demo2.8pus.io").
    /// Leave null/empty to target the default Octopus production endpoint. Host only — no scheme,
    /// port, or path (use <paramref name="apiServerPort"/> for the port). Traffic is always over TLS.
    /// </param>
    /// <param name="apiServerPort">Port for the custom server host. Defaults to 443. Ignored when no host is set.</param>
    public static void Initialize(string apiKey, ConnectionMode mode, string apiServerHost = null, int apiServerPort = 443)
    {
        OctopusChannel.Initialize();
        OctopusMainThread.EnsureExists();
        string host = apiServerHost ?? "";
#if UNITY_EDITOR
        MockBackend.Initialize(apiKey, mode);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("initialize", apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray, host, apiServerPort);
        }
        RegisterBridgeListener();
#elif UNITY_IOS
        RegisterIosBridgeCallbacks();
        OctopusSdkInitialize(apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray, mode.AppManagedFieldsAsIntArray.Length, host, apiServerPort);
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
    private static extern void OctopusSdkInitialize(string apiKey, string mode, int[] appManagedFields, int appManagedFieldsLength, string apiServerHost, int apiServerPort);
#endif
}
