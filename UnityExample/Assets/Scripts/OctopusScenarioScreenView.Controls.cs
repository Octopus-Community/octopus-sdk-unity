using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class OctopusScenarioScreenView
{
    private bool _backendOpen;
    private RectTransform _backendChoices;
    private TMP_Text _unifiedEffect;
    private RectTransform _unifiedSwitchHost;
    private TMP_Text _connectionState;
    private TMP_Text _connected;

    private void BuildUnifiedControls(RectTransform bench)
    {
        SampleUi.FlexibleLabel(bench,
            "Unified Profile needs both the community's backend flag and a host profile callback. " +
            "Turn it on to force the flag and wire the sample's Client profile screen. " +
            "Turn it off to clear the override and remove the callback, restoring native profiles.",
            SampleUi.TextBody, SampleUi.Muted);
        var row = SampleUi.Panel(OctopusScenariosListView.UnifiedProfileRouteRowId, bench, OctopusSampleBranding.Clear);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var copy = SampleUi.Panel("Copy", row, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(copy, OctopusSampleBranding.Dp(4f), new RectOffset(), false);
        copy.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        SampleUi.FlexibleLabel(copy, "Unified profile", SampleUi.TextBody, SampleUi.TitleColor);
        _unifiedEffect = SampleUi.FlexibleLabel(copy, "", SampleUi.TextCaption, SampleUi.Muted);
        _unifiedEffect.name = OctopusScenariosListView.UnifiedProfileLiveValueId;
        _unifiedSwitchHost = SampleUi.Panel("UnifiedSwitch", row, OctopusSampleBranding.Clear);
        _unifiedSwitchHost.gameObject.AddComponent<LayoutElement>().preferredWidth = OctopusSampleBranding.Dp(64f);
        RefreshUnifiedControls();
        var note = SampleUi.FlexibleLabel(bench,
            "Applies the flag and profile callback immediately; no SDK restart is needed in Unity. " +
            "Members without an app id keep the native profile flow.", SampleUi.TextCaption, SampleUi.Muted);
        note.name = "unified-profile-restart-note";
        var toggle = SampleUi.Button("unified-profile-backend-toggle", bench, "Backend flag override",
            SampleUiButtonVariant.Tertiary, () =>
            {
                _backendOpen = !_backendOpen;
                _backendChoices.gameObject.SetActive(_backendOpen);
            });
        SampleUi.FlexibleLabel(toggle, "⌄", SampleUi.TextCaption, SampleUi.Muted).name = "Chevron";
        _backendChoices = SampleUi.Card(OctopusScenariosListView.UnifiedProfileId, bench);
        SampleUi.FlexibleLabel(_backendChoices, "Inspect either condition independently. Use the backend value, or force the community flag on or off.",
            SampleUi.TextCaption, SampleUi.Muted);
        OverrideChoice(OctopusScenariosListView.UnifiedProfileBackendId, null);
        OverrideChoice(OctopusScenariosListView.UnifiedProfileForceOnId, true);
        OverrideChoice(OctopusScenariosListView.UnifiedProfileForceOffId, false);
        _backendChoices.gameObject.SetActive(_backendOpen);
    }

    private void OverrideChoice(string id, bool? value)
    {
        SampleUi.Button(id, _backendChoices, OctopusSampleUnifiedProfile.OverrideLabel(value),
            SampleUiButtonVariant.Secondary, () => OctopusSampleUnifiedProfile.SetOverride(value));
    }

    private void RefreshUnifiedControls()
    {
        if (_unifiedEffect == null || _unifiedSwitchHost == null) return;
        var active = OctopusSampleUnifiedProfile.RouteWired && OctopusSampleUnifiedProfile.Effective == true;
        _unifiedEffect.text = active ? "Profile taps open Client profile" :
            OctopusSampleUnifiedProfile.Effective.HasValue ? "Profile taps open native profiles" :
            "Community flag unavailable — " + OctopusSampleUnifiedProfile.EffectiveText;
        // Only the small switch is replaced on an observed flag change; the scroll/input tree stays.
        for (var i = _unifiedSwitchHost.childCount - 1; i >= 0; i--)
        {
            var child = _unifiedSwitchHost.GetChild(i);
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        SampleUi.Switch("config-unified-profile", _unifiedSwitchHost, active, true, () =>
        {
            OctopusSampleUnifiedProfile.SetRouteWired(!active);
            OctopusSampleUnifiedProfile.SetOverride(active ? (bool?)null : true);
        });
    }

    private void VerifyInCommunity()
    {
        string reason;
        if (OctopusScenarioSdk.EnsurePilotInitialized(out reason) == null)
        {
            if (_resultLabel != null) _resultLabel.text = reason;
            else _unifiedEffect.text = reason;
            return;
        }
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.Open");
        OctopusScenarioSdk.Current.Open();
    }

    private void BuildConnectionControls(RectTransform bench)
    {
        var live = Section(bench, "Live state");
        _connectionState = SampleUi.FlexibleLabel(live, "", SampleUi.TextBody, SampleUi.TitleColor);
        _connectionState.name = "connection-live-state";
        _connected = SampleUi.FlexibleLabel(live, "", SampleUi.TextBody, SampleUi.TitleColor);
        _connected.name = "connection-is-user-connected";
        SampleUi.FlexibleLabel(live,
            "Unity exposes CurrentProfile and its guest flag, not the native connectionState / isUserConnected streams. " +
            "These rows describe that observed profile, not completion of the sign-in call.", SampleUi.TextCaption, SampleUi.Muted);
        RefreshConnection();
        ConnectionAction(bench, "connectUser()", "connection-connect-execute", 0);
        ConnectionAction(bench, "disconnectUser()", "connection-disconnect-execute", 4);
    }

    private void ConnectionAction(RectTransform bench, string title, string id, int preset)
    {
        var card = Section(bench, title);
        var button = SampleUi.Button(id, card, "Execute", () =>
        {
            if (_pilot.IsRunning) return;
            ApplyPreset(_pilot.Presets[preset], false);
            ExecuteCurrent();
        });
        _runButtons.Add(button.GetComponent<Button>());
    }

    private void OnConnectionProfileChanged(OctopusProfile profile)
    {
        if (this == null)
        {
            OctopusScenarioSdk.Current.ProfileChanged -= OnConnectionProfileChanged;
            return;
        }
        RefreshConnection();
    }

    private void RefreshConnection()
    {
        if (_connectionState == null) return;
        var profile = OctopusScenarioSdk.Current.CurrentProfile;
        _connectionState.text = "connectionState · " + (profile == null ? "No profile reported" :
            profile.IsGuest ? "Guest profile" : "Connected profile (" + (profile.ClientUserId ?? "no app id") + ")");
        _connected.text = "isUserConnected · " + (profile == null ? "unknown" : profile.IsGuest ? "false (guest)" : "true (from CurrentProfile)");
    }
}
