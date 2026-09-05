# Changelog

Notable changes land under `## Unreleased` first and move into a versioned
section when a release is cut (format inspired by
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/)).

## Unreleased

## 1.12.7 — 2026-09-04

Legacy `OctopusCommunitySDK.unitypackage` for this version is **machine-generated from the package tree by an internal script** and **not import-tested**: no Unity Editor was available when it was built. UPM is the reference install path for this release.

### Changed
- **Android native SDK pin raised from 1.12.1 to 1.13.4** (EDM4U resolver and bridge compile classpath, both). It carries the native side of the two crash fixes below — the community screen no longer crashes when composed before `OctopusSDK.Initialize`, and the Android SDK's own consumer R8 rules now keep Guava for gRPC — plus everything released in Android 1.13.0–1.13.3 (see the [Android changelog](https://github.com/Octopus-Community/octopus-sdk-android/blob/main/CHANGELOG.md)). The iOS pin stays at 1.12.6: this release ships the Android crash fixes only, and the iOS move to 1.13 is a separate port. The Android bridge AAR was rebuilt against 1.13.4.
- **Android no longer emits `OctopusScreen.SettingsAbout`.** The native "About the community" screen was removed in Android 1.13.0. The enum member is kept: iOS 1.12.6 still reports it.

### Added
- `OctopusScreen.OtherUserPosts` (with `ScreenDisplayedEvent.ProfileId`) and `OctopusScreen.Activity`, reported by Android 1.13+ when another member's posts list or the connected user's community activity screen (the unified profile's replacement for `Profile`, distinct from the notification center) is displayed. Before this change those screens parsed as `OctopusScreen.Unknown`.
- **Push Notifications sample: Android notifications are now displayed out of the box.** Octopus push messages reach Android as data-only FCM messages, which neither the OS nor Firebase renders; the sample used to leave that step to the integrator, with a Kotlin README snippet that did not build as-is in a Unity Gradle project.
  - Ships `Plugins/Android/OctopusMessagingService.java`: extends the Firebase Unity plugin's own service (`com.google.firebase.messaging.cpp.ListenerService`), posts Octopus messages on an `octopus-sdk` channel with the app icon, attaches the FCM payload to the tap intent, and calls `super` so `FirebaseMessaging.MessageReceived` / `TokenReceived` keep firing in C#. Framework APIs only, compiled by Unity's exported Gradle project without extra wiring.
  - Ships `Plugins/Android/OctopusPushSample.androidlib`: registers that service on `MESSAGING_EVENT` with a higher priority than the plugin's default one.
  - The C# sample reads the tap from the launch intent (cold start and resume) as well as from `MessageReceived(NotificationOpened)`, dedupes the two by message id, and registers the token from `GetTokenAsync()` on every launch.
  - README *Android — Notification Handling* rewritten around the shipped files.

### Fixed
- **Android: opening the community no longer crashes when Android restores it in a fresh process.** When the OS recreated `OctopusUIActivity` after the game's process had been killed in the background, before the game had called `OctopusSDK.Initialize` again, the native UI read the SDK's dependency container before it existed and the app died with `UninitializedPropertyAccessException: lateinit property koinApp has not been initialized`. The activity now detects the uninitialized SDK, logs an `OctopusForUnity` warning, hands the user back to the game's launcher activity and finishes. The Android bridge AAR was rebuilt.
- **Android: Guava is now kept by the bridge's consumer ProGuard/R8 rules in minified (`minifyEnabled true`) host apps.** gRPC reaches `com.google.common.**` at runtime; a minified Unity host crashed at `OctopusSDK.Initialize` with `NoSuchMethodError: Strings.isNullOrEmpty` (`GrpcUtil.getFlag`) followed by `NoClassDefFoundError: io.grpc.LoadBalancerRegistry`. With the new rules Guava's fate no longer depends on the host's shrinker configuration or tracing order. The rules ship in the AAR, so every Unity host picks them up through the normal Gradle merge — nothing to add on the app side. If the host also bundles a second, older Guava (another SDK's repackaged copy), that classpath conflict still has to be resolved on the app side; keep rules cannot fix it.
- The package could not build on any Unity version below 2021.2, despite `package.json` declaring a 2019.4 floor. Two Android `SetTheme` call sites used C# 9 target-typed `new (...)` (2021.2+ only), and the editor-only mock overlay used C# 8 `??=` (2020.2+ only). Both rewritten to C# 7.3-compatible syntax; no behavior change. The CI compile gate now pins `LangVersion` to the declared floor (7.3) so a future regression fails the build instead of shipping silently.

## 1.12.6 — 2026-07-22

### Added
- **Community access control (Octopus A/B testing).** Two new APIs surface the native `hasAccessToCommunity` / `overrideCommunityAccess` features (iOS 1.12.6, Android 1.12.1 — both already pinned, no native version bump):
  - `OctopusSDK.HasAccessToCommunity` (read-only, cached) plus the `OctopusSDK.OnHasAccessToCommunityChanged` event report whether the current user can access community features, reflecting Octopus' internal A/B test configuration and any override you set. Pushed from native (iOS `@Published`, Android `Flow`) and raised on the Unity main thread.
  - `OctopusSDK.OverrideCommunityAccess(bool, onCompleted, onError)` lets your app grant or block community access directly. It takes **full precedence** over both the internal A/B test config and the analytics-only `TrackAccessToCommunity` signal; the resulting value flows back through `HasAccessToCommunity` / `OnHasAccessToCommunityChanged`.

  Editor mock support (`Mock.EmitHasAccessToCommunity`, faithful `OverrideCommunityAccess` recording) is included. The Android bridge AAR was rebuilt.

## 1.12.5 — 2026-07-15

### Added
- **Forced community orientation.** New `OctopusThemeSettings.ForcedOrientation` (`None` / `Portrait` / `Landscape`), also exposed under a **Behavior** tab in *Octopus SDK > Theme Configuration*. Locks the native Octopus community UI to a fixed orientation independent of the game — e.g. a portrait community inside a landscape game — and restores the game's orientation when the community closes. Defaults to `None` (follows the game/device). Fully contained in the Unity bridge; no upstream native SDK change. On iOS the app-level orientation mask is widened only while the community is shown; on Android the community activity's orientation is set. Platform notes: iOS requires **iOS 16+** (uses the public `requestGeometryUpdate` API — no private-API rotation hacks; ignored below 16, community follows the game). Android 8.0 / API 26 cannot lock a translucent activity, so it opens unlocked on that version only.

## 1.12.4 — 2026-07-09

### Added
- iOS: bumped the native SDK to 1.12.6, Android pin (1.12.1) unchanged — already the latest release.

### Fixed
- `OctopusSDK.ConnectUser` now accepts `null` for `nickname`, `bio`, and `picture` (each is optional). Previously passing `null` crashed on iOS (null `char*` into `String(cString:)`) and hung the awaited connect on Android (non-null Kotlin bridge params); `null` is now coalesced to empty, matching the native SDKs' optional-profile semantics. A blank picture means no avatar.
- Android: the Octopus UI now renders edge-to-edge and draws under the display cutout in every orientation, matching the native Android SDK's host activity. Games running in landscape on notch/punch-hole devices no longer get a black bar pushed away from the camera cutout, and the stray top inset when opening Octopus is gone. The Octopus screens already apply their own safe-area (`safeDrawing`) insets, so content stays clear of the cutout and system bars. No API change.

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
