using System;
using System.Collections.Generic;

public sealed class PushNotificationsScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("postId", "Sample notification target", true));
    private readonly List<OctopusScenarioPreset> _presets;
    public PushNotificationsScenario() : base("pushNotifications")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            new OctopusScenarioPreset(PresetTestId(1), PresetLabel(1, "Open sample notification (deep link)"),
                fields => fields.Set("postId", OctopusSampleFixtures.PostTextId ?? string.Empty), fields => Run(fields.Get("postId")))
        };
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "IsOctopusNotification", "GetOctopusNotification", "Open(OctopusNotification)" }; } }
    public override string ParameterNotice
    {
        get { return "Replays a bundled notification without an FCM/APNs push or token registration. Targets post.text through the native notification payload. The external deeplink.post fixture is still TBD."; }
    }
    private void Run(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId)) { Report("Skipped: post.text has no id."); return; }
        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("OpenNotification", out busy)) { Report(busy); return; }
        var token = OctopusScenarioSdk.OperationToken;
        try
        {
            string mode;
            if (OctopusScenarioSdk.EnsureInitialized(OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode) == null)
            { Report(mode); return; }
            var payload = new Dictionary<string, string>
            {
                { "is_octopus_notification", "true" },
                { "title", "New activity in the demo community" },
                { "body", "Tap to open the post this notification points at." },
                { "link_path", "post/" + postId },
                { "post_id", postId }
            };
            var sdk = OctopusScenarioSdk.Current;
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.IsOctopusNotification", "bundled sample payload");
            if (!sdk.IsOctopusNotification(payload))
            { Report("Sample payload not recognised as an Octopus notification."); return; }
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.GetOctopusNotification", "bundled sample payload");
            var notification = sdk.GetOctopusNotification(payload);
            if (notification == null)
            { Report("Failed to parse the sample notification payload."); return; }
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.Open(OctopusNotification)", "bundled sample notification");
            sdk.Open(notification);
            Report("Opening Octopus via notification → post.text: " + postId + ".");
        }
        catch (Exception error) { Report("Open notification failed: " + error.Message); }
        finally { OctopusScenarioSdk.EndOperation(token); }
    }
}
