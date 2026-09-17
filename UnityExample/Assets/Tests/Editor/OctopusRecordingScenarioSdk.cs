using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The <see cref="IOctopusScenarioSdk"/> an EditMode test installs through
/// <see cref="OctopusScenarioSdk.Use"/>: it records the entry point a preset reached and the
/// values it carried, and hands back a task the test decides when — and how — to complete.
///
/// Recording rather than asserting: the test says what it expects, this only remembers what
/// happened, so one recorder serves the "which call, with what" checks and the in-flight ones.
///
/// The SSO token provider is recorded as a <c>bool</c> and never invoked. It closes over the
/// sample's `authToken`, which stays out of every field, every log line and — here — every
/// recorded argument, exactly as `ConnectionScenario` documents.
/// </summary>
public sealed class OctopusRecordingScenarioSdk : IOctopusScenarioSdk
{
    /// <summary>One recorded call: the entry point's name and the arguments it received.</summary>
    public struct Call
    {
        public readonly string Method;
        public readonly object[] Args;

        public Call(string method, object[] args)
        {
            Method = method;
            Args = args ?? new object[0];
        }

        public override string ToString()
        {
            var rendered = new List<string>();
            foreach (var arg in Args) rendered.Add(arg == null ? "null" : arg.ToString());
            return Method + "(" + string.Join(", ", rendered.ToArray()) + ")";
        }
    }

    private readonly List<Call> _calls = new List<Call>();

    /// <summary>Every call recorded so far, in order.</summary>
    public IReadOnlyList<Call> Calls { get { return _calls; } }

    /// <summary>Every recorded entry-point name, including theme setup.</summary>
    public IList<string> Methods { get { return MethodNames(false); } }

    /// <summary>Explicitly excludes native theme setup when checking only scenario actions.</summary>
    public IList<string> ScenarioMethods { get { return MethodNames(true); } }

    private IList<string> MethodNames(bool excludeTheme)
    {
        var names = new List<string>();
        foreach (var call in _calls)
        {
            if (excludeTheme && IsThemeCall(call.Method)) continue;
            names.Add(call.Method);
        }
        return names;
    }

    /// <summary>The theme calls recorded so far, in order (#134).</summary>
    public IList<Call> ThemeCalls
    {
        get
        {
            var calls = new List<Call>();
            foreach (var call in _calls) if (IsThemeCall(call.Method)) calls.Add(call);
            return calls;
        }
    }

    private static bool IsThemeCall(string method)
    {
        return method == "ApplyTheme" || method == "SetColorSchemeType";
    }

    /// <summary>The profile the pilots see. Null simulates the missing config asset.</summary>
    public OctopusExampleConfig.ExampleProfile Profile { get; set; }

    /// <summary>
    /// When false (the default) the async entry points return an already-completed task, so a
    /// preset's `Run` finishes synchronously. When true they return a task this recorder keeps
    /// open in <see cref="Pending"/> — the in-flight state the serialisation slot exists for.
    /// </summary>
    public bool DeferCompletions { get; set; }

    /// <summary>The task left open by the last deferred call, or null when nothing is in flight.</summary>
    public TaskCompletionSource<bool> Pending { get; private set; }

    /// <summary>Forgets every recorded call. The profile and <see cref="DeferCompletions"/> stay.</summary>
    public void Clear()
    {
        _calls.Clear();
    }

    /// <summary>The last recorded call, or a call whose Method is null when nothing was recorded.</summary>
    public Call Last
    {
        get { return _calls.Count == 0 ? new Call(null, null) : _calls[_calls.Count - 1]; }
    }

    /// <summary>Completes the deferred call as the native bridge would on a normal return.</summary>
    public void CompletePending()
    {
        var pending = Pending;
        Pending = null;
        if (pending != null) pending.SetResult(true);
    }

    /// <summary>Faults the deferred call, so the pilot's catch/finally path runs.</summary>
    public void FailPending(string message)
    {
        var pending = Pending;
        Pending = null;
        if (pending != null) pending.SetException(new InvalidOperationException(message));
    }

    /// <inheritdoc/>
    public void Initialize(string apiKey, ConnectionMode mode)
    {
        Record("Initialize", apiKey, mode);
    }

    /// <inheritdoc/>
    public Func<Task<string>> LastTokenProvider { get; private set; }

    public Task ConnectUser(string userId, string nickname, string bio, string picture,
                            Func<Task<string>> tokenProvider)
    {
        LastTokenProvider = tokenProvider;
        Record("ConnectUser", userId, nickname, bio, picture, tokenProvider != null);
        return NextTask();
    }

    /// <inheritdoc/>
    public Task DisconnectUser()
    {
        Record("DisconnectUser");
        return NextTask();
    }

    /// <inheritdoc/>
    public void ApplyTheme(OctopusColorScheme light, OctopusColorScheme dark)
    {
        Record("ApplyTheme", light, dark);
    }

    /// <inheritdoc/>
    public void SetColorSchemeType(int colorSchemeType)
    {
        Record("SetColorSchemeType", colorSchemeType);
    }

    /// <inheritdoc/>
    public void Track(string eventName, IDictionary<string, string> properties)
    {
        Record("Track", eventName, properties);
    }

    /// <inheritdoc/>
    public void OverrideDefaultLocale(string languageCode)
    {
        Record("OverrideDefaultLocale", languageCode);
    }

    /// <inheritdoc/>
    public void SetLogo(OctopusLogo logo) { Record("SetLogo", logo); }
    public void SetFonts(OctopusFonts fonts) { Record("SetFonts", fonts); }
    public OctopusProfile CurrentProfile { get; set; }
    public event Action<OctopusProfile> ProfileChanged;
    public void EmitProfile(OctopusProfile profile)
    {
        CurrentProfile = profile;
        if (ProfileChanged != null) ProfileChanged(profile);
    }
    public Action RefreshSuccess;
    public Action<OctopusRefreshEntitlementsError> RefreshError;
    public void RefreshEntitlements(Action onSuccess, Action<OctopusRefreshEntitlementsError> onError)
    {
        Record("RefreshEntitlements");
        RefreshSuccess = onSuccess;
        RefreshError = onError;
        if (!DeferCompletions) onSuccess();
    }
    public Action<OctopusCommunityConfig> ConfigResult;
    public Action<string> ConfigError;
    public void DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode? mode)
    {
        Record("DebugOverrideTermsAcceptanceMode", mode);
    }
    public void DebugGetCommunityConfig(Action<OctopusCommunityConfig> onResult, Action<string> onError)
    {
        Record("DebugGetCommunityConfig");
        ConfigResult = onResult;
        ConfigError = onError;
        if (!DeferCompletions) onResult(null);
    }
    public void DebugOverrideProfileFieldsLock(OctopusProfileFieldsLock fieldsLock)
    {
        Record("DebugOverrideProfileFieldsLock", fieldsLock);
    }
    public OctopusCommunityData CommunityDataResult;
    public Action<OctopusCommunityData> FetchResult;
    public Action<string> FetchError;
    public event Action<OctopusCommunityData> CommunityDataChanged;
    public int CommunityDataListenerCount
    {
        get { return CommunityDataChanged == null ? 0 : CommunityDataChanged.GetInvocationList().Length; }
    }
    public void FetchCommunityData(OctopusCommunityMemberId memberId, Action<OctopusCommunityData> onResult, Action<string> onError)
    {
        Record("FetchCommunityData", memberId);
        FetchResult = onResult;
        FetchError = onError;
        if (!DeferCompletions) onResult(CommunityDataResult);
    }
    public void EmitCommunityData(OctopusCommunityData data)
    {
        if (CommunityDataChanged != null) CommunityDataChanged(data);
    }
    public void StartObservingCommunityData(OctopusCommunityMemberId memberId)
    {
        Record("StartObservingCommunityData", memberId);
        EmitCommunityData(CommunityDataResult);
    }
    public void StopObservingCommunityData() { Record("StopObservingCommunityData"); }
    public void Open()
    {
        Record("Open");
    }

    /// <inheritdoc/>
    public void OpenGroup(string groupId)
    {
        Record("OpenGroup", groupId);
    }

    /// <inheritdoc/>
    public void OpenProfile(string clientUserId)
    { Record("OpenProfile", new object[] { clientUserId }); }

    /// <inheritdoc/>
    public void OpenPost(string postId)
    {
        Record("OpenPost", postId);
    }

    /// <inheritdoc/>
    public void OpenCreatePost(OctopusPrefilledPost prefilled)
    {
        // The prefill's own fields rather than the object: a test asserting "the editor opens on
        // this text" should read the text, and OctopusPrefilledPost has no value equality.
        LastPrefilledPost = prefilled;
        Record("OpenCreatePost", prefilled == null ? null : prefilled.Text);
    }

    public event Action<IList<OctopusGroup>> GroupsChanged;
    public IList<OctopusGroup> Groups = new List<OctopusGroup>();
    public bool DeferGroups;
    public Action<IList<OctopusGroup>> PendingGroups;
    public string GroupFetchError;
    public OctopusGroupFollowUnfollowError GroupFollowError;
    public void EmitGroups(IList<OctopusGroup> groups) { GroupsChanged?.Invoke(groups); }
    public void FetchGroups(Action<IList<OctopusGroup>> completed, Action<string> error)
    {
        Record("FetchGroups");
        if (DeferGroups) PendingGroups = completed;
        else if (GroupFetchError != null) error(GroupFetchError); else completed(Groups);
    }
    public void FollowGroup(string groupId, Action completed, Action<OctopusGroupFollowUnfollowError> error)
    {
        Record("FollowGroup", groupId);
        if (GroupFollowError != null) error(GroupFollowError); else completed();
    }
    public void UnfollowGroup(string groupId, Action completed, Action<OctopusGroupFollowUnfollowError> error)
    {
        Record("UnfollowGroup", groupId);
        if (GroupFollowError != null) error(GroupFollowError); else completed();
    }

    public IList<OctopusSyncFollowGroupResult> SyncResults = new List<OctopusSyncFollowGroupResult>();
    public string SyncError;
    public void SyncFollowGroups(IList<OctopusSyncFollowGroupAction> actions,
        Action<IList<OctopusSyncFollowGroupResult>> completed, Action<string> error)
    {
        Record("SyncFollowGroups", new List<OctopusSyncFollowGroupAction>(actions));
        if (SyncError != null) error(SyncError); else completed(SyncResults);
    }

    private event Action<string> _groupAccessDenied;
    public event Action<string> GroupAccessDenied
    {
        add { Record("GroupAccessDenied.add"); _groupAccessDenied += value; }
        remove { Record("GroupAccessDenied.remove"); _groupAccessDenied -= value; }
    }
    public void EmitGroupAccessDenied(string id) { _groupAccessDenied?.Invoke(id); }

    public bool ThrowContentOverride;
    public void DebugOverrideContentOptions(OctopusContentOptions options)
    {
        Record("DebugOverrideContentOptions", options);
        if (ThrowContentOverride) throw new InvalidOperationException("override unavailable");
    }

    public OctopusSetReactionError ReactionError;
    public void SetReaction(string postId, OctopusReactionKind? kind, Action completed, Action<OctopusSetReactionError> error)
    {
        Record("SetReaction", postId, kind);
        if (ReactionError != null) error(ReactionError); else completed();
    }

    public event Action<string, OctopusPost> ClientObjectRelatedPostChanged;
    public event Action<string> NavigateToClientObject;
    public string BridgePostId = "bridge-post-1";
    public OctopusClientPostError BridgeError;
    public OctopusClientObject LastClientObject;
    public void FetchOrCreateClientObjectRelatedPost(OctopusClientObject clientObject, Action<string> completed, Action<OctopusClientPostError> error)
    {
        LastClientObject = clientObject;
        Record("FetchOrCreateClientObjectRelatedPost", clientObject.ObjectId, clientObject.Text,
            clientObject.GroupId, clientObject.ImageUrl, clientObject.SignBridgeShare != null);
        if (BridgeError != null) error(BridgeError); else completed(BridgePostId);
    }
    public void StartObservingClientObjectRelatedPost(string objectId)
    { Record("StartObservingClientObjectRelatedPost", objectId); }
    public void StopObservingClientObjectRelatedPost(string objectId)
    { Record("StopObservingClientObjectRelatedPost", objectId); }
    public void EmitClientPost(string objectId, OctopusPost post) { ClientObjectRelatedPostChanged?.Invoke(objectId, post); }
    public void EmitNavigate(string objectId) { NavigateToClientObject?.Invoke(objectId); }
    public Task<string> SignBridgeShare(string fingerprint)
    { Record("SignBridgeShare"); return Task.FromResult<string>(null); }

    public bool ThrowShareImage;
    public OctopusPrefilledPost LastPrefilledPost;
    public string PrepareBundledShareImage()
    {
        Record("PrepareBundledShareImage");
        if (ThrowShareImage) throw new InvalidOperationException("Bundled image unavailable");
        return "/sample-cache/scenario-share.png";
    }

    public event Action<OctopusEvent> OnOctopusEvent;
    public void EmitEvent(OctopusEvent value) { if (OnOctopusEvent != null) OnOctopusEvent(value); }
    public void TrackAccessToCommunity(bool hasAccess) { Record("TrackAccessToCommunity", hasAccess); }
    public bool HasAccessToCommunity { get; set; }
    public event Action<bool> OnHasAccessToCommunityChanged;
    public string AccessError;
    public void EmitCommunityAccess(bool access) { EmitAccess(access); }
    public void EmitAccess(bool value)
    {
        HasAccessToCommunity = value;
        if (OnHasAccessToCommunityChanged != null) OnHasAccessToCommunityChanged(value);
    }
    public Action PendingCompleted;
    public Action<string> PendingError;
    public string CallbackError;
    public bool ThrowOnCallbackCall;
    public void OverrideCommunityAccess(bool hasAccess, Action onCompleted, Action<string> onError)
    {
        Record("OverrideCommunityAccess", hasAccess);
        if (AccessError != null) { onError(AccessError); return; }
        CompleteCallback(onCompleted, onError);
    }
    private void CompleteCallback(Action completed, Action<string> error)
    {
        if (ThrowOnCallbackCall) throw new InvalidOperationException("bridge unavailable");
        if (DeferCompletions) { PendingCompleted = completed; PendingError = error; }
        else if (CallbackError != null) error(CallbackError);
        else completed();
    }
    public OctopusExampleConfig.ExampleProfile AlternateProfile { get; set; }
    public void SwitchCommunity(string apiKey, ConnectionMode mode, Action onCompleted, Action<string> onError)
    {
        Record("SwitchCommunity", apiKey, mode);
        CompleteCallback(onCompleted, onError);
    }
    public void Reset(Action onCompleted, Action<string> onError) { Record("Reset"); CompleteCallback(onCompleted, onError); }
    public void Stop(Action onCompleted, Action<string> onError) { Record("Stop"); CompleteCallback(onCompleted, onError); }
    public event Action<int> OnNotSeenNotificationsCount;
    public void EmitCount(int count) { if (OnNotSeenNotificationsCount != null) OnNotSeenNotificationsCount(count); }
    public bool ThrowOnRefresh;
    public void UpdateNotSeenNotificationsCount()
    {
        Record("UpdateNotSeenNotificationsCount");
        if (ThrowOnRefresh) throw new InvalidOperationException("refresh unavailable");
    }
    public bool RecognizesNotification = true;
    public bool NullNotification;
    public bool ThrowOnNotificationOpen;
    public bool IsOctopusNotification(IDictionary<string, string> payload)
    { Record("IsOctopusNotification", new Dictionary<string, string>(payload)); return RecognizesNotification; }
    public OctopusNotification GetOctopusNotification(IDictionary<string, string> payload)
    { Record("GetOctopusNotification", new Dictionary<string, string>(payload)); return NullNotification ? null : new OctopusNotification(payload); }
    public void Open(OctopusNotification notification)
    {
        Record("OpenNotification", notification);
        if (ThrowOnNotificationOpen) throw new InvalidOperationException("notification unavailable");
    }
    public Action<string> BeforeCall;
    private void Record(string method, params object[] args)
    {
        if (BeforeCall != null) BeforeCall(method);
        _calls.Add(new Call(method, args));
    }

    private Task NextTask()
    {
        if (!DeferCompletions) return Task.FromResult(true);
        Pending = new TaskCompletionSource<bool>();
        return Pending.Task;
    }
}
