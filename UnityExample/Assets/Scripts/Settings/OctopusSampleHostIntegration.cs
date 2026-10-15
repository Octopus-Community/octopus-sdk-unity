using System;
using UnityEngine;

public static class OctopusSampleHostIntegration
{
    internal static string Prefix = "OctopusSample.HostIntegration.";
    public static bool ClientObject { get { return PlayerPrefs.GetInt(Prefix + "ClientObject", 1) == 1; } }
    public static bool GroupAccess { get { return PlayerPrefs.GetInt(Prefix + "GroupAccess", 1) == 1; } }
    public static int UrlMode { get { var mode = PlayerPrefs.GetInt(Prefix + "Url", 0); return mode >= 0 && mode <= 2 ? mode : 0; } }
    public static string LastCallback { get; private set; }
    public static event Action Changed;

    public static void SetCallback(string key, bool enabled)
    {
        PlayerPrefs.SetInt(Prefix + key, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
        Raise();
    }
    public static void SetUrlMode(int mode)
    {
        if (mode < 0 || mode > 2) return;
        PlayerPrefs.SetInt(Prefix + "Url", mode);
        PlayerPrefs.Save();
        Apply();
        Raise();
    }
    public static void Apply()
    {
        if (!OctopusSampleState.IsInitialized) return;
        Detach();
        _urlMode = UrlMode;
        // Host callbacks are wired on the SDK itself, like OnLoginRequired: the scenario seam
        // records the calls a scenario makes, and this standing wiring is not one of them.
        OctopusSDK.OnGroupAccessDenied += GroupDenied;
        // Login is always wired in SSO; there is no switch that would strand a required login.
        OctopusSDK.OnLoginRequired += LoginRequired;
        OctopusSDK.NavigateToUrlHandler = UrlMode == 2 ? null : (Func<string, UrlOpeningStrategy>)ResolveUrl;
    }
    internal static void Detach()
    {
        OctopusSDK.OnGroupAccessDenied -= GroupDenied;
        OctopusSDK.OnLoginRequired -= LoginRequired;
        OctopusSDK.NavigateToUrlHandler = null;
    }
    public static void Clear()
    {
        Detach();
        foreach (var key in new[] { "ClientObject", "GroupAccess", "Url" }) PlayerPrefs.DeleteKey(Prefix + key);
        PlayerPrefs.Save();
        LastCallback = null;
        Raise();
    }
    private static void LoginRequired() { OctopusSampleAccountView.Open(); }
    private static void GroupDenied(string id)
    {
        if (!GroupAccess) return;
        LastCallback = "The host received a denied group request.";
        OctopusSampleLog.Current.LogStateChange("Host group access denied", "Callback received");
        OctopusMainThread.Post(() => OctopusSampleHostCallbackView.Open("Group access denied", LastCallback));
        Raise();
    }
    public static void ContentTapped(string objectId)
    {
        if (!ClientObject) return;
        string run; int score;
        if (OctopusReefRunShare.TryParse(objectId, out run, out score)) return;
        LastCallback = "The host received a content button tap.";
        OctopusSampleHostCallbackView.Open("Content button", LastCallback);
        Raise();
    }
    public static UrlOpeningStrategy ResolveUrl(string url)
    {
        // iOS asks synchronously off the Unity thread. Decide from plain preferences cached below,
        // and dispatch all Unity UI/browser work to the player loop.
        if (DecideUrl(_urlMode, url) == UrlOpeningStrategy.HandledByOctopus) return UrlOpeningStrategy.HandledByOctopus;
        OctopusMainThread.Post(() =>
        {
            LastCallback = "The app handled a link from the community.";
            OctopusSampleHostCallbackView.Open("Link opened in the app", LastCallback);
            Raise();
        });
        return UrlOpeningStrategy.HandledByApp;
    }
    internal static UrlOpeningStrategy DecideUrl(int mode, string url)
    {
        Uri uri;
        var contact = Uri.TryCreate(url, UriKind.Absolute, out uri) &&
            (uri.Scheme == "mailto" || (uri.Host == "octopuscommunity.com" && uri.AbsolutePath == "/contact"));
        return mode == 1 || (mode == 0 && contact) ? UrlOpeningStrategy.HandledByApp : UrlOpeningStrategy.HandledByOctopus;
    }
    private static volatile int _urlMode;
    private static void Raise()
    {
        _urlMode = UrlMode;
        var handler = Changed;
        if (handler != null) handler();
    }
}
