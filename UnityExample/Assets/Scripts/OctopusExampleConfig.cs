using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ScriptableObject holding the machine-local configuration the sample cannot carry in source:
/// API keys, static tokens and a config-level demo SSO and bridge signing secret for the example scenes,
/// and the optional links the shell's About screen offers. The asset file is git-ignored — each
/// developer creates their own
/// via Assets > Create > Octopus Example Config.
/// See Assets/Resources/OctopusExampleConfig.asset.template for the expected structure.
/// </summary>
public class OctopusExampleConfig : ScriptableObject
{
    private const string ResourcePath = "OctopusExampleConfig";

    [System.Serializable]
    public class ExampleProfile
    {
        public string apiKey;
        public string authToken;
        public string userId;
        public string nickname;
        public string bio;
        public string picture;

        public string[] entitlements = new string[0];

        // Snapshot of the owning config's secret. Never serialized per profile; native token
        // callbacks can read this plain C# value without loading a Unity resource off-thread.
        [System.NonSerialized] internal string signingSecret = "";
    }

    [Tooltip("Demo only: local HS256 secret for SSO and bridge posts, shared by all profiles. Empty uses authToken for SSO; bridge posts need a host signer. Never ship in production.")]
    public string ssoTokenSecret = "";

    [Header("Default profile (OctopusAuth examples)")]
    [SerializeField] ExampleProfile defaultProfile = new ExampleProfile();

    [Header("Forced Login profile (SSO with forced login)")]
    [SerializeField] ExampleProfile forcedLoginProfile = new ExampleProfile();

    [Header("Managed Fields profile (SSO with managed fields)")]
    [SerializeField] ExampleProfile managedFieldsProfile = new ExampleProfile();

    // Where About > Design reference points, empty by default. It lives here rather than in a
    // source file for the same reason the API keys do: this asset is git-ignored, and
    // `Assets/Scripts/` is published to the public mirror, so the URL of an internal design
    // project written into a versioned file would ship with it. Empty is the supported value —
    // the row is absent rather than dead, and no reader is told about a link they cannot open.
    [Header("Links (optional, local only)")]
    [SerializeField] string designReferenceUrl = "";

    public ExampleProfile Default => ResolveProfile(defaultProfile, 0);
    public ExampleProfile ForcedLogin => ResolveProfile(forcedLoginProfile, 1);
    public ExampleProfile ManagedFields => ResolveProfile(managedFieldsProfile, 2);

    private ExampleProfile ResolveProfile(ExampleProfile configured, int fixtureIndex)
    {
        var source = configured ?? new ExampleProfile();
        // A legacy asset can have only credentials. Supply an entire fixture identity in that
        // case; preserve intentional empty fields when the developer supplied their own userId.
        var identity = string.IsNullOrWhiteSpace(source.userId)
            ? OctopusSampleFixtures.CreateProfile(fixtureIndex) : source;
        return new ExampleProfile
        {
            apiKey = string.IsNullOrWhiteSpace(source.apiKey) ? defaultProfile?.apiKey : source.apiKey,
            authToken = source.authToken,
            userId = identity.userId,
            nickname = identity.nickname,
            bio = identity.bio,
            picture = identity.picture,
            entitlements = (string[])(identity.entitlements ?? new string[0]).Clone(),
            signingSecret = ssoTokenSecret
        };
    }
    public string DesignReferenceUrl => designReferenceUrl;

    private static OctopusExampleConfig _instance;

    public static OctopusExampleConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<OctopusExampleConfig>(ResourcePath);
                if (_instance == null)
                {
                    Debug.LogError(
                        "OctopusExampleConfig asset not found in Resources/. " +
                        "Create one via Assets > Create > Octopus Example Config." +
                        "See Assets/Resources/OctopusExampleConfig.asset.template for details.");
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// The same asset, or null, without logging an error when it is absent.
    ///
    /// For a caller that *handles* the absence and shows it to the user — the scenario pilots and
    /// the Home dashboard both do. <see cref="Instance"/>'s error is for the legacy `*Example`
    /// scenes, which have no such surface and would otherwise fail silently; keeping it on the path
    /// the shell reads would put a red console error on every EditMode run and on every clone
    /// without the gitignored asset, for a state the screen already explains in words.
    /// </summary>
    public static OctopusExampleConfig LoadedOrNull
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<OctopusExampleConfig>(ResourcePath);
            return _instance;
        }
    }

#if UNITY_EDITOR
    private const string ResourcesFolder = "Assets/Resources";
    private const string AssetPath = ResourcesFolder + "/" + ResourcePath + ".asset";

    [MenuItem("Assets/Create/Octopus Example Config")]
    public static void CreateAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<OctopusExampleConfig>(AssetPath);
        if (existing != null)
        {
            Debug.Log("OctopusExampleConfig already exists at " + AssetPath);
            Selection.activeObject = existing;
            return;
        }

        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var config = CreateInstance<OctopusExampleConfig>();
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = config;
        Debug.Log("Created OctopusExampleConfig at " + AssetPath +
                  ". Fill the default API key and global ssoTokenSecret (demo signing), " +
                  "or each profile's authToken (static fallback). Fixture identities are supplied automatically. " +
                  "Lifecycle Switch community also needs a distinct community key in forcedLoginProfile.apiKey. Keep this asset local.");
    }
#endif
}
