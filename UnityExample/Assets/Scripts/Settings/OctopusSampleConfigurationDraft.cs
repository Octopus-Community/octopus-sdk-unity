using System;

// Plain in-memory form state. Never serialize credentials or put them in PlayerPrefs.
public sealed class OctopusSampleConfigurationDraft
{
    public int Community = 1;
    public bool CustomServer;
    public string CustomHost = "";
    public string CustomApiKey = "";
    public string UserId;
    public bool UserSelected;
    public bool NavyTheme = true;
    // null preserves the current appearance; 0 follows system in both the shell and native SDK.
    public int? Appearance;

    public static readonly string[] DemoUsers = { "android-sample-user", "qa-tester-ios", "qa-tester-android" };
    public static readonly string[] CommunityLabels =
    {
        "SSO · no managed fields · no forced login",
        "SSO · no managed fields · forced login",
        "SSO · all managed fields · forced login",
        "SSO · some managed fields · forced login",
        "Custom… (paste any API key)"
    };

    public bool ForceLogin { get { return Community >= 2 && Community <= 4; } }
    public ProfileField[] ManagedFields
    {
        get
        {
            if (Community == 3) return new[] { ProfileField.NICKNAME, ProfileField.BIO, ProfileField.PICTURE };
            if (Community == 4) return new[] { ProfileField.NICKNAME };
            return new ProfileField[0];
        }
    }

    public string CommunityLabel
    {
        get { return Community >= 1 && Community <= 5 ? CommunityLabels[Community - 1] : "Unavailable"; }
    }

    public OctopusSampleConfigurationDraft Copy()
    {
        return (OctopusSampleConfigurationDraft)MemberwiseClone();
    }

    public string ResolveHost(string injectedDemoHost)
    {
        // Demo never consumes CustomHost: even if a previous build saved an obsolete demo host.
        return CustomServer ? (CustomHost ?? "").Trim().ToLowerInvariant()
            : OctopusExampleConfig.ResolveApiServerHost(injectedDemoHost);
    }

    public static bool ValidCustomHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Trim().Length > 253) return false;
        foreach (var label in host.Trim().Split('.'))
        {
            if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-') return false;
            foreach (var c in label)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-') return false;
        }
        return true;
    }

    public static string MaskApiKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "Not configured" : "••••••••";
    }

    public override string ToString()
    {
        return CommunityLabel + " · " + (CustomServer ? "Custom host" : "Demo") + " · key=" + MaskApiKey(CustomApiKey);
    }
}
