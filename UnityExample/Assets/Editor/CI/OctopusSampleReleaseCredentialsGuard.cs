using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Prevents local demo credentials from entering any distributable sample build.</summary>
public sealed class OctopusSampleReleaseCredentialsGuard : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -1000; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        Validate(OctopusExampleConfig.LoadedOrNull, report.summary.options);
    }

    public static void Validate(OctopusExampleConfig config, BuildOptions options)
    {
        if ((options & BuildOptions.Development) != 0 || config == null) return;
        if (!string.IsNullOrEmpty(config.ssoTokenSecret)) Refuse("ssoTokenSecret");
        for (var selection = 1; selection <= 4; selection++)
        {
            var profile = config.ConfigurationProfile(selection);
            if (profile != null && !string.IsNullOrEmpty(profile.authToken)) Refuse("authToken");
        }
    }

    private static void Refuse(string field)
    {
        throw new BuildFailedException("Non-Development builds must not contain " + field +
            ". Clear it in OctopusExampleConfig before building; obtain SSO tokens from a backend.");
    }
}
