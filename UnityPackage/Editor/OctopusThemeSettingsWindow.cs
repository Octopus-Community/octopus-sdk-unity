#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor window for managing Octopus SDK theme configuration.
/// Accessible via Octopus SDK > Theme Configuration menu.
/// </summary>
public class OctopusThemeSettingsWindow : EditorWindow
{
    private OctopusThemeSettings settings;
    private SerializedObject serializedSettings;
    private List<Font> fontList = new List<Font>();
    private OctopusThemeSettings.FontValidationResult[] validationResults;
    private Vector2 scrollPosition;

    // Tab selection
    private int selectedTab = 0;
    private static readonly string[] TabNames = { "Top Bar", "Fonts", "Colors" };

    // Colors sub-tab selection
    private int selectedColorTab = 0;
    private static readonly string[] ColorTabNames = { "Light Theme", "Dark Theme" };

    // Deferred actions to avoid GUI layout errors
    private int? pendingRemoveIndex = null;
    private int? pendingChangeIndex = null;
    private Font pendingChangeFont = null;
    private bool pendingAdd = false;

    // Font style display names
    private static readonly string[] FontStyleNames = new string[]
    {
        "Title 1",
        "Title 2",
        "Body 1",
        "Body 2",
        "Caption 1",
        "Caption 2",
        "Nav Bar Item"
    };

    // Default font sizes for each style
    private static readonly float[] DefaultFontSizes = new float[]
    {
        24f,  // Title 1
        20f,  // Title 2
        16f,  // Body 1
        14f,  // Body 2
        12f,  // Caption 1
        10f,  // Caption 2
        16f   // Nav Bar Item
    };

    [MenuItem("Octopus SDK/Theme Configuration", false, 100)]
    public static void ShowWindow()
    {
        var window = GetWindow<OctopusThemeSettingsWindow>("Octopus SDK Theme");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        settings = OctopusThemeSettings.GetOrCreateSettings();
        serializedSettings = new SerializedObject(settings);

        fontList.Clear();
        if (settings.Fonts != null)
        {
            fontList.AddRange(settings.Fonts);
        }

        Validate();
    }

    private void Validate()
    {
        if (settings != null)
        {
            validationResults = settings.ValidateFonts();
        }
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            LoadSettings();
            return;
        }

        // Process any deferred actions from previous frame
        ProcessDeferredActions();

        EditorGUILayout.Space(10);

        // Tab bar
        selectedTab = GUILayout.Toolbar(selectedTab, TabNames);

        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        switch (selectedTab)
        {
            case 0:
                DrawTopBarTab();
                break;
            case 1:
                DrawFontsTab();
                break;
            case 2:
                DrawColorsTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTopBarTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawLogo();

        EditorGUILayout.Space(10);

        DrawAppName();

        EditorGUILayout.Space(10);

        DrawNavBarColorOption();

        EditorGUILayout.EndVertical();
    }

    private void DrawFontsTab()
    {
        DrawFontList();

        EditorGUILayout.Space(10);

        DrawValidationErrors();
    }

    private void DrawColorsTab()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.label);
        headerStyle.fixedHeight = 20;
        headerStyle.fontStyle = FontStyle.Bold;

        // Color Scheme Type selector
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Color Scheme Mode", headerStyle);
        GUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        var newColorScheme = (OctopusThemeSettings.ColorSchemeType)EditorGUILayout.EnumPopup("Mode", settings.ColorScheme);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Update Octopus Color Scheme");
            settings.ColorScheme = newColorScheme;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Light/Dark sub-tabs
        selectedColorTab = GUILayout.Toolbar(selectedColorTab, ColorTabNames);

        EditorGUILayout.Space(5);

        // Draw the selected color scheme config
        if (selectedColorTab == 0)
        {
            DrawColorSchemeConfig(settings.LightColorSchemeConfig, "Light");
        }
        else
        {
            DrawColorSchemeConfig(settings.DarkColorSchemeConfig, "Dark");
        }
    }

    private void DrawColorSchemeConfig(OctopusThemeSettings.ColorSchemeConfig config, string schemeName)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.label);
        headerStyle.fixedHeight = 20;
        headerStyle.fontStyle = FontStyle.Bold;

        EditorGUILayout.LabelField($"{schemeName} Theme Colors", headerStyle);
        EditorGUILayout.HelpBox("Enable and configure custom colors. Disabled colors use SDK defaults.", MessageType.None);

        GUILayout.Space(5);

        bool changed = false;

        // Primary
        changed |= DrawColorField(config, "Primary", ref config.primaryEnabled, ref config.primary);

        // Primary Low
        changed |= DrawColorField(config, "Primary Low", ref config.primaryLowEnabled, ref config.primaryLow);

        // Primary High
        changed |= DrawColorField(config, "Primary High", ref config.primaryHighEnabled, ref config.primaryHigh);

        // On Primary
        changed |= DrawColorField(config, "On Primary", ref config.onPrimaryEnabled, ref config.onPrimary);

        if (changed)
        {
            Undo.RecordObject(settings, $"Update {schemeName} Color Scheme");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Validation message
        if (config.HasAnyEnabled && !config.IsComplete)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("For best results, enable all four colors. Partially configured schemes may produce unexpected results.", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private bool DrawColorField(OctopusThemeSettings.ColorSchemeConfig config, string label, ref bool enabled, ref Color color)
    {
        bool changed = false;

        EditorGUILayout.BeginHorizontal();

        // Enable checkbox
        EditorGUI.BeginChangeCheck();
        bool newEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(20));
        if (EditorGUI.EndChangeCheck())
        {
            enabled = newEnabled;
            changed = true;
        }

        // Label
        EditorGUILayout.LabelField(label, GUILayout.Width(90));

        // Color picker (disabled when not enabled)
        EditorGUI.BeginDisabledGroup(!enabled);
        EditorGUI.BeginChangeCheck();
        Color newColor = EditorGUILayout.ColorField(color);
        if (EditorGUI.EndChangeCheck())
        {
            color = newColor;
            changed = true;
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        return changed;
    }

    private void ProcessDeferredActions()
    {
        bool changed = false;

        if (pendingRemoveIndex.HasValue)
        {
            if (pendingRemoveIndex.Value >= 0 && pendingRemoveIndex.Value < fontList.Count)
            {
                int removedIndex = pendingRemoveIndex.Value;
                fontList.RemoveAt(removedIndex);

                // Update font style references
                var fontStyles = settings.FontStyles;
                for (int i = 0; i < fontStyles.Length; i++)
                {
                    if (fontStyles[i] != null)
                    {
                        if (fontStyles[i].fontIndex == removedIndex)
                        {
                            // Font was deleted - disable this style
                            fontStyles[i].enabled = false;
                            fontStyles[i].fontIndex = -1;
                        }
                        else if (fontStyles[i].fontIndex > removedIndex)
                        {
                            // Shift index down
                            fontStyles[i].fontIndex--;
                        }
                    }
                }

                changed = true;
            }
            pendingRemoveIndex = null;
        }

        if (pendingChangeIndex.HasValue)
        {
            if (pendingChangeIndex.Value >= 0 && pendingChangeIndex.Value < fontList.Count)
            {
                fontList[pendingChangeIndex.Value] = pendingChangeFont;
                changed = true;
            }
            pendingChangeIndex = null;
            pendingChangeFont = null;
        }

        if (pendingAdd)
        {
            fontList.Add(null);
            changed = true;
            pendingAdd = false;
        }

        if (changed)
        {
            SaveAndValidate();
            Repaint();
        }
    }

    private void DrawLogo()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.label);
        headerStyle.fixedHeight = 20;
        headerStyle.fontStyle = FontStyle.Bold;

        EditorGUILayout.LabelField("Logo", headerStyle);

        GUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        var newLogo = (Texture2D)EditorGUILayout.ObjectField(settings.Logo, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Update Octopus Theme Logo");
            settings.Logo = newLogo;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Display logo preview if set
        if (settings.Logo != null)
        {
            GUILayout.Space(10);

            float maxPreviewSize = 128f;
            float aspectRatio = (float)settings.Logo.width / settings.Logo.height;
            float previewWidth, previewHeight;

            if (aspectRatio >= 1f)
            {
                // Landscape or square: constrain by width
                previewWidth = Mathf.Min(maxPreviewSize, settings.Logo.width);
                previewHeight = previewWidth / aspectRatio;
            }
            else
            {
                // Portrait: constrain by height
                previewHeight = Mathf.Min(maxPreviewSize, settings.Logo.height);
                previewWidth = previewHeight * aspectRatio;
            }

            // Center the preview horizontally
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
            EditorGUI.DrawPreviewTexture(previewRect, settings.Logo, null, ScaleMode.ScaleToFit);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        // EditorGUILayout.EndVertical();
    }

    private void DrawAppName()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.label);
        headerStyle.fixedHeight = 20;
        headerStyle.fontStyle = FontStyle.Bold;

        // EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("App Name", headerStyle);

        GUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        var newAppName = EditorGUILayout.TextField(settings.AppName);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Update Octopus Theme App Name");
            settings.AppName = newAppName;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // EditorGUILayout.EndVertical();
    }

    private void DrawNavBarColorOption()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.label);
        headerStyle.fixedHeight = 20;
        headerStyle.fontStyle = FontStyle.Bold;

        // EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Navigation Bar Color", headerStyle);

        GUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        bool newNavBarUsesPrimary = EditorGUILayout.Toggle("Use Primary Color", settings.NavBarUsesPrimaryColor);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Update Octopus Theme Nav Bar Color Option");
            settings.NavBarUsesPrimaryColor = newNavBarUsesPrimary;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

    }

    private void DrawFontList()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.label);
        headerStyle.fixedHeight = 20;
        headerStyle.fontStyle = FontStyle.Bold;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Fonts", headerStyle);

        GUILayout.Space(2);

        for (int i = 0; i < fontList.Count; i++)
        {
            bool hasError = false;
            string errorMessage = null;

            // Only validate non-null fonts
            if (fontList[i] != null && validationResults != null && i < validationResults.Length && !validationResults[i].IsValid)
            {
                hasError = true;
                errorMessage = validationResults[i].ErrorMessage;
            }

            if (hasError)
            {
                GUI.color = new Color(1f, 0.7f, 0.7f);
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            var newFont = (Font)EditorGUILayout.ObjectField(fontList[i], typeof(Font), false);
            if (EditorGUI.EndChangeCheck())
            {
                // Check if font is a built-in Unity font
                if (newFont != null && IsBuiltInFont(newFont))
                {
                    Debug.LogWarning($"Octopus SDK: Cannot add built-in font '{newFont.name}'. Only project fonts (.ttf, .otf) can be embedded.");
                }
                // Check if font is already in the list (at a different index)
                else if (newFont != null && IsFontAlreadyAdded(newFont, i))
                {
                    Debug.LogWarning($"Octopus SDK: Font '{newFont.name}' is already in the list.");
                }
                else
                {
                    // Defer the change to avoid layout errors
                    pendingChangeIndex = i;
                    pendingChangeFont = newFont;
                }
            }

            if (GUILayout.Button("-", GUILayout.Width(25)))
            {
                // Defer the removal to avoid layout errors
                pendingRemoveIndex = i;
            }
            EditorGUILayout.EndHorizontal();

            GUI.color = Color.white;

            if (hasError)
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }
        }

        // Footer / Add Button
        EditorGUI.BeginDisabledGroup(fontList.Count >= OctopusThemeSettings.MaxFonts);
        if (GUILayout.Button("+", GUILayout.Width(40)))
        {
            // Defer the add to avoid layout errors
            pendingAdd = true;
        }
        EditorGUI.EndDisabledGroup();

        // Font Style Configuration section
        if (fontList.Count > 0)
        {
            EditorGUILayout.Space(10);
            DrawFontStyleConfigurations();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawFontStyleConfigurations()
    {
        GUIStyle subHeaderStyle = new GUIStyle(EditorStyles.label);
        subHeaderStyle.fontStyle = FontStyle.Bold;

        EditorGUILayout.LabelField("Font Style Configuration", subHeaderStyle);
        EditorGUILayout.HelpBox("Assign embedded fonts to specific text styles. Disabled styles use default fonts.", MessageType.None);

        GUILayout.Space(5);

        // Build font dropdown options (no "None" - checkbox controls enabled state)
        var fontOptions = new List<string>();
        for (int i = 0; i < fontList.Count; i++)
        {
            if (fontList[i] != null)
            {
                fontOptions.Add(fontList[i].name);
            }
            else
            {
                fontOptions.Add($"(Empty slot {i + 1})");
            }
        }
        string[] fontOptionsArray = fontOptions.ToArray();

        var fontStyles = settings.FontStyles;
        bool changed = false;

        for (int i = 0; i < FontStyleNames.Length; i++)
        {
            var config = fontStyles[i];
            if (config == null)
            {
                config = new OctopusThemeSettings.FontStyleConfig();
                fontStyles[i] = config;
                changed = true;
            }

            EditorGUILayout.BeginHorizontal();

            // Enable checkbox
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUILayout.Toggle(config.enabled, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck())
            {
                config.enabled = newEnabled;
                if (newEnabled)
                {
                    if (config.fontSize <= 0)
                    {
                        config.fontSize = DefaultFontSizes[i];
                    }
                    if (config.fontIndex < 0 && fontList.Count > 0)
                    {
                        config.fontIndex = 0;
                    }
                }
                changed = true;
            }

            // Style name label
            EditorGUILayout.LabelField(FontStyleNames[i], GUILayout.Width(90));

            // Font dropdown and size (only enabled when checkbox is checked)
            EditorGUI.BeginDisabledGroup(!config.enabled);

            // Font dropdown (direct index, no offset)
            int dropdownIndex = config.fontIndex;
            if (dropdownIndex < 0) dropdownIndex = 0;
            EditorGUI.BeginChangeCheck();
            int newDropdownIndex = EditorGUILayout.Popup(dropdownIndex, fontOptionsArray, GUILayout.MinWidth(120));
            if (EditorGUI.EndChangeCheck())
            {
                config.fontIndex = newDropdownIndex;
                changed = true;
            }

            // Font size
            EditorGUILayout.LabelField("Size:", GUILayout.Width(35));
            EditorGUI.BeginChangeCheck();
            float newSize = EditorGUILayout.FloatField(config.fontSize, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck())
            {
                config.fontSize = Mathf.Max(1f, newSize);
                changed = true;
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        if (changed)
        {
            Undo.RecordObject(settings, "Update Font Style Configuration");
            settings.FontStyles = fontStyles;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }

    private void DrawValidationErrors()
    {
        if (validationResults == null || validationResults.Length == 0) return;

        int errorCount = 0;
        foreach (var result in validationResults)
        {
            if (!result.IsValid) errorCount++;
        }

        if (errorCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{errorCount} font(s) have validation errors. These will cause build failures on Android/iOS.",
                MessageType.Warning);
        }
    }

    private void SaveAndValidate()
    {
        SaveSettings();
        Validate();
    }

    private void SaveSettings()
    {
        if (settings == null) return;

        Undo.RecordObject(settings, "Update Octopus Theme Settings");
        settings.Fonts = fontList.ToArray();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Checks if a font is already in the list at a different index.
    /// </summary>
    private bool IsFontAlreadyAdded(Font font, int excludeIndex)
    {
        for (int i = 0; i < fontList.Count; i++)
        {
            if (i != excludeIndex && fontList[i] == font)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a font is a built-in Unity font (not a project asset).
    /// Built-in fonts cannot be embedded in native builds.
    /// </summary>
    private bool IsBuiltInFont(Font font)
    {
        string path = AssetDatabase.GetAssetPath(font);
        return string.IsNullOrEmpty(path) || !path.StartsWith("Assets/");
    }
}
#endif
