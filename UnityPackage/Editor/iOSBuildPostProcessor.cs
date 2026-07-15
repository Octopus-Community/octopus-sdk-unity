#if UNITY_EDITOR && UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using System.Diagnostics;
using System.IO;

public static class OctopusPostBuild
{
    private const string RubyScriptName = "patch_xcode_proj.rb";

    [PostProcessBuild(1000)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        PatchInfoPlist(buildPath);
        RunRubyScript(buildPath);
    }

    private static void PatchInfoPlist(string buildPath)
    {
        var settings = OctopusThemeSettings.Instance;
        if (settings == null) return;

        bool patchColorScheme = settings.ColorScheme != OctopusThemeSettings.ColorSchemeType.System;
        bool patchOrientation = settings.ForcedOrientation != OctopusThemeSettings.ForcedOrientationType.None;
        if (!patchColorScheme && !patchOrientation) return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            UnityEngine.Debug.LogWarning("[Octopus SDK] Info.plist not found at: " + plistPath);
            return;
        }

        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        if (patchColorScheme)
        {
            string interfaceStyle = settings.ColorScheme == OctopusThemeSettings.ColorSchemeType.Light ? "Light" : "Dark";
            plist.root.SetString("UIUserInterfaceStyle", interfaceStyle);
            UnityEngine.Debug.Log($"[Octopus SDK] Set UIUserInterfaceStyle to {interfaceStyle} in Info.plist");
        }

        if (patchOrientation)
        {
            // Make the app *capable* of the forced orientation so the Octopus modal can rotate to it
            // even when the game is locked the other way. This is a fallback: on Unity versions whose
            // trampoline implements application:supportedInterfaceOrientationsForWindow: the runtime
            // swizzle drives orientation and this plist entry is ignored; on those that don't, the
            // plist is the app-level mask. Adding the orientation does NOT rotate the game — Unity's
            // own view controller still reports its Player-Settings orientation.
            foreach (var orientation in OrientationPlistValues(settings.ForcedOrientation))
            {
                EnsureOrientation(plist, "UISupportedInterfaceOrientations", orientation, createIfMissing: true);
                EnsureOrientation(plist, "UISupportedInterfaceOrientations~ipad", orientation, createIfMissing: false);
            }
        }

        plist.WriteToFile(plistPath);
    }

    private static string[] OrientationPlistValues(OctopusThemeSettings.ForcedOrientationType orientation)
    {
        switch (orientation)
        {
            case OctopusThemeSettings.ForcedOrientationType.Portrait:
                return new[] { "UIInterfaceOrientationPortrait" };
            case OctopusThemeSettings.ForcedOrientationType.Landscape:
                return new[] { "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight" };
            default:
                return new string[0];
        }
    }

    // Ensures `orientation` is present in the named plist orientation array. Creates the array only
    // when createIfMissing is true (so we don't invent an ~ipad key on an iPhone-only app).
    private static void EnsureOrientation(PlistDocument plist, string key, string orientation, bool createIfMissing)
    {
        PlistElementArray arr;
        if (plist.root.values.TryGetValue(key, out var existing) && existing is PlistElementArray existingArr)
        {
            arr = existingArr;
        }
        else if (createIfMissing)
        {
            arr = plist.root.CreateArray(key);
        }
        else
        {
            return;
        }

        foreach (var e in arr.values)
        {
            if (e is PlistElementString s && s.value == orientation) return; // already present
        }

        arr.AddString(orientation);
        UnityEngine.Debug.Log($"[Octopus SDK] Added {orientation} to {key} in Info.plist");
    }

    private static void RunRubyScript(string buildPath)
    {
        string xcodeprojPath = Path.Combine(buildPath, "Unity-iPhone.xcodeproj");
        if (!Directory.Exists(xcodeprojPath))
        {
            UnityEngine.Debug.LogError("[Octopus SDK] Xcode project not found");
            return;
        }

        string rubyScriptPath = LocateRubyScript();
        if (string.IsNullOrEmpty(rubyScriptPath))
        {
            UnityEngine.Debug.LogError("[Octopus SDK] Failed to locate patch_xcode_proj.rb");
            return;
        }

        string arguments = $"\"{rubyScriptPath}\" \"{xcodeprojPath}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ruby",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[Octopus SDK] Failed to start Ruby process: " + e);
            return;
        }

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            if (!string.IsNullOrEmpty(stderr))
            {
                UnityEngine.Debug.LogError("[Octopus SDK] Ruby stderr:\n" + stderr);
            }
            UnityEngine.Debug.LogError("[Octopus SDK] Ruby exited with code " + process.ExitCode);
        }
    }

    private static string LocateRubyScript()
    {
        foreach (var pkg in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
        {
            string candidate = Path.Combine(pkg.resolvedPath, RubyScriptName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string assetsCandidate = Path.Combine(Application.dataPath, RubyScriptName);
        if (File.Exists(assetsCandidate))
        {
            return assetsCandidate;
        }

        string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(RubyScriptName));

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (assetPath.EndsWith(RubyScriptName))
            {
                return Path.GetFullPath(assetPath);
            }
        }

        UnityEngine.Debug.LogError("[Octopus SDK] Ruby script not found");
        return null;
    }
}
#endif