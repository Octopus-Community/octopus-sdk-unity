#if UNITY_EDITOR && UNITY_ANDROID
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// Build processor that generates an AAR containing theme assets (logo and fonts) for Android builds.
/// The AAR is created at Assets/OctopusSDK/Plugins/Android/octopus-assets.aar and contains:
/// - Logo at res/drawable/my_logo accessible via R.drawable.my_logo
/// - Fonts at res/font/ accessible via R.font.font_name
/// </summary>
public class AndroidThemeAARBuilder : IPreprocessBuildWithReport
{
    private const string AARFileName = "octopus-assets.aar";
    private const string PluginsAndroidPath = "Assets/OctopusSDK/Plugins/Android";
    private const string TempBuildFolder = "Temp/OctopusAssetsAAR";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
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

        // Check if there are any assets to bundle
        bool hasFonts = settings.Fonts != null && settings.Fonts.Length > 0;
        bool hasLogo = settings.Logo != null;

        // Update runtime fields (logoFileName) based on editor asset references
        settings.UpdateRuntimeFields();

        if (!hasFonts && !hasLogo)
        {
            Debug.Log("Octopus SDK: No theme assets configured, skipping AAR generation.");
            CleanupExistingAAR();
            return;
        }

        int fontCount = hasFonts ? settings.Fonts.Length : 0;
        string logoStatus = hasLogo ? " + logo" : "";
        Debug.Log($"Octopus SDK: Building theme AAR with {fontCount} font(s){logoStatus}...");

        try
        {
            BuildAAR(settings);
            Debug.Log($"Octopus SDK: Theme AAR generated successfully at {PluginsAndroidPath}/{AARFileName}");
        }
        catch (System.Exception ex)
        {
            throw new BuildFailedException($"Octopus SDK: Failed to build theme AAR: {ex.Message}");
        }
    }

    private void BuildAAR(OctopusThemeSettings settings)
    {
        string tempPath = Path.Combine(Application.dataPath, "..", TempBuildFolder);
        string outputPath = Path.Combine(Application.dataPath, "OctopusSDK", "Plugins", "Android");
        string aarPath = Path.Combine(outputPath, AARFileName);

        // Clean temp directory
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, true);
        }
        Directory.CreateDirectory(tempPath);

        // Ensure output directory exists
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        string resDir = Path.Combine(tempPath, "res");
        Directory.CreateDirectory(resDir);

        if (settings.Logo != null)
        {
            string drawableDir = Path.Combine(resDir, "drawable");
            Directory.CreateDirectory(drawableDir);

            string logoSourcePath = AssetDatabase.GetAssetPath(settings.Logo);
            string logoSourceFullPath = Path.Combine(Application.dataPath, "..", logoSourcePath);
            string logoExtension = Path.GetExtension(logoSourcePath).ToLowerInvariant();

            // Android drawable names must be lowercase, alphanumeric with underscores only
            string logoDestPath = Path.Combine(drawableDir, "my_logo" + logoExtension);

            File.Copy(logoSourceFullPath, logoDestPath, true);
        }

        if (settings.Fonts != null && settings.Fonts.Length > 0)
        {
            string fontDir = Path.Combine(resDir, "font");
            Directory.CreateDirectory(fontDir);

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
                string fontResourceName = SanitizeFontName(fileName);
                string destPath = Path.Combine(fontDir, fontResourceName);

                File.Copy(sourceFullPath, destPath, true);
            }
        }

        string manifestPath = Path.Combine(tempPath, "AndroidManifest.xml");
        File.WriteAllText(manifestPath, GenerateManifest());

        string classesJarPath = Path.Combine(tempPath, "classes.jar");
        CreateEmptyJar(classesJarPath);

        string rTxtPath = Path.Combine(tempPath, "R.txt");
        File.WriteAllText(rTxtPath, "");

        string proguardPath = Path.Combine(tempPath, "proguard.txt");
        File.WriteAllText(proguardPath, "");

        if (File.Exists(aarPath))
        {
            File.Delete(aarPath);
        }

        ZipFile.CreateFromDirectory(tempPath, aarPath);
        Directory.Delete(tempPath, true);
        AssetDatabase.Refresh();
    }

    private string GenerateManifest()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android""
    package=""com.octopuscommunity.assets"">
</manifest>";
    }

    private void CreateEmptyJar(string jarPath)
    {
        // Create a minimal valid JAR file (just a ZIP with META-INF/MANIFEST.MF)
        using (var zipArchive = ZipFile.Open(jarPath, ZipArchiveMode.Create))
        {
            var manifestEntry = zipArchive.CreateEntry("META-INF/MANIFEST.MF");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.WriteLine("Manifest-Version: 1.0");
                writer.WriteLine("Created-By: Octopus SDK for Unity");
            }
        }
    }

    private void CleanupExistingAAR()
    {
        string aarPath = Path.Combine(Application.dataPath, "OctopusSDK", "Plugins", "Android", AARFileName);
        if (File.Exists(aarPath))
        {
            File.Delete(aarPath);
            string metaPath = aarPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Sanitizes font filename for Android resource naming conventions.
    /// Android resource names must be lowercase, alphanumeric with underscores only.
    /// </summary>
    private string SanitizeFontName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Replace hyphens and spaces with underscores
        name = name.Replace('-', '_').Replace(' ', '_');

        // Remove any characters that aren't lowercase letters, numbers, or underscores
        var sanitized = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
            {
                sanitized.Append(c);
            }
        }

        return sanitized.ToString() + extension;
    }
}
#endif
