using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Batchmode entry points for a future store-publishing CI pipeline; all inputs arrive via
// environment variables. NOT wired to any GitHub Actions workflow yet.
//
// Lives under Assets/Editor/ like every other Editor-only script in this project (no asmdef
// here, same as LegacyPackageExporter.cs / IOSPushPostProcess.cs) so it is never part of a
// player build. It is also outside ci/compile-check/'s scope on purpose: that gate only
// mirrors UnityPackage/ (see ci/compile-check/README.md "Scope"), so this file cannot be
// exercised by `dotnet build` against the hand-written stubs — it needs a real, licensed
// Unity Editor, which this project does not have wired into CI today.
//
// Invocation (once a licensed Editor is available):
//   Unity -batchmode -quit -projectPath UnityExample \
//         -executeMethod BuildScript.BuildAndroid -logFile -
//   Unity -batchmode -quit -projectPath UnityExample \
//         -executeMethod BuildScript.BuildIOS -logFile -
//
// Every build input arrives through an environment variable, never a command-line argument
// that would show up in a process listing or a log grep. A required variable that is missing
// or empty fails loud (EditorApplication.Exit(1) after a clear error) — it never silently
// falls back to a debug signing config the way local `flutter run --release` is allowed to.
//
// NOTE: BuildAndroid writes the keystore/key passwords into PlayerSettings.Android, which Unity
// serialises into ProjectSettings/ProjectSettings.asset. Any CI runner invoking this on a
// persistent (non-ephemeral) machine must restore that file afterwards (e.g.
// `git checkout -- UnityExample/ProjectSettings/ProjectSettings.asset` in an `if: always()`
// cleanup step) so the plaintext passwords do not linger in the working tree between runs.
public static class BuildScript
{
    // ===== Android =====
    //
    // Produces a signed .aab (Play requires App Bundles for new apps). Reuses the same
    // KEYSTORE_* secret names as this org's other sample-app CI pipelines, so the workflow
    // template stays identical across repos (Play App Signing re-signs the upload key anyway,
    // so only the secret *names* need to match — not the underlying keystore values).
    public static void BuildAndroid()
    {
        try
        {
            var keystoreFile = RequireEnv("KEYSTORE_FILE");
            var keystorePassword = RequireEnv("KEYSTORE_PASSWORD");
            var keyAlias = RequireEnv("KEY_ALIAS");
            var keyPassword = RequireEnv("KEY_PASSWORD");
            var versionCode = int.Parse(RequireEnv("CI_VERSION_CODE"));
            var outputPath = RequireEnv("OUTPUT_PATH"); // e.g. build/android/UnityExample.aab

            PlayerSettings.Android.keystoreName = keystoreFile;
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = keyPassword;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.bundleVersionCode = versionCode;

            // Play Console no longer accepts new APK uploads for this applicationId — an AAB
            // is mandatory. EditorUserBuildSettings, not a BuildOptions flag: buildAppBundle
            // has no BuildOptions equivalent, it is read from user build settings at
            // BuildPipeline.BuildPlayer time.
            EditorUserBuildSettings.buildAppBundle = true;

            var report = BuildPipeline.BuildPlayer(BuildPlayerOptionsFor(BuildTarget.Android, outputPath));
            ExitOnResult(report);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] BuildAndroid failed: {e}");
            EditorApplication.Exit(1);
        }
    }

    // ===== iOS =====
    //
    // Unity does not produce a signed .ipa directly: BuildPipeline.BuildPlayer(iOS) only
    // generates the Xcode project at OUTPUT_PATH. Archiving, signing (fastlane match) and
    // TestFlight upload are a SEPARATE step that runs Xcode itself and therefore needs a
    // macOS runner. The generated project still needs
    // UnityPackage/Editor/ruby/patch_xcode_proj.rb run against it afterwards
    // (same script the manual release process already depends on) before it links the
    // native Octopus Swift package — that call is NOT made here, it belongs to the fastlane
    // lane alongside `match` and `build_app`, one step after this method returns.
    public static void BuildIOS()
    {
        try
        {
            var buildNumber = RequireEnv("APP_BUILD_NUMBER");
            var outputPath = RequireEnv("OUTPUT_PATH"); // e.g. build/ios (a directory)

            PlayerSettings.iOS.buildNumber = buildNumber;

            var report = BuildPipeline.BuildPlayer(BuildPlayerOptionsFor(BuildTarget.iOS, outputPath));
            ExitOnResult(report);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] BuildIOS failed: {e}");
            EditorApplication.Exit(1);
        }
    }

    // ===== Shared =====

    private static BuildPlayerOptions BuildPlayerOptionsFor(BuildTarget target, string outputPath)
    {
        return new BuildPlayerOptions
        {
            // Same scene list EditorBuildSettings already carries (ProjectSettings/
            // EditorBuildSettings.asset) — not hardcoded here, so a scene added or removed
            // through the Editor's Build Settings window is picked up without touching this
            // file.
            scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray(),
            locationPathName = outputPath,
            target = target,
            targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            options = BuildOptions.None,
        };
    }

    private static void ExitOnResult(UnityEditor.Build.Reporting.BuildReport report)
    {
        var result = report.summary.result;
        if (result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] Build succeeded: {report.summary.outputPath} ({report.summary.totalSize} bytes)");
            EditorApplication.Exit(0);
        }
        else
        {
            // Unity's own -batchmode -quit exit code is not reliable across versions for a
            // failed BuildPipeline.BuildPlayer call (some report 0). Decide the exit code from
            // the BuildReport instead of trusting the process to do it.
            Debug.LogError($"[BuildScript] Build finished with result {result}");
            EditorApplication.Exit(1);
        }
    }

    private static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Required environment variable '{name}' is missing or empty. " +
                "This entry point never falls back to a default signing config or version.");
        }
        return value;
    }
}
