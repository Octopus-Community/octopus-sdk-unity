# Changelog

Notable changes land under `## Unreleased` first and move into a versioned
section when a release is cut (format inspired by
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/)).

## Unreleased

## 1.14.0 — 2026-09-28

Legacy `OctopusCommunitySDK.unitypackage` for this version was regenerated with Unity Editor 6000.3.3f1 (`build_legacy_package.sh`, the reference path), not import-tested in a scratch project before the cut. UPM remains the reference install path.

### Added
- runtime: `OctopusSDK.SetIcons(OctopusIcons icons)` overrides the icons of the native Octopus UI,
  bridged to Android (`OctopusIcons`) and iOS (`OctopusTheme.Assets.Icons`). Each
  `OctopusIconSlot` takes an `OctopusIcon(androidDrawableName, iOSResourceName)` resolved like
  `OctopusLogo`; unset slots keep the native default and `SetIcons(null)` restores them all. Every
  icon group up to native 1.13 is covered; the `screenStates` illustrations that Android 1.14.1 and
  iOS 1.14.0 add are not bridged yet (#414). A few slots exist on one platform only and are
  ignored on the other (documented per slot). Additive: no existing signature changed. (#366)
- runtime: `OctopusProfile.IsGuest` tells an anonymous guest apart from an authenticated user —
  in forced-login communities `CurrentProfile` becomes non-null again right after `DisconnectUser`,
  as a guest. Mirrors iOS `OctopusProfile.isGuest` and Android `ConnectionState.Connected.isGuest`;
  both bridges now send it with every profile snapshot, and the `OctopusProfile` constructor gains
  an optional `isGuest` parameter (default `false`, source-compatible). Needs the rebuilt bridge
  AAR shipped with this package.
- runtime: `OctopusSDK.SwitchCommunity(string apiKey, ConnectionMode mode, string apiServerHost,
  int apiServerPort, Action onCompleted = null, Action<string> onError = null)` switches community
  on a custom server — the switch counterpart of `Initialize`'s `apiServerHost`/`apiServerPort`,
  forwarded as the native `apiServer` (Android) and `Configuration(apiServer:)` (iOS). The existing
  overload is unchanged and still targets production: an app initialized against a custom host
  must pass it again on every switch. Works on the native pins this release ships (Android
  1.14.1, iOS 1.14.0). (#391)
- sample: Scenarios › Sign-in & user gains a collapsible Unified Profile (exposeClientUserId)
  section after its cards, as in the Android sample — Use backend value, Force active or Force
  inactive (same labels and test ids) — applied live through `DebugOverrideExposeClientUserId`
  and re-applied at every start and community switch, with the effective value read back in the
  section. The sample now routes member-avatar taps to its host profile page, so the Unified
  Profile is reachable on the default demo community; the `communityData` scenario names the flag
  and its effective value.
- sample: Scenarios › Sign-in & user › Unified Profile gains a *Host profile route* switch that
  wires or unwires `NavigateToProfileHandler` live, as the Flutter sample's switch does. On (the
  default, as on Android) member avatars open the sample's host profile page; off, every avatar
  opens the SDK profile. The choice holds across the next Start. (#393)
- sample: Restore the previously validated SDK profile at launch after the first successful
  scenario initialization. First launch still requires a tap; startup failures appear on Home. (#332)
- runtime: `OctopusSDK.OpenActivity(OctopusCommunityMemberId memberId, OctopusNavigationMode? navigationMode = null)`
  opens another member's activity screen, by Octopus profile id (`FromProfileId`) or by client user
  id (`FromClientUserId`, resolved natively), matching Flutter's and React Native's member-scoped
  Activity screen. Additive overload: `OpenActivity()` and `OpenActivity(null)` keep opening the
  connected user's activity. Works on the native pins this release ships (Android 1.14.1, iOS
  1.14.0). (#220)
- sample: Configuration and Account screens, reached from Settings. Configuration picks one of the
  build's two profiles (Default, Forced login) and applies it live through a community switch, no
  restart; Account connects and disconnects the demo SSO user. The chosen profile number — never a
  key or token — is now persisted, so the 1.13.0 note that nothing in this sample is persisted no
  longer holds. Sample only — no package API changed. (#341, #349)
- sample: A `qaTheme` launch extra (`light` or `dark`) sets the appearance for that launch only,
  without saving it, so a QA run no longer inherits the theme an earlier run left behind
  (`adb install -r` keeps PlayerPrefs). The launch log gains ` theme=<value>` only when the extra
  is present. The saved theme's numbers (`Light = 0`, `Dark = 1`) are now explicit and marked as a
  stored contract. Sample only — no package API changed. (#374)

### Changed
- **Native SDK pins raised to the 1.14 line on both platforms**: Android 1.13.4 → 1.14.1 (EDM4U resolver,
  bridge compile classpath and the example's generated copies, all eight lines; 1.14.1 is a
  single-fix patch over 1.14.0: stopping the SDK, switching community or initializing it again
  while it is still starting up no longer closes the app, present since native 1.12) and iOS 1.13.2 →
  1.14.0 (`patch_xcode_proj.rb` → `PACKAGE_VERSION`). Floors unchanged (Android minSdk 21, iOS 13).
  The Android bridge needed no source change for the bump — it uses neither `GuestError` nor raw
  profile tab indices; the shipped AAR is rebuilt against 1.14.1 with the `IsGuest` change above.
  iOS 1.14.0 declares no breaking change. Natively, both SDKs add a Comments tab on profile and
  Activity screens, a "who reacted" list and empty/error screen states; iOS 1.14.0 also ships a
  `PrivacyInfo.xcprivacy`, so a host may need to update its App Store Connect App Privacy answers.
  Theme overrides for the new screen-state illustrations are not exposed by this package yet
  (#414).
- ci: The sample's Play Internal Testing upload moves to CI and authenticates by Workload Identity
  Federation; the local service-account-JSON upload path is removed. `Scripts/store/publish-android.sh`
  now builds and signs by default (`--build-only` is kept as an alias) and, with `--stage`, puts the
  bundle on a draft GitHub release that the dispatch-only `Internal beta sample` workflow uploads.
  The bundle is still built locally: the Unity Editor does not run in CI. Sample tooling only — no
  package API changed.
- docs: The README is reorganized for a quick evaluation (what you get, requirements,
  installation pinned to a release tag, a quickstart checked against the 1.13.0 API, sample
  app, links, license). The long-form reference it carried (connection modes, push notifications,
  groups, badges, theming, locale, analytics, sample configuration, the Xcode 27 known issue)
  moves unchanged to `docs/integration-guide.md`, which is now exported to the public mirror
  (the rest of `docs/` stays internal). No package API changed.
- bridge: The Android Octopus UI now always shows the status and navigation bars, even when the
  game runs fullscreen: `OctopusUIActivity` clears `FLAG_FULLSCREEN` and shows the system bars on
  open, as the iOS presentation already did. Only the Octopus activity's own window changes; the
  game activity's window flags are not modified. (#367)
- sample: Scenario cards read as product copy instead of catalogue data. Each of the 23 listed
  cards now shows what the scenario demonstrates, a sentence of context and the SDK symbol it
  calls — Android's wording verbatim — instead of the catalogue title over a comma-separated
  list of API names. The catalogue titles stay untouched and searchable, so a query that found a
  card before still finds it. Sample only — no package API changed. (#129)
- sample: Every scenario card carries its own `GameObject.name` — `scenarios-<id>-card-panel` —
  instead of the literal `Card` every sibling shared, so a hierarchy dump or a failing UI
  assertion names the card it belongs to. The catalog handle QA taps, `scenarios-<id>-card`, is
  unchanged. Sample only — no package API changed. (#142)
- sample: The dark theme moves onto the octopuscommunity.com navy ladder, matching Android:
  navy cards (`#0F1B2D`) over the ink page, a bluish hairline (`#1E2A3D`), a hover/pressed step
  at `#1D2E48`, a brighter text ladder (`#F2F6FC` / `#E9F0FA` / `#8C9AB0`) and a control outline
  at `#6A7D9B` that keeps 3:1 on every surface. The Home "Current configuration" block is framed
  with a new, stronger hairline (`#243349`). The light theme and the colors handed to the SDK
  are unchanged. Sample only — no package API changed.
- ci: A new `check-firebase-analytics.sh` guard, run in the `Native SDK pin coherence` job with
  its own regression suite, fails a PR that brings Firebase Analytics back into the sample — the
  `firebase-analytics` or `play-services-measurement` coordinates, or the iOS `Firebase/Core` pod.
  The sample's iOS pod moves to `Firebase/CoreOnly`: `Firebase/Core` depends on FirebaseAnalytics
  under CocoaPods (the iOS build resolves through Swift packages and links `FirebaseCore` and
  `FirebaseMessaging` only, so today's builds were not affected). The resolver's line-number
  comments in `mainTemplate.gradle` now match the dependency files again. Sample and tooling only —
  no package API changed. (#396)

### Fixed
- sample: Configuration opens on the profile the SDK is actually running after a Lifecycle
  *switchCommunity (alt key)*, instead of the saved one, so a later Apply of the saved profile
  switches back rather than doing nothing. (#387)
- sample: A startup that throws after the SDK initialized (theme or state replay) no longer
  resets the saved profile and reports a startup failure over a working session; it logs a
  warning instead. (#387)
- sample: The Unified Profile effective value is read again after a community switch while its
  Scenarios section is on screen, and a stale synchronous read failure no longer overwrites a
  newer read. (#393)
- sample: The Debug entry, moved back from the app bar after a theme change made while it sat
  there, takes the new theme's colours instead of the old one's. (#394)
- sample: The notification permission request is retried on the next state change when it throws
  before reaching the OS, instead of being counted as made; the README documents that on iOS the
  push token only arrives after the first successful initialisation. (#394)
- sample: On Android API 34 and below the status bar is transparent over the page, as API 35+
  already draws it: the window was not edge-to-edge there, so an opaque bar cut the dark halo at
  its lower edge. The header still starts below the bar. Sample only — no package API changed.
  (#378)
- sample: The Android system back is only listened to in an Android player: in the Editor, or on
  an iPad with a hardware keyboard, Escape no longer closes screens or sends tabs to Home. The
  confirmation it cancels first is now marked by a `SampleUiBackCancel` component on the Cancel
  button instead of a `-cancel` name suffix, and an Input System test covers a press and release
  inside one update. Sample only — no package API changed. (#375)
- sample: A fully scrolled Home or Community list stops with its last card clear of the fade above
  the tab bar; the safe-area probe releases its Android views on every failure path; the shared
  halo and fade textures are released before an Editor script reload instead of leaking one pair
  per reload. Sample only — no package API changed. (#380)
- sample: A community switch (Lifecycle *switchCommunity (alt key)* or Configuration *Apply* onto a
  profile with another key) moved the session from the demo backend to production; both paths now
  pass the profile's host, and Home names the host the SDK last initialized or switched onto
  rather than the startup one. The `apiServerHost` tooltip and config template state "host only, no scheme,
  port or path". (#391)
- sample: Bridge posts work on the demo community. The sample initialized the SDK without a host,
  so the native SDKs targeted production, where the demo key resolves to a community without
  bridge-post setup, and every Bridge preset failed with "Bridge posts are not configured for this
  community". It now passes the demo backend host, as the Android, Flutter and React Native
  samples do; an optional `apiServerHost` in `OctopusExampleConfig` overrides it, and Home names
  the host in use. Sample only — no package API changed.
- sample: A saved profile that fails to start at launch is forgotten, so the next launch asks for a
  profile instead of failing the same way until the app is reinstalled. Home labels it STARTUP
  FAILED rather than a failed connection call, the Configuration screen that opens over it says
  why, and replaying the saved Force login choice no longer writes a "feature toggled" line in the
  Debug console. After a configuration reset, Configuration highlights the profile the SDK is
  running on instead of Default, so Apply keeps the live session. Sample only — no package API
  changed. (#342, #349)
- sample: The dark theme glows from the top-right corner, as on the other samples: the brand
  blue at 12% fading to transparent over one screen width, fixed to the viewport, behind a
  transparent header, on every sample-owned screen — not on the Community tab, as on the other
  samples. Drawn from a texture generated in code, no new asset. The Home and Community docks
  drop their opaque band in dark theme, so the Open community action sits on the page instead of
  a hard-edged strip, and the list above them fades into the page over its last 24dp instead of
  being cut on a hard line; the fade never takes a tap. The light theme is unchanged.
- sample: On Android the header starts below the status bar again, in both themes. Since the
  sample shows the system bars over an edge-to-edge player, Unity's safe area no longer covered
  the status bar, so the title sat under the clock and the Unity and Debug chips under the status
  icons. The safe area now also clears the system bars that overlap the player's view; the bars'
  backgrounds (and the dark halo) still paint behind the status strip.
- sample: The Android system back (button or gesture) now works across the sample shell. One
  press does what the topmost screen's own controls would: it cancels an open confirmation,
  otherwise presses that screen's on-screen Back (one level, so a Developer tools sub-screen
  returns to its index first), otherwise returns a root tab to Home, and on Home sends the app to
  the background like the native Android sample — it never quits. The community's native screens
  keep their own back. Sample only — no package API changed.
- bridge: Native SDK system-bar colours and icon contrast now follow the resolved Unity theme
  and navigation-bar surface; iOS feed and profile presentations apply the same appearance to
  UIKit and SwiftUI. (#367)
- bridge: Android app bars use the selected community palette for their background and
  text, including while scrolled, instead of capturing the host's theme before it is applied.
- sample: Persist the Appearance theme choice and restore it before the first screen on relaunch. (#344)
- sample: An Android build made without `google-services.json` — a fresh clone, a QA workspace —
  no longer paints a Firebase error over every screen of a development build. The sample checks for
  the Firebase options resource first and, when it is absent, skips Firebase with a single warning;
  with a config present, dependency and token failures are still reported. Sample only — no package
  API changed. (#345)
- sample: Hide the Account screen's Sign out action for a guest visitor, matching Android. (#346)
- sample: Cross-screen links land where they say. The Force login lock note points to
  Configuration, which applies the profile live, instead of asking for a restart; the Community
  band, the Home connection card and the Settings Language row open their scenario directly; a
  scenario's feature chip reopens Scenarios on its section, expanded and scrolled to; Developer
  tools links to Configuration and to Scenarios › Notifications; Configuration opened from Account
  returns to Account on Back or Apply, but a tab switch closes it onto the tab picked. The two
  Settings reset cards merge into one. Sample only — no package API
  changed. (#369)
- bridge: Android `FetchCommunityData` failures now append the native error detail, matching
  iOS, and `Reset` failures include it on both platforms so hosts can diagnose the failure
  beyond the bare sentence. Existing first sentences stay unchanged; empty details are omitted.
  `SwitchCommunity` errors remain generic to avoid echoing a supplied credential. (#291)
- bridge: Drop the dead job reference after an Android community-data collection failure,
  keeping the documented implicit rebind on Initialize/SwitchCommunity working. Pass absent
  member identifiers as empty strings across JNI and normalize them to absent values in
  Android community-data fetch and observation calls. (#192)
- store: Clean up serialized keystore passwords and the generated Gradle export when Android
  publishing is interrupted by SIGINT or SIGTERM, preserving exit status 130 or 143. (#331)
- sample: Declare the iOS export compliance exemption in the exported `Info.plist` to avoid
  repeated compliance prompts on TestFlight uploads. (#320)
- bridge: Android `ConnectUser` failures now search the whole validation-error batch by
  priority (`UserBanned` > `MissingToken` > `ProfileError` > other) instead of reading only the
  first reported error, so a ban arriving alongside another error is no longer masked. Generic
  connection failures (no network, permission denied, server error, etc.) now report a readable
  message instead of `result.toString()`'s `OctopusResult$Failure$NoNetwork@1f2a3b`-style dump —
  same defect class as #183's setReaction/refreshEntitlements/follow-group fix, reusing its
  helper. Error codes and JSON shape are unchanged. (#194)
- sample: Scrolling no longer drops a frame every ninth vsync. The soft elevation behind every
  card was drawn as eight offset copies of the card — nine times its fill rate for a 2 dp rim —
  which a fill-bound mobile GPU cannot pay inside 16.6 ms. Four copies split the same opacity
  budget and keep the rim's width; only the falloff at the corners flattens, measured at
  0.4/255 mean and 23/255 peak against the old rendering. Measured on a 60 Hz Pixel 4a over
  30 s of scripted flings: frames over 25 ms fall from 11.8 % to 0.0 %, p99 from 33.3 ms to
  16.7 ms. (#322)
- bridge: Android connection failures (`refreshEntitlements`, `setReaction`, follow/unfollow
  group) now report a readable message instead of `result.toString()`'s
  `OctopusResult$Failure$NoNetwork@1f2a3b`-style dump. Error codes and JSON shape are
  unchanged. (#183)
- sample: Render at the display's refresh rate instead of the 30 fps Unity gives a mobile
  player that never sets `Application.targetFrameRate`, so scrolling is sampled at 60 Hz (or up
  to 120 Hz where the panel and the platform allow it) rather than 30. On iOS, exceeding 60 also
  needs `CADisableMinimumFrameDuration` in the generated `Info.plist`, which this project does
  not set yet. (#294)
- sample: The Debug entry is visible again in the light theme's navy app bar. Adopted into a header,
  it now wears the app bar's chrome control fill (3.31:1 against the bar) instead of the accent
  fill that equals the navy chrome in light; back on the page it keeps its own style. Internal builds only —
  no package API changed. (#377)
- ios: The activity screen's **Edit my profile** menu item now reaches the host. The iOS bridge
  never set the native `onNavigateToProfileEditCallback`, which that item is gated on, so a Unity
  host subscribing to `OnModifyUser` got the entry point on Android only. It is now wired to the
  same `OnModifyUser` event, with the same field values (`null` from this entry point — no
  specific field) and the same "leave the community, keeping its state" behaviour as SSO's edit
  hook. It fires in `.octopus` connection mode too, where no app-managed fields exist, so it is
  not limited to the SSO/app-managed-fields scenario the README used to describe.

  The callback is wired **only while `OctopusSDK.OnModifyUser` has a subscriber**. The native SDK
  hides the menu item while the callback is nil, precisely so the entry point cannot dead-end;
  wiring it unconditionally would have shown the item to a host with no handler, where tapping it
  dismisses the community and delivers to nobody. Subscribing and unsubscribing now pushes that
  state to the native side, the same way setting `NavigateToProfileHandler` pushes profile-tap
  interception. **Known divergence:** Android wires its equivalent unconditionally — one native
  parameter serves both edit paths there, and the native Android SDK requires it in SSO mode with
  app-managed profile fields — so on Android the item is shown even to a host with no
  `OnModifyUser` subscriber, where tapping it does nothing. Subscribe to `OnModifyUser` alongside
  `NavigateToProfileHandler` and both platforms behave identically. This matches the choice the
  Flutter wrapper already ships for the same native hook. (#280)
- ios: Log when `OpenActivity` cannot reach the SDK core through reflection, instead of
  falling back to the main feed silently. The fallback itself is unchanged, and the legitimate
  "no profile yet" path stays quiet — only a failed core lookup, which a native SDK rename would
  cause, now prints a line. (#234)
- ios: Append the native error description to a failed `FetchCommunityData` message, so a
  host has something to log or display beyond the bare sentence. The sentence itself is
  unchanged, so existing string matching keeps working; how much the appended text says
  depends on the error the native SDK raises. (#276)
- sample: The store builds and the Android QA build report the embedded SDK version as their
  version name instead of the Unity default `1.0.0`. The iOS simulator build is unchanged: it
  never leaves the machine that produced it. (#273)
- sample: The notification permission prompt no longer appears over the first screen at launch.
  The iOS project setting that requested authorization on app launch is off, and the sample asks
  (iOS authorization, Android 13 runtime permission) once the SDK is initialized with Push
  registration on — after the first Start in Configuration, at launch on a later run that replays
  a saved profile, or when the switch is turned back on — as the Android sample does. Sample only
  — no package API changed. (#233)

## 1.13.0 — 2026-09-17

Legacy `OctopusCommunitySDK.unitypackage` for this version was regenerated with Unity Editor 6000.3.3f1 (`build_legacy_package.sh`, the reference path), not import-tested in a scratch project before the cut. UPM remains the reference install path.

### Added
- sample: Show Home cards in SDK, configuration, last connection call and access order, with SDK versions and native pins and details collapsed on each visit. (#157)
- sample: Offer Light and Dark in Settings > Appearance, retain the theme QA handles and session selection, and guard all app bars against theme controls. (#156)
- Add community-data fetching and observation on Android, iOS and the Editor Mock. (#167)
- Add `FormatOctopusCompactCount` and optional `OctopusFont.FontWeight` on Android, iOS and the Editor Mock. (#169)
- Add `ConnectUser` callbacks with typed `OctopusClientUserError` failures on Android, iOS and the Editor Mock. (#168)
- Add `SetReaction` with typed errors and nullable reaction removal on Android, iOS and the Editor Mock. (#163)
- Add `OpenProfile`, `OpenActivity`, profile-tap interception and optional `OctopusNavigationMode` on the Open family, with native bridges and Editor Mock support (#164)
- Add debug configuration overrides and `DebugGetCommunityConfig` on Android, iOS and the Editor Mock. (#165)
- Add `FetchOrCreateClientObjectRelatedPost`, typed errors, `OctopusPost` snapshots and per-object related-post observation on Android, iOS and the Editor Mock. (#166)
- Add `SwitchCommunity`, `SwitchCommunityOctopusAuth`, `Reset`, `Stop` and `Close` lifecycle APIs on Android, iOS and the Editor Mock, with completion/error callbacks for asynchronous operations. (#161)
- Add `FollowGroup`/`UnfollowGroup` with typed errors, `OctopusGroup.CanAccess`/`CanCreateChildren`, and `OnGroupAccessDenied`, with Android/iOS bridges and Editor mock support. (#162)
- Add connected profile snapshots (`OctopusProfile`, `CurrentProfile`, `OnProfileChanged`) and `RefreshEntitlements` with typed errors on Android, iOS, and the Editor Mock. (#160)
- **`OctopusSDK.Version` — the package states its own version at runtime.** A host that has to say
  which Octopus SDK it embeds — an About screen, a crash report, a support ticket — had no way to
  read it in a player build: `package.json` is a manifest the runtime never loads, and
  `UnityEditor.PackageManager` is editor-only. `OctopusSDK.Version` is a `const string` carrying the
  UPM package's version, and only that: the Android and iOS SDKs behind the pins follow their own
  patch streams and this value does not track them. It cannot drift from the manifest —
  `ci/native-pins/verify-native-pins.sh`, the required *Native SDK pin coherence* check on every PR,
  fails when the constant and `package.json` disagree byte for byte, so a release bump that forgets
  the constant cannot merge. The example project's About screen shows it as an **SDK version** row
  and the Settings footer now names it beside the sample and Unity versions. (#116)
- sample: Add the Initial Screen scenario with all 11 catalogue presets, native navigation and explicit member-activity limitations.
- sample: Sign SSO tokens locally with a shared demo secret, per-preset entitlements and reusable user profile fixtures, retaining static-token fallback.
- sample: Replace the hidden Octopus Arcade Easter egg with Reef Run, a Unity 2D mini-game reachable from Home and from `--es qaScenario arcade`, which shares a score to the community as a client-object post and reopens itself in challenge mode when a member taps that post's CTA.
- sample: add the Community & groups scenario screens (groups, syncFollowGroups, groupAccessDenied, communityAccess, contentOptions, reactions, bridge, createPost).
- sample: add the events, trackABTests, forceOctopusABTests and lifecycle scenario pilots.
- sample: add the notSeenNotifications and pushNotifications scenario pilots.
- sample: Add Android QA launch extras for shell tabs, registry scenarios and presets, with single-line `[OctopusQA]` logcat markers and a Config Start integration hook. (#227)
- sample: `BuildScript.BuildAndroidDebug` batchmode entry point producing an unsigned development APK for on-device QA (no keystore, no version code).
- sample: add the theme scenario pilot.
- sample: add the refreshEntitlements, termsAcceptance, profileFieldsLock and communityData scenario pilots.
- sample: Hide Octopus Arcade behind five version taps in About, with a 45-second community mini-game and a local best score.
- sample: Polish sample rendering with Inter SDF text, soft elevation and Material Symbols in both themes.
- sample: Follow system text size (enlargement only, floored at 1.0) with reflowing uGUI labels and fields, cap only shell tab labels at 1.3×, and keep controls reachable above safe-area and keyboard insets.
- sample: Apply the brand light and dark color schemes to the native SDK at initialisation and keep its appearance in step with the sample theme. (#134)
- sample: In internal builds only (excluded from the public mirror), restyle console and feedback as opaque sheets with accessible controls, combined filters, consistent exports, and an honest redacted report review without screenshot upload.
- sample: Refine scenario cards and safe-area layout, preserve results across themes, and add Connection action customization with explicit read-only context and Run inside Parameters. (#200, #202)
- sample: Refine Community launchers, add Settings Appearance and Language rows with explicit local reset states, and refresh About with internal-only design links.
- sample: Refine Home status and disclosures, group searchable scenario rows, preserve navigation state, and move the internal Debug entry into the app bar.
- sample: Add shared light/dark UI tokens, rounded cards, button variants, rows, field and switch states, and consistent dp sizing.
- **Example project: Send feedback accepts an optional multiline description**, redacts sensitive text, truncates console lines before the description to fit the issue URL, and keeps the draft until the sheet closes. (#138)
- **Example project: Developer tools index under Settings → Support**, with the existing Debug console, a live Events log with SDK/HOST chips and Copy log, copyable Debug info, and read-only Feature toggles. (#131)
- **Example project: scenario guidance and running feedback.** Scenario screens show the section’s
  feature state with a link to Scenarios, catalog-based “You will see” guidance, and a Running
  placeholder until completion or failure. Preset and result ids are unchanged. (#128)
- **Example project: executable Customize mode on scenario screens.** Locale and custom-event
  parameters start locked; Customize unlocks them and Run sends the edited values. Reset restores
  the last preset without an SDK call, while preset buttons still fill everything and run in one
  tap. Changed values replace an earlier result with "Not run yet — values changed" until a run
  or value restore. Controls use `<scenario>-customize` and `<scenario>-run`; the existing
  `qa-preset-<scenario>-<n>` and `<scenario>-result` handles are preserved. (#126)
- **Example project: a `Push registration` switch in the Scenarios Notifications section header.**
  Uses Android's wording and `scenarios-toggle-push-registration` id, defaults on, retains its value
  for the process and locks after initialization like Force login. Changes log
  `feature toggled → SDK config rebuilt`; disabled registration skips forwarding new iOS and Android
  tokens and logs the skip without revoking existing registrations. (#130)
- **Example project: a `Force login` switch in the Scenarios tab's Sign-in section head.** The
  shared design contract puts a section's feature switch in its head; Unity had none, and this is
  the one it can honestly carry. Flipping it picks which of the `OctopusExampleConfig` asset's two
  profiles — two API keys, two demo communities — the next `OctopusSDK.Initialize` uses, writes
  `feature toggled → SDK config rebuilt` to the Debug console as a state change rather than an API
  call, and repaints the Sign-in presets so they name the profile they will actually use. The switch
  carries Android's own id and wording (`scenarios-toggle-force-login`, and both effect sentences
  verbatim), so one scripted QA step reads the same on both platforms. This sample initialises once
  per process, so once a scenario has brought the SDK up the switch is drawn dead with the reason
  under it, rather than left live with nothing to change until a restart. **No package API changed,
  and none was needed:** `forceLoginOnStrongActions` is not a parameter of `ConnectionMode.SSO` on
  Android or iOS — it is a field of the server-driven community configuration — so there is nothing
  for this package to forward, and Android's sample models it exactly the same way, by picking a
  different named key. (#110)
- **Example project: a tabbed shell — Home, QA Scenarios, Community, Settings.** `UnityExample` now
  launches into `OctopusSample.unity`, a single scene built entirely in code, instead of the legacy
  scene menu. The **QA Scenarios** tab hosts the catalogue list that used to be a floating overlay,
  and the other three are described in their own entries below. Tab objects carry the shared
  QA catalogue's ids (`home-tab`, `scenarios-tab`, `community-tab`, `settings-tab`), so the same
  scripted pass addresses Unity as it does the other samples. The eight `*Example` scenes still ship
  unchanged and stay reachable in both directions while they exist: the legacy menu offers a **Sample
  shell** button, and the shell's Settings tab a **Legacy demo scenes** button back. Sample only — no
  package API changed. (#65)
- **Example project: the Home tab is now a dashboard.** It states what the SDK is pointed at (server
  environment, community, where the API key comes from, SSO user, entitlements, theme, language),
  whether a scenario has initialized it, what the last connection call did, and the two values the
  package publishes reactively — community access and the unseen-notification count — with a docked
  **Open community** button. It is strictly read-only: it never calls the SDK, not even
  `UpdateNotSeenNotificationsCount()`, so opening the tab cannot change what a QA step then measures.
  Two things it deliberately does not claim: the API key itself is never printed (only its source),
  and the connection card names the last *call* — `CONNECT OK`, `DISCONNECT OK`, `CALL FAILED`,
  `NO CALL` — rather than a session, because this package exposes no connection state to read back.
  A completed connect is not a live session (the iOS bridge reports completion even when
  authentication failed) and a disconnect that throws leaves the earlier session running, so a
  `CONNECTED`/`OFF` pair would be wrong in both directions. Sample only — no package API changed.
  (#65)
- **Example project: the Community tab now opens the SDK's screen, four ways.** `OctopusSDK.Open`,
  `OpenGroup`, `OpenPost` and `OpenCreatePost` each get a control, with fields for the group id, the
  post id and an optional prefilled body (`community-open-button`, `community-open-group-button`,
  `community-open-post-button`, `community-create-post-button`, and the matching `-field` names).
  The tab is a launcher rather than an embedded feed, unlike the Android, iOS, Flutter and React
  Native samples: the Unity player is a single native surface that the SDK's screen is presented
  *over*, so there is no view tree to embed it in. What does port is the samples' state band —
  host sigil, one sentence, at most one action, blocking beating degraded — shown here as
  `community-band-blocked` (no `OctopusExampleConfig` asset, so no API key and no working door) and
  `community-band-readonly` (no connect *call* has completed, so the community opens read-only, with
  an action onto the Scenarios tab). The band speaks about calls rather than sessions for the same
  reason Home's connection card does, and the card under it keeps the fact the band drops: a **Last
  connection call** row in Home's vocabulary, so a completed-but-unauthenticated connect is still
  readable after the band goes down. An empty group or post id is refused rather than forwarded,
  since the SDK would silently land on the main feed; a config asset with no API key in it stops
  every door exactly as a missing one does, rather than initializing with a blank key and failing
  later somewhere else; opening the tab calls nothing, and nothing here publishes — the post
  editor's own send control does. Sample only — no package API changed. (#65)
- **Example project: the Settings tab is now the sample's support surface.** A **Support** group
  (`settings-support-card`) holding **Developer tools** (`settings-devtools-row`, opening the debug
  console) and **About** (`settings-about-row`), a **Reset sample state** control
  (`settings-reset-button`, with an inline `settings-reset-confirm`), a build footer
  (`settings-version-label`), and the **Legacy demo scenes** route that was already there. About
  (`about-screen`) states the sample and engine versions and the licence. Four rows the other
  samples carry are deliberately absent: **Account** and **Change Configuration** navigate to
  screens this shell does not have, and **Appearance** and **Language** would restate two facts the
  app bar's theme toggle and the Home tab already publish — the tab says where that read-out lives
  instead of duplicating it. Two further differences are behavioral: the reset is named for what it
  does, since nothing in this sample is persisted — it clears the sample's own record of what the
  SDK *reported* (the last connection call, community access, the unseen count) and deliberately
  keeps what the SDK is still *configured* with, so initialization and any locale override survive;
  it makes no SDK call, so nothing is deleted on the server and the SDK is not torn down; and About
  states no SDK version, because the Unity package publishes none at runtime and a
  number typed into the sample would go stale on the next bump with nothing to catch it. The
  Developer tools row arrives through a registration seam rather than a direct call, so the sample
  still builds and runs with the mirror-excluded `Assets/Debug/` folder absent — the group simply
  comes up one row shorter. Sample only — no package API changed. (#65)
- **Example project: Settings → Support now carries a Send feedback row.** `settings-feedback-row`
  opens a confirmation screen (`feedback-screen`) that lists exactly what a report will carry — the
  newest 50 Debug console lines, oldest first, plus a table describing the build (where the reader
  was, platform, device, engine, sample version, and the same seven configuration facts the Home
  tab publishes) — above a single **Open a GitHub issue** control (`feedback-open-issue`). That
  control is an `Application.OpenURL` on a pre-filled `issues/new` link and nothing else: no token,
  no API call, no network from the sample. Nothing is transmitted until the reporter presses *Submit
  new issue* in GitHub's own form, where they are signed in as themselves and can edit or delete any
  of it first, which is why the screen shows the inventory rather than asking anyone to read a
  percent-encoded URL. Three properties are enforced rather than trusted, each with an EditMode
  test: the URL is RFC 3986-encoded, so a space or a newline in a log line cannot truncate the
  report at that character; it is measured and trimmed from the oldest console line until it fits
  7000 characters, since GitHub answers a longer one with an error page and the issue is lost with
  it, and the body then says how many lines it dropped; and every value passing through is masked
  for JWT and long-opaque-token shapes, on top of the sample never printing an API key or a
  community name in the first place. Like **Developer tools**, the row arrives through the
  registration seam, so the mirror's copy of the sample — which ships without `Assets/Debug/` —
  comes up one row shorter and needs no change. Sample only — no package API changed. (#65)
- sample: Add the host-rendered profile page `communityData` preset 6 navigates to (`OctopusSampleClientProfileView`), with the catalogue's `clientProfile-data` / `clientProfile-unknown` / `clientProfile-error` destination ids and `clientProfile-back`, so the Unity sample now renders every id of the scenario like the Android, Flutter and React Native samples do. Sample only — no package API changed.
- ci: Add `Scripts/store/publish-android.sh` and `Scripts/store/publish-ios.sh`, which build, sign and upload the sample to Play Internal Testing and TestFlight from a developer Mac, with the `fastlane` configuration they drive. (#28, #196)

### Changed
- docs: Align runner and CI documentation with local Unity gates and PR-body attestations; the Editor never runs in CI by the 2026-09-16 decision. (#259)
- docs: Document the Unity 6000.3 Xcode 27 / iOS 27 UIScene launch refusal and the Unity 6000.5+ re-export workaround, verified with 6000.5.11f1; this is a Unity limitation, not an SDK bug. (#271)
- sample: Update Events log rows incrementally, preserving order and capping rendering independently at 500 entries. (#172)
- sample: Rename the sample's Android application id and iOS bundle id to `com.octopuscommunity.sdk.unity.sample`, matching the other Octopus sample apps, and set the Apple developer team. Push notifications stay non-functional until the Firebase apps are re-registered for the new identifiers. (#196)
- **Example project: the QA Scenarios tab is a searchable, sectioned list of what the sample can
  actually demonstrate.** The 27 catalogue entries are grouped under the same six headings the other
  samples use — Sign-in & user, Presentation modes, Community & groups, Notifications, Theme &
  language, Host callbacks & events — each head collapsible and carrying its card count, above a
  search field matching a scenario's title, capability line or id. Two behavioral consequences:
  a scenario **no pilot drives is no longer listed at all** (the shared design contract: an
  unimplemented scenario is not shown, its porting status lives in the versioned catalogue), so the
  tab renders one card per driven scenario and grows with each scenario change rather than showing
  27 doors onto empty rooms; and the per-card shortcut into a legacy `*Example` scene is gone,
  because it was a one-way `SceneManager.LoadScene` that re-initialized the SDK behind the running
  scenario's back. The legacy scenes stay reachable from Settings → **Legacy demo scenes**. Sample
  only — no package API changed. (#65)
- **Example project: sample screens take their colors and type sizes from one place.** A new
  `OctopusSampleBranding` holds the sample's light and dark palettes, and `SampleUi` reads them, so
  the scenario list and the scenario screen follow the shared design tokens rather than their own
  literals — including the minimum text size and touch-target floors, which the shared builders now
  enforce structurally: opacity-derived colors are resolved to opaque values (the project renders in
  linear color space, where a translucent label does not measure what an sRGB contrast figure says),
  and the canvas reference narrows on screens under 360dp so those floors survive a phone narrower
  than the design reference. Screens are held inside `Screen.safeArea`. The app name shown on the
  launcher is now **Octopus Sample for Unity**. Sample only — no package API changed. (#65)
- **Example project: the QA scenario presets reach the SDK through a seam the tests can observe.** The three scenario pilots call `OctopusScenarioSdk.Current` instead of the static `OctopusSDK` entry points directly, which lets the EditMode suite assert which entry point a preset reaches, with which values, and that a second tap is refused while a call is still in flight. The eight `*Example` scenes are unchanged and remain the reference for how to call the package. Sample only — no package API changed. (#99)
- **Example project: About states what the sample is built on, and points at the design target.**
  The About screen now carries the engine attribution Unity's trademark guidelines ask for — a
  **Made with Unity** line and, verbatim, the notice that the application is not sponsored by or
  affiliated with Unity Technologies — plus an optional **Design reference** row
  (`about-design-reference-row`) opening the shared target the five samples implement. The badge is
  the words rather than Unity's official artwork, which this repo has no copy of and may not
  redraw; the row's URL comes from the git-ignored `OctopusExampleConfig` asset, because
  `Assets/Scripts/` is published to the public mirror, and the row is absent rather than dead when
  nothing is configured. Sample only — no package API changed. (#65)

### Removed
- ci: Remove the Main workflow. Both of its store-upload legs needed a licensed Unity Editor, which no runner has; the local scripts above replace them. (#28, #196)

### Fixed
- sample: Keep Settings Appearance and read-only Language current-value rows synchronized with shared sample state; Appearance links to its dedicated page. (#133)
- sample: Keep custom Run inside Parameters across screen rebuilds, with regression coverage for edited values and reset. (#174)
- sample: Centralize overlay sorting orders and clarify that Debug info summarizes sample facts without inventorying native metadata or callback gaps. (#173)
- sample: Give Debug info rows explicit stable QA ids independent of their display labels. (#171)
- sample: Keep the Debug entry and console above scenario and profile overlays, including when reopening the console. (#127)
- sample: Pin the Android target SDK to 36 so the sample build no longer depends on the highest SDK platform installed on the runner. Play rejects bundles targeting API 35 or lower. (#197)
- sample: Keep long app-bar titles at title size before ellipsizing, and drop the leftover sheet handle, rounded corners and shadow from the full-screen Debug console and Send feedback routes. (#249)
- sample: Move Initial Screen from Community & groups to Presentation modes, remove redundant scenario status and unused profile input, clarify preset routing and field labels, and guard missing editor prefill. (#247)
- sample: Use the shared app bar for Debug console and Send feedback, preserving control names and navigation while aligning back arrows, touch targets and safe-area content spacing. (#248)
- sample: Refuse missing SSO credentials visibly in legacy scenes and Home/Connection without faulting token callbacks; correct entitlement guidance, isolate Unity fixture identities, redact signing-secret fields and document the second key required by Switch community. (#246)
- sample: Unify app bars with arrow back controls, aligned actions and ellipsized titles, and normalize page gutters and section spacing. Keep the Scenarios search placeholder inside the text viewport with the same padding as entered text.
- sample: Fetch the current Android push token on cold start, report when Push registration is waiting for a token, guard Firebase dependency failures, and refuse lifecycle actions before SDK initialisation. (#229)
- sample: Stabilize grouped list borders while scrolling and keep pointer selection from leaving focus outlines after a drag.
- sample: Keep the internal Debug entry inside the app bar in either bootstrap order and across tab/theme rebuilds. (#149, #205)
- sample: Place the Unity identity chip beside the Home app-bar title, with no chip on other tabs.
- sample: Make card elevation visible on both light and dark surfaces. (#215)
- sample: Give tertiary actions a resting outline. (#199)
- sample: Restore list detail ink after disabled states. (#199)
- sample: Show press feedback on scenario section headers. (#205)
- sample: Preserve the entitlement limitation after Connection Customize → Run. (#202)
- sample: Explain why edited scenario values need another run. (#202)
- sample: Continue scenario chrome into safe-area insets. (#202)
- sample: Keep the Push registration switch usable after SDK initialisation, iso Android/Flutter, and forward FCM/APNs tokens from the shell — the cached token is registered as soon as registration is re-enabled. (#222)
- sample: Share app-bar sizing and spacing across detail routes. (#202, #204)
- sample: Preserve configured native logos, fonts, app name and navigation styling when applying sample colors. (#208)
- sample: Distinguish read-only Community access from blocked configuration. (#204)
- sample: Record native presentation failures in the internal console. (#204)
- sample: Internal builds retain feedback drafts for browser retries and Copy. (#209)
- sample: Keep feedback context labels readable beside long device values. (#209)
- sample: Display the same capped feedback context values as the report. (#209)
- sample: Internal reports retain readable source paths and type names during redaction. (#209)
- sample: Mask bare hosts in internal reports while preserving SDK and engine versions. (#209)
- sample: New log entries preserve internal console acknowledgements. (#209)
- sample: Size header Back pills on their own label so the Inter face no longer wraps "Back" over two lines, never below the 48dp touch target. (#213)
- sample: Keep the internal Debug entry reachable on the About, Appearance, Dev tools and scenario screens, which used to cover it. (#215)
- sample: Release transient sprites at shutdown/reload, avoid repeated unchanged layout walks, and strengthen URL validation and sample regression coverage. (#199, #204, #208, #213)
- **Example project: Send feedback preserves the last content breadcrumb through Settings and Support navigation, including the scenario id after its overlay closes.** Opening another content tab or scenario replaces it. (#136) Sample only — no package API changed.
- **Example project: Send feedback is limited to internal builds and Editor Play mode**.
  Android QA builds opt in with `OCTOPUS_INTERNAL=true`; store builds remove the define, and builds restore the previous
  Android defines on success or failure. Developer tools stays available. (#137)
- **Example project: Send feedback includes the runtime SDK version, native build pins and current Force login and theme states in both the confirmation inventory and the pre-filled issue.** Fields follow the Android inventory order; build pins are captured from dependency metadata when building the sample. (#135) Sample only — no package API changed.
- **Example project: Debug console controls now use the catalog's exact identifiers, and scenario card identifiers are attached to their opening buttons.** (#125)
- **Example project: the Debug console's EVENT rows carried no detail.** Every row read `EVENT · PostCreated` and nothing else, so a copied QA report lost the ids the event was reported for. The console described an event by reflecting over its properties, and an `OctopusEvent` exposes its data as public fields — only the inherited `Kind` is a property. Rows now carry the event's members, filtered through an explicit allowlist of ids, enum kinds, counts and flags, so a member added later by a native SDK reaches the clipboard only once someone decides it may. Sample only — no package API changed. (#96)
- ci: Keep local Unity gate failures visible before cleanup diagnostics and retain a diff of restored changes. (#268)
- ci: Restore tracked Unity serialization changes after local EditMode tests and Android builds, and explicitly report untracked files created by the run instead of issuing an attestation. (#266)

## 1.12.8 — 2026-09-08

Legacy `OctopusCommunitySDK.unitypackage` for this version was regenerated with Unity Editor 6000.3.3f1 (`build_legacy_package.sh`, the reference path), not import-tested in a scratch project before the cut. UPM remains the reference install path.

### Added
- **`link` and `background` theme colors.** `OctopusColorScheme` gained two optional colors, matching what both native SDKs already accept: `link` paints the URLs rendered inside posts and comments, `background` paints every community screen. They are independent of the primary set and of each other — omit one, or pass `Color.clear`, and the native SDK keeps its own default for that slot. Existing four-argument `new OctopusColorScheme(primary, primaryLow, primaryHigh, onPrimary)` calls compile and behave exactly as before. Both colors can be given a different value per appearance through `lightColorScheme` / `darkColorScheme`, and both are exposed in **Octopus SDK > Theme Configuration** under *Optional*, each behind its own checkbox. (#26)
- **Example project: a generic QA scenario screen, with three catalogue-driven scenarios.** `UnityExample`'s **QA Scenarios** list now opens a real screen for `connection`, `customEvents` and `locale` instead of only pointing at a related demo scene. Each screen drives its scenario from single-tap presets that pre-fill every field, and reports what the SDK did in a live result panel; the preset buttons and the result panel carry the test ids the shared QA catalogue publishes, so the same scripted pass can drive Unity as it drives the other samples. Nothing reaches the SDK until a preset is tapped. The eight existing `*Example` scenes are unchanged and still the reference for how to call the package. Sample only — no package API changed. (#66)

### Changed
- **A theme color left unset now keeps the native default instead of being applied as transparent.** This was already true of `link` and `background`; it now holds for the four colors of the primary set as well, on both platforms. `Color.clear` — an unticked checkbox in **Octopus SDK > Theme Configuration**, or `Color.clear` passed to `OctopusColorScheme` — is the "not set" sentinel for every color of the scheme, and the native SDK keeps its own value for that slot. Two consequences for a partially configured scheme: a scheme whose only enabled color is `link` or `background` now reaches the native SDK (it used not to be sent at all, since the enable check covered the primary set only), and a scheme that enables some but not all four primary colors mixes your colors with the native ones rather than painting the disabled ones transparent. A scheme with all four primary colors enabled behaves exactly as before. (#26)
- **iOS native SDK pin raised from 1.12.6 to 1.13.2** (`UnityPackage/Editor/ruby/patch_xcode_proj.rb` → `PACKAGE_VERSION`, the SwiftPM `exact:` pin the Xcode post-process writes). The Android pin stays at **1.13.4**, so both natives are on 1.13.x again and the CI pin guard runs with no drift tolerance. What the bump changes for a Unity host, on iOS:
  - `OctopusScreen.OtherUserPosts` (with `ScreenDisplayedEvent.ProfileId`) is now reported on iOS too, as it already was on Android since 1.12.7. It previously parsed as `OctopusScreen.Unknown`.
  - **A prefilled post no longer needs text or an image.** `OctopusSDK.OpenCreatePost` with only a topic (and/or a CTA) now opens the editor on the preselected group instead of falling back to the empty editor — the iOS 1.13.0 Bridge Share change, matching what Android already did. A blank `OpenCreatePost()` still opens the plain editor with no prefill on both platforms. Note one native difference that this package does not paper over: a post created from a topic-only prefill (no text, image or CTA) is attributed as *prefilled from client* on iOS (which keys the attribution on the presence of any prefill) and as a *user* post on Android (which keys it on text, image or CTA content). A CTA-only prefill is attributed as *prefilled from client* on both platforms.
  - The community UI ships 24 locales, including right-to-left ones, and asks for explicit terms acceptance where the community requires it.
  - Links carry a dedicated color and every screen paints the community background color (iOS 1.13.0 and 1.13.2). Both ship as native defaults, and both are now settable from Unity — see the `link` / `background` entry under *Added*.
  - Builds with Xcode 27 no longer hit the CoreData issue fixed in iOS 1.13.1.

  The unified-profile APIs added by iOS 1.13.0 (`set(onNavigateToProfileCallback:)`, `OctopusInitialScreen.activity`, `fetchCommunityData`, the `activityButton` theme icon) are **not** surfaced by this package yet — exposing them is a separate port on both platforms. As a consequence `OctopusScreen.Activity` is still reported by Android only: the iOS SDK has no matching screen event on 1.13.2. Full native notes: the [iOS changelog](https://github.com/Octopus-Community/octopus-sdk-swift/releases).

### Removed
- **`OctopusScreen.SettingsAbout`.** The native "About the community" screen was removed in Android 1.13.0 and iOS 1.13.0, and both native pins are now past those versions — no platform emits the event any more. Removed rather than left deprecated, to stay iso with the Android and iOS SDKs, which removed it outright. A `"SettingsAbout"` token arriving from an older native build parses as `OctopusScreen.Unknown` through the existing unknown-token fallback, so nothing throws. (#25)

### Breaking
- **`OctopusScreen.SettingsAbout` is gone**, which is source-breaking for any code that names it — typically a `case OctopusScreen.SettingsAbout:` branch in a `switch` over `OctopusScreen`. Delete the reference. A `default:` branch handles values a build does not list, but it does not protect a reference to a removed member. See [MIGRATING.md](MIGRATING.md). (#25)

### Fixed
- **iOS builds compile again on Unity 6000.5.** Unity 6000.5 removed `UnityWillPause()`, `UnityWillResume()` and `UnityIsPaused()` from the trampoline's `UnityInterface.h` and deprecated `UnityPause(int)`, replacing them with `UnitySetPlayerPause(mode, flags)` in a new `UnityInternalInterface.h`. `Runtime/iOS/OctopusUnityPause.mm` — which suspends the player loop while the community UI is presented — still called the old trio, so an iOS export from 6000.5.x failed in Xcode with `use of undeclared identifier 'UnityWillResume'`. The file now feature-tests the new header and drives `UnitySetPlayerPause` with the same mode/flag sequence the 6000.5 trampoline uses on background/foreground; the pre-6000.5 path is unchanged. Android was never affected. (#95)
- The README now documents a tag-pinned UPM URL (`?path=UnityPackage#vX.Y.Z`) for reproducible installs, next to the existing unpinned one. (#83)
- **Push Notifications sample, Android:** the launch-intent tap dedupe now falls back to the bare `message_id` extra when FCM's `google.message_id` is absent. A payload carrying only the legacy key produced a null dedupe key, so the still-set launch intent was re-handled on every focus regain and the Octopus screen re-opened. (#56)
- The example project's generated Android dependency files (`UnityExample/Assets/Plugins/Android/mainTemplate.gradle`, `UnityExample/ProjectSettings/AndroidResolverDependencies.xml`) were left on Octopus Android 1.12.1 when 1.12.7 raised the pin to 1.13.4; they now match the resolver and the bridge. Only the example was affected — a host's own EDM4U resolve always read the package's `OctopusDependencies.xml`. (#64)

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
