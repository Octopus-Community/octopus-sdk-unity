public partial class OctopusSDK
{
    /// <summary>
    /// The version of this package, readable at runtime.
    /// <para>
    /// <c>package.json</c> is a manifest the player never reads, and
    /// <c>UnityEditor.PackageManager</c> is editor-only, so a host that wants to state which
    /// Octopus SDK it embeds — in an About screen, a crash report, a support ticket — has no other
    /// source. It is the package's own version, not the native Android or iOS SDK's: those follow
    /// their own patch streams behind the pins, and this string does not track them.
    /// </para>
    /// <para>
    /// Hand-written, and kept honest mechanically: <c>ci/native-pins/verify-native-pins.sh</c>
    /// fails when this literal and <c>UnityPackage/package.json</c> disagree, and that script is
    /// the required <c>Native SDK pin coherence</c> check on every PR. A release bump that forgets
    /// this line cannot merge.
    /// </para>
    /// </summary>
    public const string Version = "1.13.0";
}
