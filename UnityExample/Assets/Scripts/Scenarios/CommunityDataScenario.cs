using System;
using System.Collections.Generic;

public sealed class CommunityDataScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("action", "Action", true),
        new OctopusScenarioField("member", "Member source", true));
    private readonly List<OctopusScenarioPreset> _presets;
    // The SDK has a single native observer, so ownership must also be single across visits.
    private static CommunityDataScenario _observer;
    private IOctopusScenarioSdk _observedSdk;
    private string _lastProfileId;
    private int _generation;

    public CommunityDataScenario() : base("communityData")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Preset(1, "Fetch by clientUserId", "fetch-client", "current-or-configured"),
            Preset(2, "Fetch by profileId (from the last lookup)", "fetch-profile", "last-lookup"),
            Preset(3, "Observe by clientUserId (start)", "observe", "current-or-configured"),
            Preset(4, "Stop observing", "stop", "observed-member"),
            Preset(5, "Contract: both / neither id throws", "contract", "local-contract"),
            Preset(6, "Open the host-rendered profile page", "host-profile", "current-or-configured"),
        };
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols
    {
        get { return new[] { "FetchCommunityData", "StartObservingCommunityData", "StopObservingCommunityData",
            "OnCommunityDataChanged", "CurrentProfile", "OctopusCommunityMemberId" }; }
    }
    public override string ParameterNotice
    {
        get { return "Client id lookups require exposed client user ids. Public gamification score is normally null. " +
            "The C# factories enforce exactly one id by construction. Host profile navigation needs a renderer " +
            "subscribed to HostProfileRequested; the pilot supplies its data, unknown-member and error states."; }
    }

    private OctopusScenarioPreset Preset(int index, string label, string action, string member)
    {
        return new OctopusScenarioPreset(PresetTestId(index), PresetLabel(index, label), fields =>
        {
            fields.Set("action", action);
            fields.Set("member", member);
        }, Run);
    }

    private void Run(OctopusScenarioFields fields)
    {
        var action = fields.Get("action");
        if (action == "contract") { CheckContract(); return; }
        if (action == "stop")
        {
            var wasObserving = _observedSdk != null;
            StopObservation();
            Report(wasObserving ? "Cancelled the subscription — the native observation is torn down." :
                "Nothing was being observed.");
            return;
        }
        if (action == "fetch-profile" && _lastProfileId == null)
        {
            Report("No profileId known yet — run Preset 1 or 3 first.");
            return;
        }
        string reason;
        var profile = OctopusScenarioSdk.EnsurePilotInitialized(out reason);
        if (profile == null) { Report(reason); return; }
        var sdk = OctopusScenarioSdk.Current;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.CurrentProfile", "read clientUserId");
        var current = sdk.CurrentProfile;
        var clientUserId = current == null || string.IsNullOrEmpty(current.ClientUserId)
            ? profile.userId : current.ClientUserId;
        try
        {
            var member = action == "fetch-profile" ? OctopusCommunityMemberId.FromProfileId(_lastProfileId) :
                OctopusCommunityMemberId.FromClientUserId(clientUserId);
            if (action == "observe")
            {
                if (_observer != null) _observer.StopObservation();
                _observer = this;
                _observedSdk = sdk;
                Report("Observing community data by clientUserId. Run Preset 1 or act in Community to see an emission.");
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnCommunityDataChanged", "subscribe");
                sdk.CommunityDataChanged += OnData;
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.StartObservingCommunityData", "clientUserId");
                sdk.StartObservingCommunityData(member);
                return;
            }
            var generation = ++_generation;
            OctopusScenarioHostProfile destination = null;
            var navigationHandled = false;
            if (action == "host-profile")
            {
                destination = new OctopusScenarioHostProfile(clientUserId);
                navigationHandled = RequestHostProfile(destination);
            }
            ReportRunning("Fetching community data…");
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.FetchCommunityData",
                member.ProfileId == null ? "clientUserId" : "profileId");
            sdk.FetchCommunityData(member, data =>
            {
                if (destination != null) destination.Complete(data);
                if (generation != _generation) return;
                Remember(data);
                Report((destination == null ? "Fetched: " : navigationHandled ? "Host profile requested: " :
                    "Host profile data ready; navigation unavailable in this renderer. ") + Describe(data));
            }, error =>
            {
                if (destination != null) destination.Complete(null, error);
                if (generation == _generation) Report("FetchCommunityData failed: " + error);
            });
        }
        catch (Exception exception)
        {
            if (action == "observe") StopObservation();
            if (HostProfile != null && HostProfile.IsLoading) HostProfile.Complete(null, exception.Message);
            Report("Community data failed: " + exception.Message);
        }
    }

    private void Remember(OctopusCommunityData data)
    {
        if (data != null && !string.IsNullOrEmpty(data.ProfileId)) _lastProfileId = data.ProfileId;
    }
    private void OnData(OctopusCommunityData data)
    {
        Remember(data);
        var line = "flow(clientUserId): " + Describe(data);
        if (IsRunning) ReportRunning(line); else Report(line);
    }
    private static string Describe(OctopusCommunityData data)
    {
        if (data == null) return "Unknown member (null — the community may not expose client user ids).";
        return "profileId=" + data.ProfileId + "; messageCount=" + Value(data.MessageCount) +
            "; gamification.level=" + (data.Gamification == null ? "null" : data.Gamification.Level.ToString()) +
            "; gamification.score=" + (data.Gamification == null ? "null" : Value(data.Gamification.Score));
    }
    private static string Value(int? value) { return value.HasValue ? value.Value.ToString() : "null"; }

    private void CheckContract()
    {
        var profile = OctopusCommunityMemberId.FromProfileId("fixture-profile");
        var client = OctopusCommunityMemberId.FromClientUserId("fixture-client");
        var rejected = 0;
        foreach (var value in new string[] { null, "" })
        {
            try { OctopusCommunityMemberId.FromProfileId(value); } catch (ArgumentException) { ++rejected; }
            try { OctopusCommunityMemberId.FromClientUserId(value); } catch (ArgumentException) { ++rejected; }
        }
        var valid = profile.ClientUserId == null && client.ProfileId == null && rejected == 4;
        Report(valid ? "Exactly-one-id contract enforced locally: factories reject empty ids; both / neither id " +
            "cannot be constructed in C#. No SDK call made." : "Contract violation in member-id factories.");
    }

    private void StopObservation()
    {
        var sdk = _observedSdk;
        _observedSdk = null;
        if (sdk == null) return;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnCommunityDataChanged", "unsubscribe");
        sdk.CommunityDataChanged -= OnData;
        if (_observer != this) return;
        _observer = null;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.StopObservingCommunityData", "");
        sdk.StopObservingCommunityData();
    }

    public override void Dispose()
    {
        base.Dispose();
        ++_generation;
        StopObservation();
    }
}
