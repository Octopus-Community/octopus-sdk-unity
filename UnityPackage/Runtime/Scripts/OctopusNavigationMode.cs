/// <summary>Selects the iOS SDK's internal navigation container. Android ignores this setting;
/// the Octopus UI keeps its existing full-screen presentation on both platforms.</summary>
public enum OctopusNavigationMode
{
    /// <summary>Use the native <c>navigationStack</c> container (iOS 16+, legacy fallback below).
    /// Same name and semantics as the Flutter / React Native <c>navigationStack</c> value.</summary>
    NavigationStack = 0,
    /// <summary>Use the native automatic container (currently a legacy NavigationView).
    /// This does not change the outer presentation to a sheet. Same name and semantics as the
    /// Flutter / React Native <c>automatic</c> value.</summary>
    Automatic = 1
}
