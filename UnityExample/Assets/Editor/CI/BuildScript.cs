using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
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
//   Unity -batchmode -nographics -projectPath UnityExample \
//         -executeMethod BuildScript.BuildAndroidDebug -logFile -      (QA debug APK, no secrets)
//   Unity -batchmode -nographics -quit -projectPath UnityExample \
//         -executeMethod BuildScript.BuildIOSSimulator -logFile -
//
// Every build input arrives through an environment variable, never a command-line argument
// that would show up in a process listing or a log grep. A required variable that is missing
// or empty fails loud (EditorApplication.Exit(1) after a clear error) — it never silently
// falls back to a debug signing config the way local `flutter run --release` is allowed to.
//
// NOTE: BuildAndroid writes the keystore/key passwords into PlayerSettings.Android, which Unity
// serialises into ProjectSettings/ProjectSettings.asset. Whoever invokes this must put that
// tracked file back afterwards so the plaintext passwords do not linger in the working tree.
// `Scripts/store/publish-android.sh` does it from a snapshot taken before the build, on every
// exit path — a snapshot rather than a checkout, because these builds now run on a developer's
// own machine, where that file may carry legitimate uncommitted edits.
public static class BuildScript
{
    // ===== Android =====
    //
    // Produces a signed .aab (Play requires App Bundles for new apps). Driven by
    // `Scripts/store/publish-android.sh`; there is no CI equivalent, because a store build
    // needs a licensed Unity Editor and no runner has one (issue #28). The KEYSTORE_* names
    // are kept identical to this org's other sample apps so the same keystore.properties can
    // be reused verbatim (Play App Signing re-signs the upload key anyway, so only the *names*
    // need to match — not the underlying keystore values).
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

            var target = NamedBuildTarget.Android;
            var previousDefines = PlayerSettings.GetScriptingDefineSymbols(target);
            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                PlayerSettings.SetScriptingDefineSymbols(target, WithInternalDefine(
                    previousDefines, Environment.GetEnvironmentVariable("OCTOPUS_INTERNAL")));
                report = BuildPipeline.BuildPlayer(BuildPlayerOptionsFor(BuildTarget.Android, outputPath));
            }
            finally
            {
                // Restore before ExitOnResult exits the Editor, including when BuildPlayer throws.
                PlayerSettings.SetScriptingDefineSymbols(target, previousDefines);
            }
            ExitOnResult(report);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] BuildAndroid failed: {e}");
            EditorApplication.Exit(1);
        }
    }

    // Unsigned debug APK for on-device QA (pm-tools `qa/qa.sh unity build`, which invokes
    // `-executeMethod BuildScript.BuildAndroidDebug` from the repo root and then installs
    // UnityExample/build/android/UnityExample.apk). No keystore, no version code, no secret:
    // the debug keystore Unity ships is used and restored afterwards, so this never leaves the
    // custom-keystore flag flipped in ProjectSettings.asset on a persistent machine.
    //
    //   Unity -batchmode -nographics -projectPath UnityExample \
    //         -executeMethod BuildScript.BuildAndroidDebug -logFile -
    //
    // OUTPUT_PATH is optional (default build/android/UnityExample.apk, relative to the project);
    // OCTOPUS_INTERNAL=true adds the internal define exactly like BuildAndroid.
    public static void BuildAndroidDebug()
    {
        var useCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        var buildAppBundle = EditorUserBuildSettings.buildAppBundle;
        var target = NamedBuildTarget.Android;
        var previousDefines = PlayerSettings.GetScriptingDefineSymbols(target);
        try
        {
            var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH");
            if (string.IsNullOrEmpty(outputPath)) outputPath = "build/android/UnityExample.apk";

            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetScriptingDefineSymbols(target, WithInternalDefine(
                previousDefines, Environment.GetEnvironmentVariable("OCTOPUS_INTERNAL")));

            var options = BuildPlayerOptionsFor(BuildTarget.Android, outputPath);
            options.options = BuildOptions.Development;
            var report = BuildPipeline.BuildPlayer(options);

            RestoreAndroidDebugSettings(useCustomKeystore, buildAppBundle, previousDefines);
            ExitOnResult(report);
        }
        catch (Exception e)
        {
            RestoreAndroidDebugSettings(useCustomKeystore, buildAppBundle, previousDefines);
            Debug.LogError($"[BuildScript] BuildAndroidDebug failed: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static void RestoreAndroidDebugSettings(bool useCustomKeystore, bool buildAppBundle, string defines)
    {
        PlayerSettings.Android.useCustomKeystore = useCustomKeystore;
        EditorUserBuildSettings.buildAppBundle = buildAppBundle;
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, defines);
    }

    // Pure so EditMode tests can check both directions without changing PlayerSettings.
    public static string WithInternalDefine(string defines, string environmentValue)
    {
        const string internalDefine = "OCTOPUS_INTERNAL";
        var symbols = (defines ?? string.Empty).Split(';')
            .Select(symbol => symbol.Trim())
            .Where(symbol => symbol.Length != 0 && symbol != internalDefine)
            .ToList();
        if (string.Equals(environmentValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            symbols.Add(internalDefine);
        }
        return string.Join(";", symbols);
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

    // Exports an iOS Simulator Xcode project for local QA. OUTPUT_PATH is optional
    // (default build/ios-sim, relative to the Unity project), like BuildAndroidDebug.
    // Build and install the exported workspace separately with signing disabled.
    public static void BuildIOSSimulator()
    {
        try
        {
            var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH");
            if (string.IsNullOrEmpty(outputPath)) outputPath = "build/ios-sim";

            var previousSdk = PlayerSettings.iOS.sdkVersion;
            var previousSimulatorArch = PlayerSettings.iOS.simulatorSdkArchitecture;
            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                // The project default is x86_64, which Xcode refuses as a destination on an
                // Apple Silicon simulator. Match the host so the exported libraries are usable.
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                var options = BuildPlayerOptionsFor(BuildTarget.iOS, outputPath);
                options.options = BuildOptions.Development;
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                // Restore before ExitOnResult exits the Editor, including on build failure.
                PlayerSettings.iOS.sdkVersion = previousSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousSimulatorArch;
            }
            ExitOnResult(report);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildScript] BuildIOSSimulator failed: {e}");
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
