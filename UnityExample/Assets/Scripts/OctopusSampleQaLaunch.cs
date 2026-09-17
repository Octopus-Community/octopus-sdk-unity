using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Implemented by the active shell, independently of its UI framework.</summary>
public interface IOctopusSampleQaNavigation
{
    void SelectTab(string id);
    bool IsTabReady(string id);
    IOctopusSampleQaScenario OpenScenario(string id);

    /// <summary>
    /// Opens a sample-owned screen (<see cref="OctopusSampleQaDestinations"/>) and reports whether
    /// it could. Separate from <see cref="OpenScenario"/> because a destination returns no pilot:
    /// it has no presets and no result line for QA to read.
    /// </summary>
    bool OpenDestination(string id);
}

public interface IOctopusSampleQaScenario
{
    bool RunPreset(int number);
}

/// <summary>One launch request, consumed before navigation or Start can re-enter it.</summary>
public sealed class OctopusSampleQaRequest
{
    private readonly OctopusSampleQaLaunchOptions _options;
    private bool _applied;
    private bool _started;

    public OctopusSampleQaRequest(OctopusSampleQaLaunchOptions options)
    {
        _options = options;
    }

    public void ConfigShown(Action start)
    {
        if (_started || !_options.AutoStart || _options.Error != null) return;
        _started = true;
        start();
    }

    public IEnumerator Apply(IOctopusSampleQaNavigation navigation)
    {
        if (_applied || _options.Error != null) yield break;
        _applied = true;
        if (_options.Scenario != null && !OctopusScenarioPilots.Has(_options.Scenario))
        {
            OctopusSampleQaLaunch.Error("unknown scenario");
            yield break;
        }
        var tab = _options.Scenario != null ? "scenarios"
            : _options.Destination != null ? OctopusSampleQaDestinations.TabOf(_options.Destination)
            : _options.Tab;
        if (tab == null) yield break;
        navigation.SelectTab(tab);
        while (!navigation.IsTabReady(tab)) yield return null;
        if (_options.Destination != null)
        {
            if (!navigation.OpenDestination(_options.Destination))
                OctopusSampleQaLaunch.Error("unknown destination");
            yield break;
        }
        if (_options.Scenario == null) yield break;
        var screen = navigation.OpenScenario(_options.Scenario);
        if (screen == null) OctopusSampleQaLaunch.Error("unknown scenario");
        else if (_options.Preset.HasValue && !screen.RunPreset(_options.Preset.Value))
            OctopusSampleQaLaunch.Error("unknown preset");
    }
}

/// <summary>Sample-only launch input and single-line logcat output.</summary>
public static class OctopusSampleQaLaunch
{
    private static OctopusSampleQaRequest _request;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        _request = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        var extras = new Dictionary<string, string>();
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                foreach (var key in new[] { "qaTab", "qaScenario", "qaPreset" })
                    if (intent.Call<bool>("hasExtra", key))
                        extras[key] = intent.Call<string>("getStringExtra", key);
                if (intent.Call<bool>("hasExtra", "qaAutoStart"))
                    extras["qaAutoStart"] = intent.Call<bool>("getBooleanExtra", "qaAutoStart", false)
                        ? "true" : "false";
            }
        }
        catch (Exception)
        {
            // Do not echo an intent or exception message: it can contain unrelated launch data.
            Error("cannot read Android intent");
            return;
        }
        var options = OctopusSampleQaLaunchOptions.Parse(extras);
        Log("launch-options=" + options.Summary);
        if (options.Error != null) Error(options.Error);
        _request = new OctopusSampleQaRequest(options);
#endif
    }

    // Call once the shell is built; run this iterator as a coroutine owned by that shell.
    public static IEnumerator ShellReady(IOctopusSampleQaNavigation navigation)
    {
        Log("shell=ready");
        if (_request != null) yield return _request.Apply(navigation);
    }

    // A Config screen supplies its existing Start action after loading persisted configuration.
    // The base sample has no Config screen. Its future Start handler must call ConfigStarted.
    public static void ConfigShown(Action start)
    {
        if (_request != null) _request.ConfigShown(start);
    }

    public static void ConfigStarted() { Log("config=started"); }
    public static void Error(string reason) { Log("launch-options=error " + reason); }

    public static string SingleLine(string text)
    {
        return string.Join(" ", (text ?? string.Empty).Split(
            new[] { '\r', '\n', '\u0085', '\u2028', '\u2029' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public static void Log(string marker)
    {
        Debug.Log("[OctopusQA] " + SingleLine(marker));
    }
}
