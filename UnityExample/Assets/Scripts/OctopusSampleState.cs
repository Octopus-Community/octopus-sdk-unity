using System;

/// <summary>
/// What the sample last did to the SDK, and the only thing the Home dashboard can honestly show
/// about a session on this platform.
///
/// Android's Home reads four SDK flows — `isInitialisedFlow`, `connectionState`,
/// `notSeenNotificationsCount`, `hasAccessToCommunity`. This package publishes the last two only
/// (see <c>OctopusSDK.CommunityAccess.cs</c> and <c>OctopusSDK.NotSeenNotificationsCount.cs</c>);
/// there is no initialisation flow and no connection state to read back, a gap
/// <see cref="ConnectionScenario"/> already states on its own screen. So initialisation, session
/// and locale are recorded here, by the code that made the call, and the dashboard says that is
/// what it is showing.
///
/// Process-wide, like the SDK it describes: `OctopusSDK.Initialize` configures a process-wide
/// singleton, so "have we initialised yet" cannot be per-screen state.
/// </summary>
public static class OctopusSampleState
{
    /// <summary>What the sample knows about the session, which is never more than its own last call.</summary>
    public enum Session
    {
        /// <summary>No connection call has been made in this process.</summary>
        None,

        /// <summary>A `ConnectUser` call returned. That the call returned, not that a session exists.</summary>
        ConnectCompleted,

        /// <summary>A `DisconnectUser` call returned.</summary>
        Disconnected,

        /// <summary>A connection call threw, or was refused before reaching the SDK.</summary>
        Failed
    }

    private static bool _initialized;
    private static string _modeLabel;
    private static Session _session = Session.None;
    private static string _sessionDetail;
    private static string _localeOverride;
    private static bool _observing;
    private static bool _hasCommunityAccess;
    private static int _unseenNotifications = -1;

    /// <summary>
    /// Raised whenever any value here changes. The Home dashboard and the Scenarios list subscribe;
    /// it is an event rather than a poll because a scenario screen can change the state while
    /// neither is even built — and, for the Scenarios list, while it is built and behind the
    /// scenario that changed it.
    /// </summary>
    public static event Action Changed;

    /// <summary>True once <c>OctopusSDK.Initialize</c> has been called in this process.</summary>
    public static bool IsInitialized { get { return _initialized; } }

    /// <summary>The connection mode the SDK was initialised with, or null before that.</summary>
    public static string ModeLabel { get { return _modeLabel; } }

    /// <summary>The last connection call's outcome.</summary>
    public static Session ConnectionSession { get { return _session; } }

    /// <summary>A sentence describing <see cref="ConnectionSession"/>, or null.</summary>
    public static string SessionDetail { get { return _sessionDetail; } }

    /// <summary>The language code last passed to <c>OverrideDefaultLocale</c>, or null.</summary>
    public static string LocaleOverride { get { return _localeOverride; } }

    /// <summary>
    /// The SDK's last published community-access value. Mirrored here rather than read per-screen
    /// because <c>OnHasAccessToCommunityChanged</c> fires once, when it changes.
    /// </summary>
    public static bool HasCommunityAccess { get { return _hasCommunityAccess; } }

    /// <summary>
    /// The SDK's last published unseen-notification count, or -1 if it has never published one.
    ///
    /// -1 is not zero and the screens must not render it as zero: the SDK pushes this value, the
    /// sample never asks for it (<c>UpdateNotSeenNotificationsCount()</c> is a native call and Home
    /// is read-only), so "not yet told" is a state the reader has to be able to see.
    /// </summary>
    public static int UnseenNotifications { get { return _unseenNotifications; } }

    /// <summary>
    /// Starts mirroring the two values the SDK publishes, once per process, and seeds community
    /// access from <c>OctopusSDK.HasAccessToCommunity</c>'s cache.
    ///
    /// Lives here rather than in the dashboard that displays it because the dashboard is disposable:
    /// the shell destroys and rebuilds its content on every tab switch and every theme change, and a
    /// count held by the view would be forgotten on each one — the SDK publishes a count when it
    /// changes, so a rebuilt view would then read "not yet told" for the rest of the session while
    /// the SDK considers the reader informed. Call it before anything initialises the SDK, so no
    /// event fires into a process that is not listening yet.
    ///
    /// Never unsubscribed, deliberately: the subscriber is a static class that lives as long as the
    /// events do, so there is nothing to leak and nothing to outlive.
    /// </summary>
    public static void EnsureObserving()
    {
        if (_observing) return;
        _observing = true;
        _hasCommunityAccess = OctopusSDK.HasAccessToCommunity;
        OctopusSDK.OnHasAccessToCommunityChanged += ReportCommunityAccess;
        OctopusSDK.OnNotSeenNotificationsCount += ReportUnseenNotifications;
    }

    /// <summary>
    /// Records a community-access value published by the SDK. Public for the EditMode tests, which
    /// have no way to make the real SDK publish one.
    /// </summary>
    public static void ReportCommunityAccess(bool hasAccess)
    {
        if (_hasCommunityAccess == hasAccess) return;
        _hasCommunityAccess = hasAccess;
        Raise();
    }

    /// <summary>
    /// Records an unseen-notification count published by the SDK. Public for the same reason as
    /// <see cref="ReportCommunityAccess"/>.
    /// </summary>
    public static void ReportUnseenNotifications(int count)
    {
        if (_unseenNotifications == count) return;
        _unseenNotifications = count;
        Raise();
    }

    /// <summary>Records the initialisation, once. A second call with the same mode changes nothing.</summary>
    public static void ReportInitialized(string modeLabel)
    {
        if (_initialized && _modeLabel == modeLabel) return;
        _initialized = true;
        _modeLabel = modeLabel;
        Raise();
    }

    /// <summary>Records a connection call's outcome and the sentence to show beside it.</summary>
    public static void ReportSession(Session session, string detail)
    {
        if (_session == session && _sessionDetail == detail) return;
        _session = session;
        _sessionDetail = detail;
        Raise();
    }

    /// <summary>Records a locale override.</summary>
    public static void ReportLocaleOverride(string languageCode)
    {
        if (_localeOverride == languageCode) return;
        _localeOverride = languageCode;
        Raise();
    }

    /// <summary>
    /// Forgets everything. Written for EditMode tests and for
    /// <see cref="OctopusScenarioSdk.Use"/>, which swaps the SDK the pilots call: every value here
    /// describes an SDK, so keeping one across a swap would describe an SDK nobody is calling.
    /// The app never calls it.
    ///
    /// The two mirrored SDK values go back to "nothing published yet" with the rest, and stay
    /// subscribed: after a <see cref="OctopusScenarioSdk.Use"/> the counts describe an SDK nobody is
    /// calling any more, and the real SDK republishes on its own when it has something to say.
    /// </summary>
    public static void Reset()
    {
        _initialized = false;
        _modeLabel = null;
        _session = Session.None;
        _sessionDetail = null;
        _localeOverride = null;
        _hasCommunityAccess = _observing && OctopusSDK.HasAccessToCommunity;
        _unseenNotifications = -1;
        Raise();
    }

    /// <summary>
    /// Forgets what the SDK last *reported* — the connection call and the two mirrored values — and
    /// deliberately keeps what it was last *configured* with: the initialisation flag, its mode, and
    /// any locale override.
    ///
    /// The split is the whole point, and <see cref="Reset"/> is not a substitute. `IsInitialized` is
    /// not a display value: <see cref="OctopusScenarioSdk.EnsureInitialized"/> reads it as the guard
    /// that stops a second `Initialize` on an SDK that already has one, and the native side answers
    /// a second call with another channel and another set of collectors. Clearing it from a UI
    /// control would mean "open Community, reset, open Community" quietly initialising twice. A
    /// locale override is the same kind of fact — the SDK is still in that locale, and forgetting it
    /// would only make the Home dashboard disagree with the app the reader is looking at.
    ///
    /// What it does clear is honest to clear: a past call is past, and the two published values
    /// arrive again on their own the moment the SDK has something to say.
    ///
    /// This is what the Settings tab's reset calls. <see cref="Reset"/> stays for
    /// <see cref="OctopusScenarioSdk.Use"/> and for tests, where the SDK underneath really has been
    /// swapped and every value here describes something nobody is calling any more.
    /// </summary>
    public static void ResetObservations()
    {
        _session = Session.None;
        _sessionDetail = null;
        _hasCommunityAccess = _observing && OctopusSDK.HasAccessToCommunity;
        _unseenNotifications = -1;
        Raise();
    }

    private static void Raise()
    {
        var handler = Changed;
        if (handler != null) handler();
    }
}
