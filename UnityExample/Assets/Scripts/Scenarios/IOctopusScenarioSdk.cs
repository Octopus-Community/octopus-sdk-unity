using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Everything a scenario preset's <see cref="OctopusScenarioPreset.Run"/> touches outside the
/// sample's own state: the `OctopusSDK` entry points and streams the pilots use, plus the
/// gitignored config asset those calls read their values from.
///
/// The seam exists so the `Run` half of a preset is testable at all. `OctopusSDK.Initialize`
/// creates a live `OctopusChannel` GameObject and `OctopusExampleConfig.Instance` loads an asset
/// that is git-ignored (absent on CI and on a fresh clone), so a test driving a preset against
/// the real statics would either touch the native bridge or stop at "config asset missing". With
/// this interface installed through <see cref="OctopusScenarioSdk.Use"/>, an EditMode test drives
/// the same code path the button does and records exactly which entry point it reached.
///
/// It is deliberately not a general-purpose SDK wrapper: it carries the calls the pilots make and
/// nothing else. The eight legacy `*Example` scenes still call `OctopusSDK` directly (issue #100).
/// </summary>
public interface IOctopusScenarioSdk
{
    event Action<IList<OctopusGroup>> GroupsChanged;
    void FetchGroups(Action<IList<OctopusGroup>> completed, Action<string> error);
    void FollowGroup(string groupId, Action completed, Action<OctopusGroupFollowUnfollowError> error);
    void UnfollowGroup(string groupId, Action completed, Action<OctopusGroupFollowUnfollowError> error);

    void SyncFollowGroups(IList<OctopusSyncFollowGroupAction> actions,
        Action<IList<OctopusSyncFollowGroupResult>> completed, Action<string> error);

    event Action<string> GroupAccessDenied;

    void DebugOverrideContentOptions(OctopusContentOptions options);

    void SetReaction(string postId, OctopusReactionKind? kind, Action completed, Action<OctopusSetReactionError> error);

    event Action<string, OctopusPost> ClientObjectRelatedPostChanged;
    event Action<string> NavigateToClientObject;
    void FetchOrCreateClientObjectRelatedPost(OctopusClientObject clientObject, Action<string> completed, Action<OctopusClientPostError> error);
    void StartObservingClientObjectRelatedPost(string objectId);
    void StopObservingClientObjectRelatedPost(string objectId);
    Task<string> SignBridgeShare(string fingerprint);

    string PrepareBundledShareImage();

    /// <summary>
    /// The sample's configured profile, or null when the gitignored `OctopusExampleConfig` asset
    /// is missing — the pilots report that instead of calling anything.
    ///
    /// *Which* profile depends on the `Force login` switch in the Scenarios Sign-in header: off
    /// gives <see cref="OctopusExampleConfig.Default"/>, on gives
    /// <see cref="OctopusExampleConfig.ForcedLogin"/>. Two profiles means two API keys, i.e. two
    /// demo communities configured differently on the backend, which is the only place
    /// `forceLoginOnStrongActions` lives — see <see cref="OctopusSampleFeatureToggles"/>.
    /// </summary>
    OctopusExampleConfig.ExampleProfile Profile { get; }

    event Action<OctopusEvent> OnOctopusEvent;
    void TrackAccessToCommunity(bool hasAccess);
    bool HasAccessToCommunity { get; }
    event Action<bool> OnHasAccessToCommunityChanged;
    void OverrideCommunityAccess(bool hasAccess, Action onCompleted, Action<string> onError);
    OctopusExampleConfig.ExampleProfile AlternateProfile { get; }
    void SwitchCommunity(string apiKey, ConnectionMode mode, Action onCompleted, Action<string> onError);
    void Reset(Action onCompleted, Action<string> onError);
    void Stop(Action onCompleted, Action<string> onError);
    event Action<int> OnNotSeenNotificationsCount;
    void UpdateNotSeenNotificationsCount();
    bool IsOctopusNotification(IDictionary<string, string> payload);
    OctopusNotification GetOctopusNotification(IDictionary<string, string> payload);
    void Open(OctopusNotification notification);
    /// <summary>`OctopusSDK.Initialize(apiKey, mode)`.</summary>
    void Initialize(string apiKey, ConnectionMode mode);

    /// <summary>`OctopusSDK.ConnectUser(...)`. The task completes when the call returns.</summary>
    Task ConnectUser(string userId, string nickname, string bio, string picture,
                     Func<Task<string>> tokenProvider);

    /// <summary>`OctopusSDK.DisconnectUser()`.</summary>
    Task DisconnectUser();

    /// <summary>
    /// `OctopusSDK.SetLightColorScheme` + `SetDarkColorScheme` — the sample's brand palette,
    /// sent once after `Initialize` (issue #134).
    /// </summary>
    void ApplyTheme(OctopusColorScheme light, OctopusColorScheme dark);

    /// <summary>`OctopusSDK.SetColorSchemeType(type)`: 1 = light, 2 = dark.</summary>
    void SetColorSchemeType(int colorSchemeType);

    /// <summary>`OctopusSDK.Track(name, properties)`.</summary>
    void Track(string eventName, IDictionary<string, string> properties);

    /// <summary>`OctopusSDK.OverrideDefaultLocale(languageCode)`.</summary>
    void OverrideDefaultLocale(string languageCode);

    /// <summary>Sets a native resource logo; null restores the default.</summary>
    void SetLogo(OctopusLogo logo);
    /// <summary>Sets native font overrides; null restores defaults.</summary>
    void SetFonts(OctopusFonts fonts);
    /// <summary>Latest native profile snapshot; null when no profile is available.</summary>
    OctopusProfile CurrentProfile { get; }
    /// <summary>Native profile updates on the Unity main thread; unsubscribe when the visit ends.</summary>
    event Action<OctopusProfile> ProfileChanged;
    /// <summary>Refresh completion is independent of ProfileChanged delivery.</summary>
    void RefreshEntitlements(Action onSuccess, Action<OctopusRefreshEntitlementsError> onError);
    /// <summary>Debug override; null restores backend consent configuration.</summary>
    void DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode? mode);
    /// <summary>Reads effective configuration asynchronously; a null result means unavailable.</summary>
    void DebugGetCommunityConfig(Action<OctopusCommunityConfig> onResult, Action<string> onError);
    /// <summary>Debug override; null restores backend field locks.</summary>
    void DebugOverrideProfileFieldsLock(OctopusProfileFieldsLock fieldsLock);
    /// <summary>Fetches a public member snapshot; a null result means unknown or unavailable.</summary>
    void FetchCommunityData(OctopusCommunityMemberId memberId, Action<OctopusCommunityData> onResult, Action<string> onError);
    /// <summary>Updates from the single native community-data observation.</summary>
    event Action<OctopusCommunityData> CommunityDataChanged;
    /// <summary>Replaces the native observation. Subscribe to CommunityDataChanged first.</summary>
    void StartObservingCommunityData(OctopusCommunityMemberId memberId);
    /// <summary>Stops the native observation; subscribers must also unsubscribe.</summary>
    void StopObservingCommunityData();

    /// <summary>
    /// `OctopusSDK.Open()` — the community's own UI, on its main feed.
    /// </summary>
    void Open();

    /// <summary>`OctopusSDK.OpenGroup(groupId)` — the community's UI, on one group's feed.</summary>
    void OpenGroup(string groupId);

    /// <summary>`OctopusSDK.OpenPost(postId)` — the community's UI, on one post's detail.</summary>
    void OpenPost(string postId);

    /// <summary>Opens a member profile by client user id, or the own profile with null.</summary>
    void OpenProfile(string clientUserId);

    /// <summary>
    /// `OctopusSDK.OpenCreatePost(prefilled)` — the community's post editor, blank when
    /// <paramref name="prefilled"/> is null.
    /// </summary>
    void OpenCreatePost(OctopusPrefilledPost prefilled);
}

/// <summary>
/// The implementation the app runs on: every member forwards to the matching `OctopusSDK` static,
/// and <see cref="Profile"/> reads the gitignored config asset. Nothing else lives here — a
/// behaviour worth testing belongs in the pilot, on this side of the seam.
/// </summary>
public sealed class OctopusLiveScenarioSdk : IOctopusScenarioSdk
{
    public event Action<IList<OctopusGroup>> GroupsChanged
    {
        add { OctopusSDK.OnGroupsChanged += value; }
        remove { OctopusSDK.OnGroupsChanged -= value; }
    }
    public void FetchGroups(Action<IList<OctopusGroup>> completed, Action<string> error)
    { OctopusSDK.FetchGroups(completed, error); }
    public void FollowGroup(string groupId, Action completed, Action<OctopusGroupFollowUnfollowError> error)
    { OctopusSDK.FollowGroup(groupId, completed, error); }
    public void UnfollowGroup(string groupId, Action completed, Action<OctopusGroupFollowUnfollowError> error)
    { OctopusSDK.UnfollowGroup(groupId, completed, error); }

    public void SyncFollowGroups(IList<OctopusSyncFollowGroupAction> actions,
        Action<IList<OctopusSyncFollowGroupResult>> completed, Action<string> error)
    { OctopusSDK.SyncFollowGroups(actions, completed, error); }

    public event Action<string> GroupAccessDenied
    {
        add { OctopusSDK.OnGroupAccessDenied += value; }
        remove { OctopusSDK.OnGroupAccessDenied -= value; }
    }

    public void DebugOverrideContentOptions(OctopusContentOptions options)
    { OctopusSDK.DebugOverrideContentOptions(options); }

    public void SetReaction(string postId, OctopusReactionKind? kind, Action completed, Action<OctopusSetReactionError> error)
    { OctopusSDK.SetReaction(postId, kind, completed, error); }

    public event Action<string, OctopusPost> ClientObjectRelatedPostChanged
    {
        add { OctopusSDK.OnClientObjectRelatedPostChanged += value; }
        remove { OctopusSDK.OnClientObjectRelatedPostChanged -= value; }
    }
    public event Action<string> NavigateToClientObject
    {
        add { OctopusSDK.OnNavigateToClientObject += value; }
        remove { OctopusSDK.OnNavigateToClientObject -= value; }
    }
    public void FetchOrCreateClientObjectRelatedPost(OctopusClientObject clientObject, Action<string> completed, Action<OctopusClientPostError> error)
    { OctopusSDK.FetchOrCreateClientObjectRelatedPost(clientObject, completed, error); }
    public void StartObservingClientObjectRelatedPost(string objectId)
    { OctopusSDK.StartObservingClientObjectRelatedPost(objectId); }
    public void StopObservingClientObjectRelatedPost(string objectId)
    { OctopusSDK.StopObservingClientObjectRelatedPost(objectId); }
    public Task<string> SignBridgeShare(string fingerprint)
    {
        return OctopusScenarioSdk.BridgeShareSigner(fingerprint);
    }

    public string PrepareBundledShareImage()
    {
        var image = UnityEngine.Resources.Load<UnityEngine.TextAsset>("ScenarioShareImage");
        if (image == null) throw new InvalidOperationException("Bundled ScenarioShareImage is missing.");
        var path = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "octopus-scenario-share.png");
        System.IO.File.WriteAllBytes(path, image.bytes);
        return path;
    }

    /// <inheritdoc/>
    public OctopusExampleConfig.ExampleProfile Profile
    {
        get
        {
            // LoadedOrNull, not Instance: every caller on this seam reports the absence on screen,
            // so Instance's Debug.LogError would only add a red console line to a state the user is
            // already being told about — and would fail every EditMode run, where the gitignored
            // asset is absent by construction.
            var config = OctopusExampleConfig.LoadedOrNull;
            if (config == null) return null;
            return OctopusSampleFeatureToggles.ForceLogin ? config.ForcedLogin : config.Default;
        }
    }

    public event Action<OctopusEvent> OnOctopusEvent
    {
        add { OctopusSDK.OnOctopusEvent += value; }
        remove { OctopusSDK.OnOctopusEvent -= value; }
    }
    public void TrackAccessToCommunity(bool hasAccess) { OctopusSDK.TrackAccessToCommunity(hasAccess); }
    public bool HasAccessToCommunity { get { return OctopusSDK.HasAccessToCommunity; } }
    public event Action<bool> OnHasAccessToCommunityChanged
    {
        add { OctopusSDK.OnHasAccessToCommunityChanged += value; }
        remove { OctopusSDK.OnHasAccessToCommunityChanged -= value; }
    }
    public void OverrideCommunityAccess(bool hasAccess, Action onCompleted, Action<string> onError)
    { OctopusSDK.OverrideCommunityAccess(hasAccess, onCompleted, onError); }
    public OctopusExampleConfig.ExampleProfile AlternateProfile
    {
        get
        {
            var config = OctopusExampleConfig.LoadedOrNull;
            return config == null ? null : (OctopusSampleFeatureToggles.ForceLogin ? config.Default : config.ForcedLogin);
        }
    }
    public void SwitchCommunity(string apiKey, ConnectionMode mode, Action onCompleted, Action<string> onError)
    { OctopusSDK.SwitchCommunity(apiKey, mode, onCompleted, onError); }
    public void Reset(Action onCompleted, Action<string> onError) { OctopusSDK.Reset(onCompleted, onError); }
    public void Stop(Action onCompleted, Action<string> onError) { OctopusSDK.Stop(onCompleted, onError); }
    public event Action<int> OnNotSeenNotificationsCount
    {
        add { OctopusSDK.OnNotSeenNotificationsCount += value; }
        remove { OctopusSDK.OnNotSeenNotificationsCount -= value; }
    }
    public void UpdateNotSeenNotificationsCount() { OctopusSDK.UpdateNotSeenNotificationsCount(); }
    public bool IsOctopusNotification(IDictionary<string, string> payload) { return OctopusSDK.IsOctopusNotification(payload); }
    public OctopusNotification GetOctopusNotification(IDictionary<string, string> payload) { return OctopusSDK.GetOctopusNotification(payload); }
    public void Open(OctopusNotification notification) { OctopusSDK.Open(notification); }
    /// <inheritdoc/>
    public void Initialize(string apiKey, ConnectionMode mode)
    {
        OctopusSDK.Initialize(apiKey, mode);
    }

    /// <inheritdoc/>
    public Task ConnectUser(string userId, string nickname, string bio, string picture,
                            Func<Task<string>> tokenProvider)
    {
        return OctopusSDK.ConnectUser(userId, nickname, bio, picture, tokenProvider);
    }

    /// <inheritdoc/>
    public Task DisconnectUser()
    {
        return OctopusSDK.DisconnectUser();
    }

    /// <inheritdoc/>
    public void ApplyTheme(OctopusColorScheme light, OctopusColorScheme dark)
    {
        OctopusSDK.SetLightColorScheme(light);
        OctopusSDK.SetDarkColorScheme(dark);
    }

    /// <inheritdoc/>
    public void SetColorSchemeType(int colorSchemeType)
    {
        OctopusSDK.SetColorSchemeType(colorSchemeType);
    }

    /// <inheritdoc/>
    public void Track(string eventName, IDictionary<string, string> properties)
    {
        OctopusSDK.Track(eventName, properties);
    }

    /// <inheritdoc/>
    public void OverrideDefaultLocale(string languageCode)
    {
        OctopusSDK.OverrideDefaultLocale(languageCode);
    }

    public void SetLogo(OctopusLogo logo) { OctopusSDK.SetLogo(logo); }
    public void SetFonts(OctopusFonts fonts) { OctopusSDK.SetFonts(fonts); }
    public OctopusProfile CurrentProfile { get { return OctopusSDK.CurrentProfile; } }
    public event Action<OctopusProfile> ProfileChanged
    {
        add { OctopusSDK.OnProfileChanged += value; }
        remove { OctopusSDK.OnProfileChanged -= value; }
    }
    public void RefreshEntitlements(Action onSuccess, Action<OctopusRefreshEntitlementsError> onError)
    {
        OctopusSDK.RefreshEntitlements(onSuccess, onError);
    }
    public void DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode? mode)
    {
        OctopusSDK.DebugOverrideTermsAcceptanceMode(mode);
    }
    public void DebugGetCommunityConfig(Action<OctopusCommunityConfig> onResult, Action<string> onError)
    {
        OctopusSDK.DebugGetCommunityConfig(onResult, onError);
    }
    public void DebugOverrideProfileFieldsLock(OctopusProfileFieldsLock fieldsLock)
    {
        OctopusSDK.DebugOverrideProfileFieldsLock(fieldsLock);
    }
    public void FetchCommunityData(OctopusCommunityMemberId memberId, Action<OctopusCommunityData> onResult, Action<string> onError)
    {
        OctopusSDK.FetchCommunityData(memberId, onResult, onError);
    }
    public event Action<OctopusCommunityData> CommunityDataChanged
    {
        add { OctopusSDK.OnCommunityDataChanged += value; }
        remove { OctopusSDK.OnCommunityDataChanged -= value; }
    }
    public void StartObservingCommunityData(OctopusCommunityMemberId memberId)
    {
        OctopusSDK.StartObservingCommunityData(memberId);
    }
    public void StopObservingCommunityData() { OctopusSDK.StopObservingCommunityData(); }

    /// <inheritdoc/>
    public void Open()
    {
        OctopusSDK.Open();
    }

    /// <inheritdoc/>
    public void OpenGroup(string groupId)
    {
        OctopusSDK.OpenGroup(groupId);
    }

    /// <inheritdoc/>
    public void OpenProfile(string clientUserId)
    { OctopusSDK.OpenProfile(clientUserId); }

    /// <inheritdoc/>
    public void OpenPost(string postId)
    {
        OctopusSDK.OpenPost(postId);
    }

    /// <inheritdoc/>
    public void OpenCreatePost(OctopusPrefilledPost prefilled)
    {
        OctopusSDK.OpenCreatePost(prefilled);
    }
}
