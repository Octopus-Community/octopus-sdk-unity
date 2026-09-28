#if UNITY_IOS && UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;
using System;
using System.IO;

public class IOSPushPostProcess
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        string projectPath = PBXProject.GetPBXProjectPath(path);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTarget = project.GetUnityMainTargetGuid();

        // ---------------------------
        // 1. Create / Ensure entitlements file
        // ---------------------------
        string entitlementsFileName = "Unity-iPhone.entitlements";
        string entitlementsPath = Path.Combine(path, entitlementsFileName);

        ProjectCapabilityManager capabilityManager =
            new ProjectCapabilityManager(projectPath, entitlementsFileName, null, mainTarget);

        // ---------------------------
        // 2. Add Push Notifications capability
        // ---------------------------
        // `aps-environment`: development unless OCTOPUS_IOS_APS_ENVIRONMENT says otherwise.
        // An App Store profile refuses a `development` entitlement, and an Apple Development
        // profile refuses a `production` one, so this cannot be hardcoded either way — see
        // IOSPushEntitlements. `Scripts/store/publish-ios.sh` exports the variable as
        // `production`; every other export keeps the previous behaviour.
        bool development;
        try
        {
            development = IOSPushEntitlements.UseDevelopmentEnvironment(
                Environment.GetEnvironmentVariable(IOSPushEntitlements.EnvironmentVariable));
        }
        catch (InvalidOperationException e)
        {
            // Fail the export rather than sign an archive the store will reject at the very
            // end of an upload.
            throw new BuildFailedException(e.Message);
        }
        capabilityManager.AddPushNotifications(development);

        // ---------------------------
        // 3. Add Background Modes (remote notifications only)
        // ---------------------------
        capabilityManager.AddBackgroundModes(
            BackgroundModesOptions.RemoteNotifications
        );

        capabilityManager.WriteToFile();

        // Export compliance exemption (#320): standard TLS via BoringSSL/swift-nio-ssl,
        // HMAC-SHA256 for authentication only (never bulk encryption), no encryption at rest.
        // Verified against the 1.13.0 TestFlight pins: octopus-sdk-swift 1.13.2,
        // firebase-ios-sdk 12.8.0, grpc-swift 1.27.6.
        string infoPlistPath = Path.Combine(path, "Info.plist");
        PlistDocument infoPlist = new PlistDocument();
        infoPlist.ReadFromFile(infoPlistPath);
        infoPlist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        infoPlist.WriteToFile(infoPlistPath);

        Debug.Log($"iOS Push entitlements configured (aps-environment: " +
                  $"{(development ? "development" : "production")}).");
    }
}
#endif