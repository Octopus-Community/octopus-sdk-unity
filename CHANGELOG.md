# Changelog

## 1.12.1 — 2026-06-10

### Added
- Editor Mock Mode: the SDK now runs in the Unity Editor without a device build — async calls (`ConnectUser`/`DisconnectUser`, group sync/fetch) resolve instead of hanging, an in-Editor overlay shows the simulated screen, and `OctopusSDK.Mock` exposes a recorded call log plus drivers (`EmitNotSeenCount`, `EmitLoginRequired`, `EmitNavigateToClientObject`, `EmitGroupsChanged`, `EmitModifyUser`) for manual iteration and automated EditMode tests. Toggle at runtime with `OctopusSDK.Mock.Enabled` / `OctopusSDK.Mock.ShowOverlay` (or an optional `OctopusMockSettings` asset); on by default in the Editor and fully compiled out of device builds.
- Prefilled posts can include a call-to-action button via `OctopusPrefilledPost.CtaLabel` / `OctopusPrefilledPost.CtaUrl` (both required, otherwise the CTA is omitted).
- `OctopusSDK.NavigateToUrlHandler` to intercept CTA/link taps inside Octopus and return a `UrlOpeningStrategy` (`HandledByApp` to handle it yourself, `HandledByOctopus` to let Octopus open it). When unset, Octopus opens every URL as before.
- `OctopusSDK.OnNavigateToClientObject` event, raised when a user taps a CTA on a post linked to one of your own objects (article, product…); the argument is the `clientObjectId`.

### Fixed
- iOS: opening the main feed (`Open()` / `OpenPost("")`) right after `OpenCreatePost` could re-show the post composer instead of the feed; each navigation call now resets to the requested screen.
- Android: CTA/link tap interception (`NavigateToUrlHandler`) and `OnNavigateToClientObject` now fire immediately and bring your app to the foreground, instead of only running after the user manually closed the Octopus UI.

## 1.12.0 — 2026-06-04

### Changed
- Upgrade underlying native SDKs to 1.12.0 (Android + iOS).
- iOS push notifications now use the data-driven 1.11 API. The native
  `OctopusAppController.mm` file and method swizzling are no longer required.
  Detect taps with your push library on both platforms, then call
  `OctopusSDK.Open(notification)`.

### Added
- `OctopusSDK.OpenPost(postId)` and `OctopusSDK.OpenCreatePost(prefilled)` to open
  the post-detail and post-editor screens directly (with optional prefilled text, topic, and image).
- `OctopusSDK.OpenGroup(groupId)` to open a specific group's feed.
- Sync-followed-groups API: `OctopusSDK.SyncFollowGroups(...)`,
  `OctopusSDK.FetchGroups(...)`, `OctopusSDK.OnGroupsChanged`, and the
  `OctopusGroup`, `OctopusSyncFollowGroupAction`, `OctopusSyncFollowGroupResult`,
  `OctopusSyncFollowGroupStatus` types.

### Breaking
- Removed the iOS-only `OctopusSDK.OnNotificationTapped` event.
- Removed the requirement to add `OctopusAppController.mm` on iOS.
- `OctopusSDK.IsOctopusNotification` / `GetOctopusNotification` now accept either a
  flat payload (Android FCM) or the iOS `UserInfo` (with a JSON `data` envelope).

## 1.10.1 — 2026-04-13

### Fixes
- Fix iOS push notification navigation to correctly handle notification taps
- Use method swizzling for iOS notification tap handling with Unity Mobile Notifications
- Decouple iOS push notification handling from Firebase dependency
- Ensure thread safety for SDK initialization and proper event cleanup in examples
- Use Android framework APIs for notification permission in examples

### Documentation
- Document iOS-specific push notification setup (native `UnityAppController` subclass approach)
- Add Firebase setup instructions for Android

### Chore
- Gitignore Firebase config files in UnityExample

## 1.10.0 — 2026-04-08

First public release of the Octopus SDK for Unity.

### Features
- Initialize the SDK with SSO connection mode
- Connect and disconnect users with profile fields (nickname, bio, picture)
- Open the Octopus community UI
- Theme customization (light/dark color schemes, logos, fonts)
- Push notification support (registration and not-seen count)
- Language override
- Login-required and modify-user event callbacks
- Track custom events and community access
- Android and iOS native bridge support
