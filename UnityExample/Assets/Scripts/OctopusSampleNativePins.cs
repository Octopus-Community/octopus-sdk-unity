using UnityEngine;

/// <summary>
/// Build pins captured before every player build, not versions queried from native binaries.
/// The Editor writes them into a generated, git-ignored <c>Resources</c> text asset: a changed
/// asset always reaches the next build, whereas Unity's incremental pipeline skips scene
/// processing callbacks for scenes it reuses from a previous build. Home and Send feedback read
/// the same metadata in every sample build.
/// </summary>
public static class OctopusSampleNativePins
{
    public const string ResourceName = "OctopusSampleNativePins";
    public const string Unavailable = "Unavailable — build pin metadata was not captured";

    public static string Current()
    {
#if UNITY_EDITOR
        return ReadSources();
#else
        var metadata = Resources.Load<TextAsset>(ResourceName);
        return metadata == null || string.IsNullOrEmpty(metadata.text) ? Unavailable : metadata.text;
#endif
    }

#if UNITY_EDITOR
    public static string AssetPath
    {
        get { return "Assets/Resources/" + ResourceName + ".txt"; }
    }

    // The same sources checked by ci/native-pins/verify-native-pins.sh: Android's generated
    // resolver output and iOS's SwiftPM exact pin. Only validated version numbers are retained.
    public static string ReadSources()
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(OctopusSDK).Assembly);
        if (package == null) throw new System.InvalidOperationException("Octopus package not resolved.");
        return ParseSources(
            System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                "Plugins/Android/mainTemplate.gradle")),
            System.IO.File.ReadAllText(System.IO.Path.Combine(package.resolvedPath,
                "Editor/ruby/patch_xcode_proj.rb")));
    }

    public static string ParseSources(string android, string ios)
    {
        var native = System.Text.RegularExpressions.Regex.Matches(android,
            @"(?m)^\s*implementation\s+'com\.octopuscommunity:octopus-sdk(-ui)?:(\d+\.\d+\.\d+)'\s*(?://.*)?$");
        var swift = System.Text.RegularExpressions.Regex.Matches(ios,
            @"(?m)^PACKAGE_VERSION\s*=\s*'(\d+\.\d+\.\d+)'\s*$");
        if (native.Count != 2 || native[0].Groups[2].Value != native[1].Groups[2].Value ||
            native[0].Groups[1].Value == native[1].Groups[1].Value || swift.Count != 1)
        {
            throw new System.InvalidOperationException("Missing, malformed or conflicting native build pins.");
        }
        return "Android " + native[0].Groups[2].Value + " · iOS " + swift[0].Groups[1].Value;
    }

    /// <summary>
    /// Writes the current pins into the generated asset and returns them. Reads before writing:
    /// a missing source fails the build instead of shipping yesterday's pin. An unchanged asset
    /// is left alone so the build cache stays valid.
    /// </summary>
    public static string Write()
    {
        var summary = ReadSources();
        var path = System.IO.Path.Combine(Application.dataPath, "..", AssetPath);
        if (!System.IO.File.Exists(path) || System.IO.File.ReadAllText(path) != summary)
        {
            System.IO.File.WriteAllText(path, summary);
            UnityEditor.AssetDatabase.ImportAsset(AssetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
        }
        return summary;
    }
#endif
}

#if UNITY_EDITOR
// Before the data build rather than per scene: a pre-build callback runs on every build, cached
// or not, and the changed text asset invalidates exactly the content that carries it.
public sealed class OctopusSampleNativePinsProcessor : UnityEditor.Build.IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
    {
        try
        {
            OctopusSampleNativePins.Write();
        }
        catch (System.Exception error)
        {
            throw new UnityEditor.Build.BuildFailedException(error);
        }
    }
}
#endif
