#if UNITY_EDITOR && UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class CustomThemesPostBuild
{
    [PostProcessBuild(600)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
            return;

        CopyLogosToDataRaw(buildPath);
        CopyFontsAndRegister(buildPath);
    }

    private static void CopyLogosToDataRaw(string buildPath)
    {
        string sourcePath = Path.Combine(Application.dataPath, "CustomThemesExample", "Resources", "iOS");
        if (!Directory.Exists(sourcePath))
            return;

        string destPath = Path.Combine(buildPath, "Data", "Raw");
        if (!Directory.Exists(destPath))
            Directory.CreateDirectory(destPath);

        foreach (string file in Directory.GetFiles(sourcePath))
        {
            if (file.EndsWith(".meta"))
                continue;

            string fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destPath, fileName), true);
            Debug.Log($"CustomThemesExample: Copied {fileName} to Data/Raw/");
        }
    }

    private static void CopyFontsAndRegister(string buildPath)
    {
        string fontsSourcePath = Path.Combine(Application.dataPath, "CustomThemesExample", "Resources", "Fonts");
        if (!Directory.Exists(fontsSourcePath))
            return;

        // Load Xcode project to add fonts to build
        string pbxProjectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromFile(pbxProjectPath);
        string targetGuid = pbxProject.GetUnityMainTargetGuid();

        var fontFileNames = new List<string>();

        foreach (string file in Directory.GetFiles(fontsSourcePath))
        {
            if (file.EndsWith(".meta"))
                continue;

            string fileName = Path.GetFileName(file);
            string destPath = Path.Combine(buildPath, fileName);
            File.Copy(file, destPath, true);
            fontFileNames.Add(fileName);

            // Add font to Xcode project's "Copy Bundle Resources" build phase
            string fileGuid = pbxProject.AddFile(fileName, fileName, PBXSourceTree.Source);
            pbxProject.AddFileToBuild(targetGuid, fileGuid);

            Debug.Log($"CustomThemesExample: Copied font {fileName} to bundle root and added to Xcode project");
        }

        // Save modified Xcode project
        pbxProject.WriteToFile(pbxProjectPath);

        if (fontFileNames.Count > 0)
        {
            RegisterFontsInInfoPlist(buildPath, fontFileNames);
        }
    }

    private static void RegisterFontsInInfoPlist(string buildPath, List<string> fontFileNames)
    {
        string plistPath = Path.Combine(buildPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogWarning("CustomThemesExample: Info.plist not found, cannot register fonts.");
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
                    Debug.Log($"CustomThemesExample: Added {fontFileNames.Count} font(s) to existing UIAppFonts in Info.plist");
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
                Debug.Log($"CustomThemesExample: Registered {fontFileNames.Count} font(s) in Info.plist UIAppFonts");
            }
        }
    }
}
#endif
