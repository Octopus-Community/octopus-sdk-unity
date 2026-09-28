using System;

// Decides the `aps-environment` entitlement value the iOS export is written with.
//
// Deliberately NOT inside `#if UNITY_IOS`, unlike IOSPushPostProcess.cs which consumes it:
// that guard only holds when the active build target is iOS, so a helper defined under it
// disappears from Assembly-CSharp-Editor on an Android or Standalone target and could not be
// covered by the EditMode suite. Keeping the decision here makes it testable on any target;
// the guarded post-process keeps only the Xcode plumbing.
//
// Why this exists at all: a store build signed with an App Store provisioning profile is
// REJECTED when the entitlement says `development`. The sibling samples solve the same
// problem with two checked-in entitlement files selected by build configuration (Flutter:
// `Runner.entitlements` / `Runner-Release.entitlements`; React Native ships `production`
// outright). Unity regenerates the whole Xcode project on every export, so there is no file
// to check in — the equivalent lever is an environment variable read at export time, which
// `Scripts/store/publish-ios.sh` sets to `production`.
public static class IOSPushEntitlements
{
    // Read by OnPostProcessBuild. Unset is the common case (a developer exporting for their
    // own device), so it must keep meaning `development`.
    public const string EnvironmentVariable = "OCTOPUS_IOS_APS_ENVIRONMENT";

    /// <summary>
    /// True when the export must carry `aps-environment = development`.
    /// </summary>
    /// <remarks>
    /// Unset or empty means development: a local export is signed with an Apple Development
    /// profile, which an App Store entitlement value would make unsignable. A store export
    /// opts in explicitly. Any other value throws rather than silently picking a side — a
    /// typo here costs a full re-export plus an App Store Connect rejection.
    /// </remarks>
    public static bool UseDevelopmentEnvironment(string environmentValue)
    {
        var value = (environmentValue ?? string.Empty).Trim();
        if (value.Length == 0 || string.Equals(value, "development", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(value, "production", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        throw new InvalidOperationException(
            $"{EnvironmentVariable} must be 'development', 'production' or unset, got: '{value}'");
    }
}
