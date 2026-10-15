/// <summary>
/// What the sample's Play In-App Update check found. Mirrors <c>AppUpdateChecker.Status</c> in the
/// Android sample and <c>SampleUpdateStatus</c> in the Flutter one on purpose: one behaviour reached
/// through three bindings, and the states are what a QA lane reads. Keep them in step
/// (pm-tools <c>shared/design/samples/AUTO_UPDATE.md</c>).
/// </summary>
public enum OctopusSampleUpdateState
{
    /// <summary>Not an Android player (Editor, iOS). The card shows no update row at all.</summary>
    Unsupported,

    /// <summary>No check has run yet.</summary>
    Unknown,

    Checking,
    UpToDate,
    UpdateAvailable,

    /// <summary>
    /// A newer build exists but Play refuses the in-place flow. Deliberately NOT
    /// <see cref="UpToDate"/>: telling a tester their stale build is current is the one answer
    /// this feature must never give.
    /// </summary>
    UpdateNotInstallable,

    /// <summary>
    /// Play does not own this install: a sideloaded APK, an Editor build-and-run, an emulator
    /// with no Play Store. The commonest answer in the sample's lifetime, and a normal one.
    /// </summary>
    NotFromPlayStore,

    /// <summary>A genuine transient failure, retryable; carries Play's code when there is one.</summary>
    Error,
}

/// <summary>Play's availability answer before it is paired with the info the flow needs.</summary>
public enum OctopusSampleUpdateAvailability { UpToDate, Installable, NotInstallable }

/// <summary>
/// An immutable check result. Pure (no Android or UI type), so the whole decision table is
/// pinned by EditMode tests; the JNI plumbing in <see cref="OctopusSampleAppUpdate"/> only feeds it.
/// </summary>
public sealed class OctopusSampleUpdateStatus
{
    /// <summary><c>UpdateAvailability.UPDATE_AVAILABLE</c> in the Play library.</summary>
    public const int PlayUpdateAvailable = 2;

    /// <summary><c>InstallErrorCode.ERROR_INSTALL_NOT_ALLOWED</c>.</summary>
    public const int ErrorInstallNotAllowed = -6;

    /// <summary><c>InstallErrorCode.ERROR_PLAY_STORE_NOT_FOUND</c>.</summary>
    public const int ErrorPlayStoreNotFound = -9;

    /// <summary><c>InstallErrorCode.ERROR_APP_NOT_OWNED</c>.</summary>
    public const int ErrorAppNotOwned = -10;

    public static readonly OctopusSampleUpdateStatus Unsupported = new OctopusSampleUpdateStatus(OctopusSampleUpdateState.Unsupported);
    public static readonly OctopusSampleUpdateStatus Unknown = new OctopusSampleUpdateStatus(OctopusSampleUpdateState.Unknown);
    public static readonly OctopusSampleUpdateStatus Checking = new OctopusSampleUpdateStatus(OctopusSampleUpdateState.Checking);
    public static readonly OctopusSampleUpdateStatus UpToDate = new OctopusSampleUpdateStatus(OctopusSampleUpdateState.UpToDate);
    public static readonly OctopusSampleUpdateStatus NotInstallable = new OctopusSampleUpdateStatus(OctopusSampleUpdateState.UpdateNotInstallable);
    public static readonly OctopusSampleUpdateStatus NotFromPlayStore = new OctopusSampleUpdateStatus(OctopusSampleUpdateState.NotFromPlayStore);

    public OctopusSampleUpdateState State { get; private set; }

    /// <summary>Play's error code for <see cref="OctopusSampleUpdateState.Error"/>, when one was read.</summary>
    public int? ErrorCode { get; private set; }

    /// <summary>The offered build's versionCode for <see cref="OctopusSampleUpdateState.UpdateAvailable"/>.</summary>
    public int AvailableVersionCode { get; private set; }

    private OctopusSampleUpdateStatus(OctopusSampleUpdateState state, int? errorCode = null, int availableVersionCode = 0)
    {
        State = state;
        ErrorCode = errorCode;
        AvailableVersionCode = availableVersionCode;
    }

    public static OctopusSampleUpdateStatus Available(int availableVersionCode)
    {
        return new OctopusSampleUpdateStatus(OctopusSampleUpdateState.UpdateAvailable, null, availableVersionCode);
    }

    public static OctopusSampleUpdateStatus Failed(int? errorCode)
    {
        return new OctopusSampleUpdateStatus(OctopusSampleUpdateState.Error, errorCode);
    }

    /// <summary>
    /// Anything but a plain "update available" is up to date — including UNKNOWN and an update
    /// already in progress; an available update Play will not run IMMEDIATE is NOT up to date.
    /// </summary>
    public static OctopusSampleUpdateAvailability AvailabilityOf(int availability, bool immediateAllowed)
    {
        if (availability != PlayUpdateAvailable) return OctopusSampleUpdateAvailability.UpToDate;
        return immediateAllowed ? OctopusSampleUpdateAvailability.Installable : OctopusSampleUpdateAvailability.NotInstallable;
    }

    /// <summary>The status of a successful Play answer.</summary>
    public static OctopusSampleUpdateStatus Of(int availability, bool immediateAllowed, int availableVersionCode)
    {
        switch (AvailabilityOf(availability, immediateAllowed))
        {
            case OctopusSampleUpdateAvailability.Installable: return Available(availableVersionCode);
            case OctopusSampleUpdateAvailability.NotInstallable: return NotInstallable;
            default: return UpToDate;
        }
    }

    /// <summary>
    /// Maps a failed check. The three codes meaning "Play does not own this install" all read as
    /// <see cref="OctopusSampleUpdateState.NotFromPlayStore"/>; every other code stays retryable.
    /// </summary>
    public static OctopusSampleUpdateStatus StatusOf(int? errorCode)
    {
        if (errorCode == ErrorInstallNotAllowed || errorCode == ErrorAppNotOwned || errorCode == ErrorPlayStoreNotFound)
            return NotFromPlayStore;
        return Failed(errorCode);
    }

    /// <summary>The status line, worded as in the Android sample's card.</summary>
    public string Text
    {
        get
        {
            switch (State)
            {
                case OctopusSampleUpdateState.Unsupported: return string.Empty;
                case OctopusSampleUpdateState.Checking: return "Checking for updates…";
                case OctopusSampleUpdateState.UpToDate: return "App is up to date";
                case OctopusSampleUpdateState.UpdateAvailable: return "A new version is available";
                case OctopusSampleUpdateState.UpdateNotInstallable: return "A new version exists, but Play won't install it over this build";
                case OctopusSampleUpdateState.NotFromPlayStore: return "Not installed from Play Store";
                case OctopusSampleUpdateState.Error:
                    return ErrorCode.HasValue
                        ? "Update check failed (code " + ErrorCode.Value + ") — tap to retry"
                        : "Update check failed — tap to retry";
                default: return "Tap to check for updates";
            }
        }
    }

    /// <summary>The single button's label: "Update now" only when Play will install in place.</summary>
    public string ActionText
    {
        get { return State == OctopusSampleUpdateState.UpdateAvailable ? "Update now" : "Check for update"; }
    }

    /// <summary>Whether the status line uses the danger colour, as on Android.</summary>
    public bool IsAttention
    {
        get { return State == OctopusSampleUpdateState.UpdateNotInstallable || State == OctopusSampleUpdateState.Error; }
    }

    /// <summary>
    /// Whether the launch announcement should show: only for an installable update whose
    /// versionCode the tester has not already dismissed.
    /// </summary>
    public bool ShouldAnnounce(int dismissedVersionCode)
    {
        return State == OctopusSampleUpdateState.UpdateAvailable && AvailableVersionCode != dismissedVersionCode;
    }
}
