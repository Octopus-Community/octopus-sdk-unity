using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ScriptableObject that stores theme configuration for Octopus SDK.
/// Used both for editor configuration (Logo, Fonts) and runtime access (LogoFileName, AppName).
/// Persisted in Assets/OctopusSDK/Resources/ to be loadable at runtime via the Instance property.
/// </summary>
public class OctopusThemeSettings : ScriptableObject
{
    public const int MaxFonts = 5;
    private const string ResourcePath = "OctopusThemeSettings";

    /// <summary>
    /// Color scheme options for the SDK UI.
    /// </summary>
    public enum ColorSchemeType
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    /// <summary>
    /// Font style types matching native SDK font configuration.
    /// </summary>
    public enum FontStyleType
    {
        Title1 = 0,
        Title2 = 1,
        Body1 = 2,
        Body2 = 3,
        Caption1 = 4,
        Caption2 = 5,
        NavBarItem = 6
    }

    /// <summary>
    /// Configuration for a color scheme (light or dark).
    /// Each color can be individually enabled/disabled.
    /// </summary>
    [System.Serializable]
    public class ColorSchemeConfig
    {
        public bool primaryEnabled = false;
        public Color primary = new Color(0.2f, 0.4f, 0.8f, 1f);

        public bool primaryLowEnabled = false;
        public Color primaryLow = new Color(0.4f, 0.6f, 0.9f, 1f);

        public bool primaryHighEnabled = false;
        public Color primaryHigh = new Color(0.1f, 0.3f, 0.7f, 1f);

        public bool onPrimaryEnabled = false;
        public Color onPrimary = Color.white;

        /// <summary>
        /// Returns true if any color is enabled.
        /// </summary>
        public bool HasAnyEnabled => primaryEnabled || primaryLowEnabled || primaryHighEnabled || onPrimaryEnabled;

        /// <summary>
        /// Returns true if all required colors are enabled for a complete color scheme.
        /// </summary>
        public bool IsComplete => primaryEnabled && primaryLowEnabled && primaryHighEnabled && onPrimaryEnabled;
    }

    /// <summary>
    /// Configuration for a single font style (e.g., title1, body1).
    /// </summary>
    [System.Serializable]
    public class FontStyleConfig
    {
        public bool enabled = false;
        public int fontIndex = -1;  // Index into the fonts array, -1 means none selected
        public float fontSize = 0f;

        public FontStyleConfig() { }
        public FontStyleConfig(bool enabled, int fontIndex, float fontSize)
        {
            this.enabled = enabled;
            this.fontIndex = fontIndex;
            this.fontSize = fontSize;
        }
    }

#if UNITY_EDITOR
    private const string ResourcesFolder = "Assets/OctopusSDK/Resources";
    private const string AssetPath = ResourcesFolder + "/" + ResourcePath + ".asset";
#endif

    private static OctopusThemeSettings _instance;

    // Editor-only fields (asset references)
#if UNITY_EDITOR
    [SerializeField]
    private Texture2D logo;

    [SerializeField]
    private Font[] fonts = new Font[0];
#endif

    // Runtime fields (serialized values used by native code)
    [SerializeField]
    private string logoFileName;

    [SerializeField]
    private string appName;

    [SerializeField]
    private bool navBarUsesPrimaryColor = false;

    [SerializeField]
    private ColorSchemeType colorScheme = ColorSchemeType.System;

    // Font style configurations (runtime accessible)
    [SerializeField]
    private FontStyleConfig[] fontStyles = new FontStyleConfig[7];

    // Font file names for runtime use (populated during build)
    [SerializeField]
    private string[] fontFileNames = new string[0];

    // Color scheme configurations
    [SerializeField]
    private ColorSchemeConfig lightColorSchemeConfig = new ColorSchemeConfig();

    [SerializeField]
    private ColorSchemeConfig darkColorSchemeConfig = new ColorSchemeConfig();

#if UNITY_EDITOR
    public Texture2D Logo
    {
        get => logo;
        set => logo = value;
    }

    public Font[] Fonts
    {
        get => fonts;
        set
        {
            if (value != null && value.Length > MaxFonts)
            {
                Debug.LogWarning($"OctopusThemeSettings: Maximum of {MaxFonts} fonts allowed. Truncating list.");
                var truncated = new Font[MaxFonts];
                System.Array.Copy(value, truncated, MaxFonts);
                fonts = truncated;
            }
            else
            {
                fonts = value ?? new Font[0];
            }
        }
    }
#endif

    /// <summary>
    /// The logo filename with extension (e.g., "my_logo.png").
    /// Empty if no logo is configured.
    /// </summary>
    public string LogoFileName
    {
        get => logoFileName;
#if UNITY_EDITOR
        set => logoFileName = value;
#endif
    }

    /// <summary>
    /// The app name to display in the SDK UI.
    /// Empty if not configured.
    /// </summary>
    public string AppName
    {
        get => appName;
        set => appName = value;
    }

    public bool NavBarUsesPrimaryColor
    {
        get => navBarUsesPrimaryColor;
        set => navBarUsesPrimaryColor = value;
    }

    public ColorSchemeType ColorScheme
    {
        get => colorScheme;
        set => colorScheme = value;
    }

    /// <summary>
    /// Light color scheme configuration.
    /// </summary>
    public ColorSchemeConfig LightColorSchemeConfig
    {
        get
        {
            if (lightColorSchemeConfig == null)
            {
                lightColorSchemeConfig = new ColorSchemeConfig();
            }
            return lightColorSchemeConfig;
        }
#if UNITY_EDITOR
        set => lightColorSchemeConfig = value;
#endif
    }

    /// <summary>
    /// Dark color scheme configuration.
    /// </summary>
    public ColorSchemeConfig DarkColorSchemeConfig
    {
        get
        {
            if (darkColorSchemeConfig == null)
            {
                darkColorSchemeConfig = new ColorSchemeConfig();
            }
            return darkColorSchemeConfig;
        }
#if UNITY_EDITOR
        set => darkColorSchemeConfig = value;
#endif
    }

    /// <summary>
    /// Font style configurations for all 7 style types.
    /// </summary>
    public FontStyleConfig[] FontStyles
    {
        get
        {
            if (fontStyles == null || fontStyles.Length != 7)
            {
                fontStyles = new FontStyleConfig[7];
                for (int i = 0; i < 7; i++)
                {
                    fontStyles[i] = new FontStyleConfig();
                }
            }
            return fontStyles;
        }
#if UNITY_EDITOR
        set => fontStyles = value;
#endif
    }

    /// <summary>
    /// Font file names populated during build (e.g., "MyFont.ttf").
    /// </summary>
    public string[] FontFileNames
    {
        get => fontFileNames ?? new string[0];
#if UNITY_EDITOR
        set => fontFileNames = value;
#endif
    }

    /// <summary>
    /// Gets the font file name for a specific style, or empty if not configured.
    /// </summary>
    public string GetFontFileNameForStyle(FontStyleType styleType)
    {
        int index = (int)styleType;
        if (index < 0 || index >= FontStyles.Length) return "";

        var config = FontStyles[index];
        if (!config.enabled || config.fontIndex < 0 || config.fontIndex >= FontFileNames.Length)
            return "";

        return FontFileNames[config.fontIndex];
    }

    /// <summary>
    /// Gets the font size for a specific style, or 0 if not configured.
    /// </summary>
    public float GetFontSizeForStyle(FontStyleType styleType)
    {
        int index = (int)styleType;
        if (index < 0 || index >= FontStyles.Length) return 0f;

        var config = FontStyles[index];
        if (!config.enabled) return 0f;

        return config.fontSize;
    }

    /// <summary>
    /// Gets the singleton instance, loading from Resources if needed.
    /// Returns null if no settings exist (no theme assets configured).
    /// </summary>
    public static OctopusThemeSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<OctopusThemeSettings>(ResourcePath);
            }
            return _instance;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Gets or creates the settings asset in the Resources folder.
    /// </summary>
    public static OctopusThemeSettings GetOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<OctopusThemeSettings>(AssetPath);
        if (settings == null)
        {
            settings = CreateInstance<OctopusThemeSettings>();

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/OctopusSDK"))
                {
                    AssetDatabase.CreateFolder("Assets", "OctopusSDK");
                }
                AssetDatabase.CreateFolder("Assets/OctopusSDK", "Resources");
            }

            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
        }
        return settings;
    }

    /// <summary>
    /// Updates the runtime fields (logoFileName, fontFileNames) based on editor asset references.
    /// Call this during build to ensure runtime values are up to date.
    /// </summary>
    public void UpdateRuntimeFields()
    {
        if (logo != null)
        {
            string logoPath = AssetDatabase.GetAssetPath(logo);
            string extension = Path.GetExtension(logoPath).ToLowerInvariant();
            logoFileName = "my_logo" + extension;
        }
        else
        {
            logoFileName = "";
        }

        // Update font file names
        fontFileNames = new string[fonts.Length];
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null)
            {
                string fontPath = AssetDatabase.GetAssetPath(fonts[i]);
                fontFileNames[i] = Path.GetFileName(fontPath);
            }
            else
            {
                fontFileNames[i] = "";
            }
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Validates that all referenced fonts exist and are valid.
    /// Returns a list of validation results for each font slot.
    /// </summary>
    public FontValidationResult[] ValidateFonts()
    {
        var results = new FontValidationResult[fonts.Length];

        for (int i = 0; i < fonts.Length; i++)
        {
            var font = fonts[i];
            if (font == null)
            {
                results[i] = new FontValidationResult
                {
                    Index = i,
                    IsValid = true,
                    ErrorMessage = null
                };
            }
            else
            {
                var path = AssetDatabase.GetAssetPath(font);
                if (string.IsNullOrEmpty(path))
                {
                    results[i] = new FontValidationResult
                    {
                        Index = i,
                        IsValid = false,
                        ErrorMessage = "Font asset not found in project"
                    };
                }
                else
                {
                    results[i] = new FontValidationResult
                    {
                        Index = i,
                        IsValid = true,
                        AssetPath = path,
                        Font = font
                    };
                }
            }
        }

        return results;
    }

    public struct FontValidationResult
    {
        public int Index;
        public bool IsValid;
        public string ErrorMessage;
        public string AssetPath;
        public Font Font;
    }
#endif
}
