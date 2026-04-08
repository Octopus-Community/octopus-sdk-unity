using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ScriptableObject holding API keys and tokens for example scenes.
/// The asset file is git-ignored — each developer creates their own
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
    }

    [Header("Default profile (OctopusAuth examples)")]
    [SerializeField] ExampleProfile defaultProfile = new ExampleProfile();

    [Header("Forced Login profile (SSO with forced login)")]
    [SerializeField] ExampleProfile forcedLoginProfile = new ExampleProfile();

    [Header("Managed Fields profile (SSO with managed fields)")]
    [SerializeField] ExampleProfile managedFieldsProfile = new ExampleProfile();

    public ExampleProfile Default => defaultProfile;
    public ExampleProfile ForcedLogin => forcedLoginProfile;
    public ExampleProfile ManagedFields => managedFieldsProfile;

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
        Debug.Log("Created OctopusExampleConfig at " + AssetPath);
    }
#endif
}
