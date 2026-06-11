#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor-only settings for mock mode. Persisted under Assets/OctopusSDK/Resources/.
/// Never referenced in player builds.
/// </summary>
public class OctopusMockSettings : ScriptableObject
{
    private const string ResourcePath = "OctopusMockSettings";
    private const string ResourcesFolder = "Assets/OctopusSDK/Resources";
    private const string AssetPath = ResourcesFolder + "/" + ResourcePath + ".asset";

    [SerializeField] private bool enabledByDefault = true;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private int initialNotSeenCount = 0;
    [SerializeField] private List<OctopusGroup> seedGroups = new List<OctopusGroup>();

    public bool EnabledByDefault => enabledByDefault;
    public bool ShowOverlay => showOverlay;
    public int InitialNotSeenCount => initialNotSeenCount;
    public IList<OctopusGroup> SeedGroups => seedGroups ?? (seedGroups = new List<OctopusGroup>());

    private static OctopusMockSettings _instance;

    /// <summary>Loaded instance, or null if no asset has been created yet.</summary>
    public static OctopusMockSettings Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<OctopusMockSettings>(ResourcePath);
            return _instance;
        }
    }

    public static OctopusMockSettings GetOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<OctopusMockSettings>(AssetPath);
        if (settings == null)
        {
            settings = CreateInstance<OctopusMockSettings>();
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/OctopusSDK"))
                    AssetDatabase.CreateFolder("Assets", "OctopusSDK");
                AssetDatabase.CreateFolder("Assets/OctopusSDK", "Resources");
            }
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
        }
        return settings;
    }
}
#endif
