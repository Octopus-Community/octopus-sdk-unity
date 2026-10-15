# Migrating

This guide consolidates the migration notes for releases of the Octopus SDK
for Unity that contain breaking changes. Each section lists the breaking
changes first, then how to adapt. The most recent version is at the top.
Full release notes live in [CHANGELOG.md](CHANGELOG.md).

- [To 1.15.0 (from 1.14.x) — SSO-only authentication](#to-1150-from-114x--sso-only-authentication)
- [To 1.12.8 (from 1.12.7)](#to-1128-from-1127)
- [To 1.12.0 (from 1.10.x)](#to-1120-from-110x)

---

## To 1.15.0 (from 1.14.x) — SSO-only authentication

Your app now owns sign-in, aligned with the native SDKs' removal of Magic Link authentication.
`ConnectionMode.OctopusAuth()` and `OctopusSDK.SwitchCommunityOctopusAuth` are removed,
without compatibility shims. The mode parameter of `Initialize` and `SwitchCommunity` was
already required in Unity and remains so.

**Before:**

```csharp
OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.OctopusAuth());
OctopusSDK.SwitchCommunityOctopusAuth("YOUR_OTHER_API_KEY", onCompleted, onError);
```

**After:**

```csharp
OctopusSDK.OnLoginRequired += OpenAppSignIn;
OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO());
// Once your own sign-in succeeds, supply your backend's fresh SSO JWT on demand:
await OctopusSDK.ConnectUser(userId, nickname, bio, picture, FetchTokenFromBackend);
// After switching, connect the host user again in onCompleted:
OctopusSDK.SwitchCommunity("YOUR_OTHER_API_KEY", ConnectionMode.SSO(), onCompleted, onError);
```

`OpenAppSignIn` opens your app's sign-in UI. `FetchTokenFromBackend` is your
`Func<Task<string>>` that obtains a fresh client-signed token from your backend; it must work
without the Unity player loop. Call `DisconnectUser` when your app signs the user out.
Use `ConnectionMode.SSO(ProfileField.NICKNAME, ...)` if your app manages profile fields.

`RefreshEntitlements` returns `NoClientTokenProvider` until `ConnectUser` registers a provider
in this SDK session, including when the native SDK restored a profile after relaunch. Register
the provider again after initialization, a community switch, or Stop followed by initialization.
A native stored session is not logged out at launch by this change.

`OctopusScreen.SettingsList`, `SettingsAccount`, `DeleteAccount`, and the settings account,
logout and delete-warning `OctopusIconSlot` values remain available with warning-level
`Obsolete` attributes. Their numeric values and parsing are unchanged. Handle accounts in your
app and customize `SettingsHelp` / `SettingsInfo`; Unity has no Settings icon initializer to replace.

The auth-only scene and UPM sample are removed. Remaining examples initialize with SSO;
connect your own user for authenticated actions. Generic legacy UnityExample scenes route
`OnLoginRequired` to the SSO sign-in scene. The shell already uses SSO configuration and
Account screens. Its saved selections accept only the existing SSO profiles; invalid selections
return to Configuration.

Native dependencies now target 1.15.0 on Android and iOS. These releases remove native auth
repositories and native account/logout/delete screens, and clean up pending Magic Link state.
The wrapper neither accesses private native storage nor forces a logout of a stored session.
Localization stays owned by the native SDKs.

---

## To 1.12.8 (from 1.12.7)

This release drops the `SettingsAbout` screen event. The "About the community"
screen no longer exists in the native SDKs — Android removed it in 1.13.0 and
iOS in 1.13.0 — and both native pins are now past those versions, so no
platform emits the event any more. It is **removed rather than deprecated**, to
stay iso with the Android and iOS SDKs, which removed it outright.

### Breaking — `OctopusScreen.SettingsAbout` removed

The enum member is gone from `OctopusScreen`. This is source-breaking for any
code that names it — typically a `case OctopusScreen.SettingsAbout:` branch in a
`switch`, whether or not that `switch` has a `default:`. Nothing changes at
runtime, because no native build in the supported range emits it. A `"SettingsAbout"` token arriving from an
older native build parses as `OctopusScreen.Unknown` through the SDK's
unknown-token fallback, so a stale native cannot throw in your event handler.
`OctopusScreen` has implicit ordinals, so the members after the removed one
shift down by one: compare and persist screens by name (`ToString()`), never
by their integer value — the wire protocol itself is name-based.

**Before (1.12.x):**

```csharp
OctopusSDK.OnOctopusEvent += OnOctopusEvent;

void OnOctopusEvent(OctopusEvent e)
{
    var evt = e as ScreenDisplayedEvent;
    if (evt == null) return;

    switch (evt.Screen)
    {
        case OctopusScreen.SettingsList:    TrackScreen("octopus_settings"); break;
        case OctopusScreen.SettingsAbout:   TrackScreen("octopus_about");    break;
        case OctopusScreen.SettingsAccount: TrackScreen("octopus_account");  break;
    }
}
```

**After:**

```csharp
OctopusSDK.OnOctopusEvent += OnOctopusEvent;

void OnOctopusEvent(OctopusEvent e)
{
    var evt = e as ScreenDisplayedEvent;
    if (evt == null) return;

    switch (evt.Screen)
    {
        case OctopusScreen.SettingsList:    TrackScreen("octopus_settings"); break;
        case OctopusScreen.SettingsAccount: TrackScreen("octopus_account");  break;
        // A default branch handles the screens this build does not list —
        // including ones added by a later native SDK — instead of letting them
        // fall through unhandled. It does not protect a reference to a removed
        // member: a `case` naming one must go, default or not.
        default:                                                             break;
    }
}
```

Full release notes live in [CHANGELOG.md](CHANGELOG.md).

---

## To 1.12.0 (from 1.10.x)

1.12.0 moved iOS push notifications to the data-driven native 1.11 API. Push
handling is now symmetric on iOS and Android: detect the notification tap with
your push library (e.g. Unity Mobile Notifications, Firebase Messaging), then
hand the payload to the SDK.

### Breaking — iOS-only `OctopusSDK.OnNotificationTapped` event removed

The SDK no longer detects notification taps itself on iOS. Detect the tap with
your push library on both platforms, then open the SDK with the payload.

**Before (1.10.x, iOS only):**

```csharp
// The native layer (OctopusAppController.mm) captured the tap itself.
OctopusSDK.OnNotificationTapped += () =>
{
    OctopusSDK.Open();
};
```

**After (1.12.0, both platforms):**

```csharp
// In your push library's notification-tap callback:
if (OctopusSDK.IsOctopusNotification(payload))
{
    OctopusSDK.Open(OctopusSDK.GetOctopusNotification(payload));
}
```

See the `PushNotificationsExample` sample for a complete, working wiring on
both platforms.

### Breaking — `OctopusAppController.mm` no longer required

The native `UnityAppController` subclass (`OctopusAppController.mm`) and its
method swizzling are no longer used. Remove the file from your Xcode project
(or your `Assets/Plugins/iOS` folder) when upgrading — leaving it in place is
unnecessary.

### Breaking — notification payload parsing accepts both platform shapes

`OctopusSDK.IsOctopusNotification(payload)` and
`OctopusSDK.GetOctopusNotification(payload)` now accept either:

- a **flat** key/value payload (Android FCM data message), or
- the **iOS** `UserInfo` dictionary, where the Octopus keys are a JSON string
  under the `data` key.

Pass the raw payload from your push library directly — the SDK unwraps the
right shape itself. If you previously pre-extracted the iOS `data` envelope
before calling these methods, remove that code and pass the full `UserInfo`
dictionary instead.
