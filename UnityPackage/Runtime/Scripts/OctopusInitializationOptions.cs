/// <summary>Platform-specific options read when initializing the Octopus SDK.</summary>
public sealed class OctopusInitializationOptions
{
    /// <summary>
    /// On iOS, true leaves AVAudioSession configuration and activation to your app when community
    /// videos play or pause. Defaults to false, allowing the native SDK to manage the session.
    /// When true, configure the session in your app. The .playback category keeps video audio
    /// audible with the Ring/Silent switch on; .mixWithOthers lets other apps' audio keep playing.
    /// The value is retained across community switches, including after Stop, until the next
    /// successful Initialize call.
    /// Has no effect on Android or in the Editor mock.
    /// </summary>
    public bool AppManagedAudioSession { get; set; }
}
