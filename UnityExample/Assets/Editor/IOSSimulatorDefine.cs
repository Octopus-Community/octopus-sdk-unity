#if UNITY_IOS && UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS;

public class IOSSimulatorDefine : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS)
            return;

        bool isSimulator =
            PlayerSettings.iOS.sdkVersion ==
            iOSSdkVersion.SimulatorSDK;

        var target = NamedBuildTarget.iOS;

        string defines =
            PlayerSettings.GetScriptingDefineSymbols(target);

        if (isSimulator && !defines.Contains("IOS_SIMULATOR"))
        {
            PlayerSettings.SetScriptingDefineSymbols(
                target,
                defines + ";IOS_SIMULATOR");
        }
        else if (!isSimulator && defines.Contains("IOS_SIMULATOR"))
        {
            defines = defines.Replace("IOS_SIMULATOR", "");
            PlayerSettings.SetScriptingDefineSymbols(
                target,
                defines);
        }
    }
}
#endif // UNITY_IOS && UNITY_EDITOR