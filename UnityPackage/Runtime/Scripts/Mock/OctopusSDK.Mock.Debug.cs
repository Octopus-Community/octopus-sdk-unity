#if UNITY_EDITOR
public partial class OctopusSDK
{
    /// <summary>Debug/QA only — do not ship in production builds. Stored Editor overrides.</summary>
    public sealed class OctopusDebugOverrides
    {
        /// <summary>Debug/QA only — do not ship in production builds. Null means no profile lock override.</summary>
        public OctopusProfileFieldsLock ProfileFieldsLock { get; internal set; }
        /// <summary>Debug/QA only — do not ship in production builds. Null means no content override.</summary>
        public OctopusContentOptions ContentOptions { get; internal set; }
        /// <summary>Debug/QA only — do not ship in production builds. Null means no consent override.</summary>
        public OctopusTermsAcceptanceMode? TermsAcceptanceMode { get; internal set; }
        /// <summary>Debug/QA only — do not ship in production builds. Null means no exposure override.</summary>
        public bool? ExposeClientUserId { get; internal set; }
        internal OctopusDebugOverrides() { }
    }

    public static partial class Mock
    {
        /// <summary>Debug/QA only — do not ship in production builds. Last overrides; recorded independently of Enabled.</summary>
        public static OctopusDebugOverrides DebugOverrides { get; private set; } = new OctopusDebugOverrides();
        /// <summary>Debug/QA only — do not ship in production builds. Effective result fixture for DebugGetCommunityConfig, including any desired overrides. Defaults to JSON null; no backend fetch occurs.</summary>
        public static string DebugCommunityConfigJson { get; set; } = "null";
        /// <summary>Debug/QA only — do not ship in production builds. Non-null forces the config error callback.</summary>
        public static string DebugCommunityConfigError { get; set; }

        internal static void RecordDebugOverride(string method, object value)
        {
            Record(method, value);
            switch (method)
            {
                case "OverrideProfileFieldsLock": DebugOverrides.ProfileFieldsLock = (OctopusProfileFieldsLock)value; break;
                case "OverrideContentOptions": DebugOverrides.ContentOptions = (OctopusContentOptions)value; break;
                case "OverrideTermsAcceptanceMode": DebugOverrides.TermsAcceptanceMode = (OctopusTermsAcceptanceMode?)value; break;
                case "OverrideExposeClientUserId": DebugOverrides.ExposeClientUserId = (bool?)value; break;
            }
        }

        internal static void ResetDebugMock()
        {
            DebugOverrides = new OctopusDebugOverrides();
            DebugCommunityConfigJson = "null";
            DebugCommunityConfigError = null;
            _debugConfigCallbacks.Clear();
        }
    }
}
#endif
