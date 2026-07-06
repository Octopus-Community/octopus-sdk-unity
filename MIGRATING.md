# Migrating

This guide consolidates the migration notes for releases of the Octopus SDK
for Unity that contain breaking changes. Each section lists the breaking
changes first, then how to adapt. The most recent version is at the top.
Full release notes live in [CHANGELOG.md](CHANGELOG.md).

- [To 1.12.0 (from 1.10.x)](#to-1120-from-110x)

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
