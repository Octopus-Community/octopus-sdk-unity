#if UNITY_EDITOR && UNITY_IOS
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// iOS build processor that embeds theme assets (logo and fonts) into the Xcode project bundle.
/// The logo is copied to Data/Raw/my_logo in the iOS build output.
/// Fonts are copied to the bundle root and registered in Info.plist under UIAppFonts,
/// making them available system-wide via UIFont/SwiftUI.Font.
/// </summary>
public class iOSThemeEmbedder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    /// <summary>
    /// Pre-build validation for iOS builds.
    /// </summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS)
        {
            return;
        }

        var settings = OctopusThemeSettings.GetOrCreateSettings();
        var validationResults = settings.ValidateFonts();

        // Check for validation errors
        foreach (var result in validationResults)
        {
            if (!result.IsValid)
            {
                throw new BuildFailedException(
                    $"Octopus SDK: Font validation failed at index {result.Index}: {result.ErrorMessage}");
            }
        }

        bool hasFonts = settings.Fonts != null && settings.Fonts.Length > 0;
        bool hasLogo = settings.Logo != null;

        // Update runtime fields (logoFileName) based on editor asset references
        settings.UpdateRuntimeFields();

        if (hasFonts || hasLogo)
        {
            int fontCount = hasFonts ? settings.Fonts.Length : 0;
            string logoStatus = hasLogo ? " + logo" : "";
            Debug.Log($"Octopus SDK: {fontCount} font(s){logoStatus} will be embedded in iOS build.");
        }
    }

    /// <summary>
    /// Post-build processor that copies theme assets to the iOS build output.
    /// Runs after the main build but before the existing Octopus post-processor.
    /// </summary>
    [PostProcessBuild(500)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        var settings = OctopusThemeSettings.GetOrCreateSettings();

        bool hasFonts = settings.Fonts != null && settings.Fonts.Length > 0;
        bool hasLogo = settings.Logo != null;

        if (!hasFonts && !hasLogo)
        {
            return;
        }

        int fontCount = hasFonts ? settings.Fonts.Length : 0;
        string logoStatus = hasLogo ? " + logo" : "";
        Debug.Log($"Octopus SDK: Embedding {fontCount} font(s){logoStatus} in iOS build...");

        try
        {
            EmbedThemeAssets(settings, buildPath);
            Debug.Log("Octopus SDK: iOS theme embedding completed successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Octopus SDK: Failed to embed theme assets in iOS build: {ex.Message}");
            throw;
        }
    }

    private static void EmbedThemeAssets(OctopusThemeSettings settings, string buildPath)
    {
        string pbxProjectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromFile(pbxProjectPath);
        string targetGuid = pbxProject.GetUnityMainTargetGuid();

        if (settings.Logo != null)
        {
            string rawDataPath = Path.Combine(buildPath, "Data", "Raw");
            if (!Directory.Exists(rawDataPath))
            {
                Directory.CreateDirectory(rawDataPath);
            }

            string logoSourcePath = AssetDatabase.GetAssetPath(settings.Logo);
            string logoSourceFullPath = Path.Combine(Application.dataPath, "..", logoSourcePath);
            string logoExtension = Path.GetExtension(logoSourcePath);
            string logoDestPath = Path.Combine(rawDataPath, "my_logo" + logoExtension);

            File.Copy(logoSourceFullPath, logoDestPath, true);
        }

        if (settings.Fonts != null && settings.Fonts.Length > 0)
        {
            var fontFileNames = new List<string>();

            foreach (var font in settings.Fonts)
            {
                if (font == null) continue;

                string sourcePath = AssetDatabase.GetAssetPath(font);

                // Skip built-in Unity fonts (they don't have real file paths)
                if (string.IsNullOrEmpty(sourcePath) || !sourcePath.StartsWith("Assets/"))
                {
                    Debug.LogWarning($"Octopus SDK: Skipping built-in font '{font.name}'. Only project fonts can be embedded.");
                    continue;
                }

                string sourceFullPath = Path.Combine(Application.dataPath, "..", sourcePath);
                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(buildPath, fileName);

                File.Copy(sourceFullPath, destPath, true);
                fontFileNames.Add(fileName);

                string fileGuid = pbxProject.AddFile(fileName, fileName, PBXSourceTree.Source);
                pbxProject.AddFileToBuild(targetGuid, fileGuid);
            }

            if (fontFileNames.Count > 0)
            {
                RegisterFontsInInfoPlist(buildPath, fontFileNames);
            }
        }

        pbxProject.WriteToFile(pbxProjectPath);
    }

    /// <summary>
    /// Registers font files in Info.plist under the UIAppFonts key.
    /// This makes fonts available to the app via UIFont/SwiftUI.Font.
    /// </summary>
    private static void RegisterFontsInInfoPlist(string buildPath, List<string> fontFileNames)
    {
        string plistPath = Path.Combine(buildPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogWarning("Octopus SDK: Info.plist not found, cannot register fonts.");
            return;
        }

        string plistContent = File.ReadAllText(plistPath);

        if (plistContent.Contains("<key>UIAppFonts</key>"))
        {
            int keyIndex = plistContent.IndexOf("<key>UIAppFonts</key>");
            int arrayStartIndex = plistContent.IndexOf("<array>", keyIndex);
            int arrayEndIndex = plistContent.IndexOf("</array>", arrayStartIndex);

            if (arrayStartIndex != -1 && arrayEndIndex != -1)
            {
                string existingArray = plistContent.Substring(arrayStartIndex, arrayEndIndex - arrayStartIndex + 8);
                var newFontEntries = "";
                foreach (var fontName in fontFileNames)
                {
                    if (!existingArray.Contains($"<string>{fontName}</string>"))
                    {
                        newFontEntries += $"\n\t\t<string>{fontName}</string>";
                    }
                }

                if (!string.IsNullOrEmpty(newFontEntries))
                {
                    plistContent = plistContent.Insert(arrayEndIndex, newFontEntries);
                    File.WriteAllText(plistPath, plistContent);
                }
            }
        }
        else
        {
            var fontEntries = new System.Text.StringBuilder();
            fontEntries.AppendLine("\t<key>UIAppFonts</key>");
            fontEntries.AppendLine("\t<array>");
            foreach (var fontName in fontFileNames)
            {
                fontEntries.AppendLine($"\t\t<string>{fontName}</string>");
            }
            fontEntries.Append("\t</array>");

            int lastDictClose = plistContent.LastIndexOf("</dict>");
            if (lastDictClose != -1)
            {
                plistContent = plistContent.Insert(lastDictClose, fontEntries.ToString() + "\n");
                File.WriteAllText(plistPath, plistContent);
            }
        }
    }
}
#endif
