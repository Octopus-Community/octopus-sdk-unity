# Changelog

Notable changes land under `## Unreleased` first and move into a versioned
section when a release is cut (format inspired by
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/)).

## Unreleased

_Nothing yet._

## 1.12.3 — 2026-07-06

### Fixed
- iOS: bumped the native SDK to 1.12.5, which fixes a spurious error alert ("We are trying to retrieve your data. Please try again in a few moments.") that could appear on the first post in SSO / bridge-share communities even though the post was published successfully. iOS now aligns with Android and no longer surfaces this transient, self-healing connection error. No API or behavior change. Android pin (1.12.1) unchanged.

## 1.12.2 — 2026-06-25

### Added
- Upgraded the underlying native SDKs to Android 1.12.1 / iOS 1.12.4.
- `OctopusSDK.Initialize` now accepts an optional custom gRPC server host/port (`apiServerHost`, `apiServerPort`) to target a non-production endpoint (e.g. staging); omit them to use the default production endpoint. Mirrors the native `ApiServer` configuration on both platforms; traffic is always over TLS.
- Bridge Share image signing: `OctopusPrefilledPost.SignBridgeShare` — a callback that authorises a prefilled-share **image** in a community that restricts member pictures. The SDK computes a content fingerprint and calls your callback; your backend returns a short-lived HS256 JWT (`bridge_fingerprint` claim). It fires only when the share carries an image and the community gates pictures, on **both iOS and Android**, over the loop-independent native channel — so it runs on a **background thread** while the Octopus UI is open: use loop-independent I/O and never ship your signing secret in the app.
- iOS: the Unity game loop is now suspended while the Octopus UI is open (matching Android), firing `OnApplicationPause`/`OnApplicationFocus`. Octopus events and mid-session token refresh keep working via a loop-independent native channel.
- Android: fixed a mid-session token-refresh stall when the Octopus UI was open (token request now uses the loop-independent lane).
- `OctopusSDK.OnOctopusEvent` — a real-time stream of Octopus community events (post/comment/reply created, content deleted, reaction modified, poll voted, group follow changed, gamification points gained/removed, screen displayed, post/comment/translation clicks, profile modified, session start/stop) for analytics, gamification, or backend sync, on **both iOS and Android**. See `OctopusEvent` for the typed catalogue. Handlers fire on a **background thread**, in order — uniform on both platforms — so do thread-safe/backend work directly and use `OctopusMainThread.Post(...)` for Unity-side work. Android-only fields (the iOS SDK lacks them): `ProfileReportedEvent`, `ContentDeletedEvent.ParentId`, `ContentReportedEvent.ContentKind`; report `Reasons` are raw platform tokens.

### Changed
- Android: URL interception (`NavigateToUrlHandler`) is now resolved **synchronously** over a loop-independent native channel, the moment a URL is tapped. Returning `HandledByOctopus` opens the system browser while **keeping the Octopus community screen open**; only `HandledByApp` brings your app to the foreground. The handler runs off Unity's main thread — keep it to a fast routing decision and do Unity-side work after your app regains focus.

### Fixed
- iOS: bumped the native SDK to 1.12.4, which restores compilation under Xcode 27 / Swift 6.4 (`Sendable` closure in `Compat.ScrollView`). No API or behavior change.
- Android: reusing the Octopus screen for a follow-up `Open*` call now re-reads the requested destination (via `onNewIntent`) instead of briefly showing the previous screen.

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
