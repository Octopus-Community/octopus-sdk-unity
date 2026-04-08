using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public static class LegacyPackageExporter
{
    // ===== CONFIG =====

    private const string SOURCE_PATH = @"../UnityPackage";
    private const string TEMP_IMPORT_PATH = "Assets/OctopusCommunitySDK";
    private const string OUTPUT_PACKAGE_PATH = @"../OctopusCommunitySDK.unitypackage";
    private static readonly string[] IGNORE_CONTAINS =
    {
        ".git",
        ".gitignore",
        "package.json",
        ".meta",
    };

    // ===== ENTRY POINT PARA CLI =====

    public static void Build()
    {
        try
        {
            Debug.Log("=== Legacy Package Export Started ===");

            if (Directory.Exists(TEMP_IMPORT_PATH))
                Directory.Delete(TEMP_IMPORT_PATH, true);

            CopyDirectoryRecursive(SOURCE_PATH, TEMP_IMPORT_PATH);

            AssetDatabase.Refresh();

            ExportPackage();

            AssetDatabase.DeleteAsset(TEMP_IMPORT_PATH);
            AssetDatabase.Refresh();

            Debug.Log("=== Legacy Package Export Completed ===");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Legacy export failed: " + ex);
            throw;
        }
    }

    // ===== COPY =====

    private static void CopyDirectoryRecursive(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException(source);

        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            if (ShouldIgnore(file))
                continue;

            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destination, fileName);
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            if (ShouldIgnore(dir))
                continue;

            var dirName = Path.GetFileName(dir);
            var destDir = Path.Combine(destination, dirName);
            CopyDirectoryRecursive(dir, destDir);
        }
    }

    private static bool ShouldIgnore(string path)
    {
        return IGNORE_CONTAINS.Any(ignore =>
            path.Contains(ignore));
    }

    // ===== EXPORT =====

    private static void ExportPackage()
    {
        if (!Directory.Exists("Builds"))
            Directory.CreateDirectory("Builds");

        AssetDatabase.ExportPackage(
            new string[] {
                TEMP_IMPORT_PATH,
            },
            OUTPUT_PACKAGE_PATH,
            ExportPackageOptions.Recurse |
            ExportPackageOptions.IncludeDependencies
        );

        Debug.Log("UnityPackage exported to: " + OUTPUT_PACKAGE_PATH);
    }
}