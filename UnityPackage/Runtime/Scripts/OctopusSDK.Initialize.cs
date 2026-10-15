using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Initializes the SDK.
    /// </summary>
    /// <param name="apiKey">The API key identifying your community.</param>
    /// <param name="mode">The required SSO connection mode.</param>
    /// <param name="apiServerHost">
    /// Optional custom gRPC server host (e.g. a staging endpoint like "api-demo2.8pus.io").
    /// Leave null/empty to target the default Octopus production endpoint. Host only — no scheme,
    /// port, or path (use <paramref name="apiServerPort"/> for the port). Traffic is always over TLS.
    /// </param>
    /// <param name="apiServerPort">Port for the custom server host. Defaults to 443. Ignored when no host is set.</param>
    public static void Initialize(string apiKey, ConnectionMode mode, string apiServerHost = null, int apiServerPort = 443)
    {
        Initialize(apiKey, mode, apiServerHost, apiServerPort, null);
    }

    /// <summary>
    /// Initializes the SDK with platform-specific options. Options are read during this call;
    /// later changes to the options object do not reconfigure an initialized SDK.
    /// </summary>
    /// <param name="apiKey">The API key identifying your community.</param>
    /// <param name="mode">The required SSO connection mode.</param>
    /// <param name="apiServerHost">Custom gRPC server host, or null/empty for the default endpoint.</param>
    /// <param name="apiServerPort">Custom server port. Ignored when no host is set.</param>
    /// <param name="options">Initialization options, or null to use their defaults.</param>
    public static void Initialize(string apiKey, ConnectionMode mode, string apiServerHost, int apiServerPort,
        OctopusInitializationOptions options)
    {
        if (mode == null) throw new System.ArgumentNullException("mode");
        TokenProvider = null;
        OctopusChannel.Initialize();
        OctopusMainThread.EnsureExists();
        ResetProfileObservation();
        ClearClientPostSession();
        ResetCommunityDataState(false);
        string host = apiServerHost ?? "";
        string initializationOptionsJson = InitializationOptionsToJson(options);
#if UNITY_EDITOR
        MockBackend.Initialize(apiKey, mode, initializationOptionsJson);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("initialize", apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray, host, apiServerPort, Version);
        }
        RegisterBridgeListener();
#elif UNITY_IOS
        RegisterIosBridgeCallbacks();
        OctopusSdkInitialize(apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray, mode.AppManagedFieldsAsIntArray.Length,
            host, apiServerPort, initializationOptionsJson, Version);
#endif
        SetUnityTheme();
    }

    internal static string InitializationOptionsToJson(OctopusInitializationOptions options)
    {
        return "{\"appManagedAudioSession\":" +
            (options != null && options.AppManagedAudioSession ? "true" : "false") + "}";
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
            if (UnityEngine.Object.FindObjectOfType<OctopusChannel>() != null) return;
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
    private static extern void OctopusSdkInitialize(string apiKey, string mode, int[] appManagedFields,
        int appManagedFieldsLength, string apiServerHost, int apiServerPort, string initializationOptionsJson, string wrapperVersion);
#endif
}
