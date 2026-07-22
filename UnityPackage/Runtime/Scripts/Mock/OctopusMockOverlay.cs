#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-only, Play-mode IMGUI panel that shows the simulated Octopus screen and
/// exposes buttons that drive mock callbacks. Uses IMGUI (OnGUI) deliberately so it
/// needs no Canvas/EventSystem and is input-system-agnostic.
/// </summary>
internal class OctopusMockOverlay : MonoBehaviour
{
    static OctopusMockOverlay _instance;
    GUIStyle _titleStyle;

    internal static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject("OctopusMockOverlay");
        if (Application.isPlaying)
            Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<OctopusMockOverlay>();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void OnGUI()
    {
        if (!OctopusSDK.Mock.ShowOverlay) return;
        // Lazy-init here: GUI.skin is only valid inside an OnGUI call.
        _titleStyle ??= new GUIStyle(GUI.skin.label) { richText = true };
        const int w = 280, pad = 8;
        var rect = new Rect(Screen.width - w - pad, pad, w, 260);
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("<b>Octopus SDK — Mock Mode</b>", _titleStyle);
        GUILayout.Label($"Screen: {OctopusSDK.Mock.CurrentScreen ?? "(none)"}");
        GUILayout.Label($"Recorded calls: {OctopusSDK.Mock.Calls.Count}");
        GUILayout.Space(6);
        if (GUILayout.Button("Emit not-seen count (= call count)"))
            OctopusSDK.Mock.EmitNotSeenCount(OctopusSDK.Mock.Calls.Count);
        if (GUILayout.Button("Emit login required"))
            OctopusSDK.Mock.EmitLoginRequired();
        if (GUILayout.Button("Navigate to client object \"demo\""))
            OctopusSDK.Mock.EmitNavigateToClientObject("demo");
        if (GUILayout.Button("Emit groups changed (empty)"))
            OctopusSDK.Mock.EmitGroupsChanged(new List<OctopusGroup>());
        if (GUILayout.Button($"Toggle community access (now: {OctopusSDK.HasAccessToCommunity})"))
            OctopusSDK.Mock.EmitHasAccessToCommunity(!OctopusSDK.HasAccessToCommunity);
        GUILayout.EndArea();
    }
}
#endif
