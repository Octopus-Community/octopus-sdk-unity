#if UNITY_IOS && UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;
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
        capabilityManager.AddPushNotifications(true); // true = development

        // ---------------------------
        // 3. Add Background Modes (remote notifications only)
        // ---------------------------
        capabilityManager.AddBackgroundModes(
            BackgroundModesOptions.RemoteNotifications
        );

        capabilityManager.WriteToFile();

        Debug.Log("iOS Push entitlements configured.");
    }
}
#endif