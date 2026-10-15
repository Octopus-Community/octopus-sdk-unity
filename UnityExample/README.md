# Unity sample

## Sample shell

Three tabs: **Scenarios · Community · Settings**, opening on Scenarios. Home and Explorer
are absent by design. Tab content, scenario results/customization and detail back stacks survive
switching tabs. The former Home information cards (initialization record, current configuration,
last connection call, community access/unseen count) live in Settings › Developer tools › Debug info. Community keeps the native
launchers. Reef Run is under **Settings > Demo game**; legacy scenes stay under **Settings > About > Legacy examples** until
all their scenarios migrate.

`OctopusSampleBranding.cs` is the sample token source, mapped from Android `SampleTokens.kt`:
opaque sRGB light/dark roles, 4dp spacing grid, 16dp gutters/24dp group gaps, 6/8/12/16dp radii
and pill buttons, 48dp touch minimum. Typography uses the existing OFL Inter/TMP hook at
22/18/16/12sp; system text scale, width-based Canvas scaling and safe-area handling still apply.
Native gray/hover theme roles and status semantics belong to later phases.

## Settings and developer tools

Settings groups Account, SDK preferences, Support and Reset around a Community/Server summary.
Its status describes the sample's initialization and connection-call record. The Unity bridge
does not expose native initialization or connection state.

**Appearance** offers System, Light and Dark, plus Octopus navy and SDK default. System reads
Android's resource configuration or iOS's screen traits, on resume and while the shell runs.
In the Editor it follows the Editor skin. Preset changes use the existing colour APIs without
restarting the SDK. SDK default restores native colours; the shell keeps its own brand tokens.

**Language** offers Follow system, English, French, German, Italian, Polish, Portuguese,
Spanish, Swedish and Turkish. It saves the choice and applies it through `OverrideDefaultLocale`.
The sample stays in English. Follow system reapplies the current device language on resume:
the Unity bridge cannot clear its nonnull locale override.

**Developer tools** opens Host app integration, Events log, Debug info and Debug console.
Host integration uses the profile handler, required `OnLoginRequired` → Account route,
content-button events, group-denied events and URL handler. Profile editing and in-tab native
community navigation are unavailable. The Unified Profile scenario owns its profile route.
Other choices apply immediately and survive restart. The app's URL policy handles contact links,
all links, or lets Octopus handle them. Handled links open a host confirmation screen.

Events log shows SDK events and host analytics calls, excluding ordinary API/state entries.
The optional internal recorder supplies a **Debug Console** bottom sheet with independently
selected Event, State and API Call filters and counts. Copy exports the displayed snapshot;
Clear only clears the local recorder. Drag its handle to resize or close it. Debug info groups
SDK observations, feature toggles, configuration, host integration and build facts. Public builds
that omit `Assets/Debug` still show host integration and diagnostics and explain the absent recorder.

**About** lists sample/build version, SDK/native pins, Unity version, licences and repository
links. Android's build number comes from package metadata; iOS uses Unity's build GUID.
Play Store installation/update state is unavailable without native plugin work. The Unity-specific
Reef Run entry remains on Settings; transitional legacy examples live under About.

**Reset Configuration** asks first, stops the SDK, clears saved server, community, SSO user,
appearance, language and host choices, then opens Configuration. A failed Stop keeps the setup.
Nothing is deleted on the server. Scenario results and the independent console log are retained.

CI presets require distinct demo keys; a missing key disables its preset instead of substituting
Default. The generator accepts `OCTOPUS_FORCED_LOGIN_API_KEY`, `OCTOPUS_MANAGED_FIELDS_API_KEY`
and `OCTOPUS_SOME_MANAGED_FIELDS_API_KEY`, alongside `OCTOPUS_COMMUNITY_API_KEY`. Optional
unconfigured secrets leave those presets disabled. Demo-only inputs stay in the ignored build
asset; runtime configuration never saves credentials in PlayerPrefs.

## Scenarios

The six scenario groups open **collapsed**, with a count badge and chevron. Leaving the tab
clears search and resets expansion and scroll. Searching matches card title, subtitle, reference
API chip and id; matching groups expand automatically, with no feature switches in the results.
The input stays mounted while the list filters after a 120 ms pause. An empty result shows only
`No scenario matches "<query>".`

A card opens a separate detail screen with Back, a large title, API identity and description,
the feature-state banner and a **Try it** block. **Change** on the Force-login banner opens
Settings > Configuration. Presets only fill values. **Execute** runs the selected preset or,
after **Customize**, the edited values. **Reset to preset** restores values without a call.
The **Result** starts at `Not run yet`; changed values mark an earlier result stale.

**Read a member's community profile** includes an editable **User ID** input: typing an Octopus
profile id selects the raw `FetchCommunityData(FromProfileId(...))` lookup. A preset instead
selects its client-id lookup, observation, contract check or host-profile action; Execute performs
that selected action. Unity has no connected Octopus profile-id getter, so the initial input uses
the existing demo fixture, while client-id presets use the observed/configured app identity.

**Unified Profile** is a detail screen with a labelled switch, a collapsed **Backend flag
override**, and **Verify in Community** through the native launcher. Its switch combines the
existing exposed-client-id override and host profile callback. Both apply immediately in Unity;
no restart or different auth preset is implied. Members without an exposed app id keep the native
profile flow. The native home-profile button cannot be rerouted to Activity with the Unity API.

**Sign in and out** shows live rows from `CurrentProfile` / `OnProfileChanged`, including its
guest flag, plus explicit connect/disconnect Execute buttons. Unity does not expose the native
`connectionState` / `isUserConnected` streams; the readout states this and never treats a completed
connect call as a connected session. Initial-screen presets 8/9 use the existing
`OpenActivity(OctopusCommunityMemberId)` overload for a member's app id / profile id.

The list omits the Android host-container full-screen/modal/sheet scenarios: the Unity bridge
exposes native launchers rather than an embedded view. Counts reflect supported cards. Card chips
use Android reference names for cross-sample navigation; the pilots' `ApiSymbols` retain the
actual C# entry points. The Community launcher and Settings areas remain separate flows.

## Configuration and Apply

First launch stays on **Scenarios** and automatically starts the build's default demo profile.
Missing credentials leave a visible startup failure and a link to **Settings > Configuration**;
they never produce a fake initialized state. Community continues to use native launchers.

Configuration has four collapsible sections, in order: **Server environment**, **Community**,
**Connect your users (SSO)** and **Theme**, followed by **Host app integration** information.
Selections are a private draft. **Apply** stays on this screen and reports changed, unchanged or
error; **Cancel/Back** discards edits. Controls stay locked during Apply; after 60 seconds without
a response, retry is available and late callbacks cannot overwrite a newer selection. Retry waits for an outstanding native request
to complete before switching again; the existing SDK has no cancellation API, so restarting the
sample is the recovery path if that native request never completes.

**Demo** resolves the host injected into the local `OctopusExampleConfig` build asset (the sample
fallback is `DemoApiServerHost`), never a host restored from preferences. **Custom host** takes a
host name only; the existing sample facade uses TLS port 443. Feature environments are a later phase.
Community offers four demo auth presets and a masked Custom key. Each named preset needs its own
local key: Configuration never silently substitutes the default key for an unavailable preset.
The optional `someManagedFieldsProfile` supplies the nickname-managed preset. API keys, tokens and
signing secrets remain in memory/the local build asset and are never saved in PlayerPrefs.
Custom keys therefore need re-entry after restart. Only nonsecret choices are persisted.

The SSO picker includes `android-sample-user`, `qa-tester-ios`, `qa-tester-android` and the current
build identity. A changed identity connects on Apply and needs the local demo signing secret;
a static token for a different identity is refused. Forced-login/Push switches remain in Scenarios;
Configuration reads the selected preset's forced-login policy. Unified Profile is unchanged here.

Theme supports **Octopus navy** and **SDK default** using the existing color setters. SDK default
sends unset color slots to restore the native SDK palettes; it does not duplicate native defaults.
Light/Dark apply to the sample and native community. System follows the device in the native
community; the sample Canvas retains its last Light/Dark palette because it has no shared OS
appearance observer. Host callbacks, profile navigation and links stay outside this draft.

## Launcher exports

`python3 UnityExample/Tools/export_launcher_icons.py` (Pillow + CairoSVG) regenerates every
existing Android/iOS texture slot from `Tools/launcher.svg`, retaining importer GUIDs. The A3
family uses the white Octopus mark/three dots over a navy gradient and a compact Unity cube.
On macOS system Python, set `CAIRO_LIBRARY=/opt/homebrew/lib/libcairo.2.dylib` if Cairo
is installed by Homebrew but cannot be discovered. Android's existing adaptive slots use the same opaque tile for both layers; iOS/store slots
are opaque. No store listing is changed by this exporter.

## Push notifications

The sample asks for notification permission once the SDK is initialised and the Push
registration switch is on, as the Android sample does — after auto-start at launch, when valid demo credentials are injected (#233). A keyless build
does not request permission until initialization succeeds.

On iOS the device token is requested from APNs by that same authorization, so **the token only
reaches the SDK after the first successful initialisation**. A push sent to a fresh install
before initialization succeeds has no token to land on: configure the local demo build first, then send the
test push. On Android the FCM token is fetched at launch and cached, but the notification
permission (Android 13+) is asked at the same point as on iOS. If the iOS request cannot reach
the OS, the sample logs a warning and retries it on the next state change.

## QA automation

Android reads launch extras once at process startup. Use `am start -S` to stop the
previous process first; extras delivered to an already-running activity are not replayed.
The Editor and iOS do not read launch options. All QA markers come from sample code.

| Extra | ADB type | Effect |
| --- | --- | --- |
| `qaTab` | `--es` | Select `scenarios`, `community` or `settings` after shell readiness. |
| `qaScenario` | `--es` | Open a catalog id through the Scenarios list and pilot registry. Takes precedence over `qaTab`. |
| `qaPreset` | `--es` | With `qaScenario`, run the preset at its one-based position after binding (the displayed Preset number, including actions whose catalog id ends in `clear`). |
| `qaAutoStart` | `--ez` | Invoke Configuration's Apply once if explicitly opened; normal startup needs no QA extra. |

Configuration calls `OctopusSampleQaLaunch.ConfigShown(Apply)` when opened and
`ConfigStarted()` after successful Apply. Startup already auto-starts the default demo profile.
No configuration or credentials are supplied through launch extras. An explicit `qaPreset` launch fills and executes the same
SDK actions as taps.

```bash
# Each tab (replace scenarios with community or settings).
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaTab scenarios

# Open without running a preset.
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaScenario customEvents

# Open and run preset 1.
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaScenario customEvents --es qaPreset 1

# Apply an explicitly requested Configuration destination.
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaScenario config --ez qaAutoStart true

# A catalog entry without a registered pilot reports unknown scenario on this base.
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaScenario pushNotifications --es qaPreset 1 --ez qaAutoStart true

# Observe markers (start this in another terminal before launching).
adb logcat -v brief -s Unity | grep -F '[OctopusQA]'
```

Markers occupy one log line, with prefix `[OctopusQA]`. Result text has line breaks
collapsed into spaces; it is the displayed result, including `Running…` and stale
results after edits. Unchanged results and theme rebuilds do not emit duplicates.
Markers also describe manual tab selections and preset runs.

For `qaScenario=customEvents qaPreset=1`, a fresh process with valid sample
configuration produces these markers in order (other logs may be interleaved):

```text
[OctopusQA] launch-options=tab=none scenario=customEvents preset=1 autoStart=false
[OctopusQA] shell=ready
[OctopusQA] tab=scenarios
[OctopusQA] scenario=customEvents state=opened
[OctopusQA] scenario=customEvents result=Not run yet
[OctopusQA] scenario=customEvents preset=1 state=running
[OctopusQA] scenario=customEvents result=Running…
[OctopusQA] scenario=customEvents result=Tracked 'qa_sample_event' with 0 properties (mode: SSO).
```

The final result reports the pilot's actual outcome, which can instead be a missing
configuration or SDK error. `Tracked` means the SDK call returned, not server delivery.
Config Start emits `[OctopusQA] config=started` only when an actual Start succeeds.
Invalid input emits `[OctopusQA] launch-options=error <reason>`; unregistered
scenarios use `unknown scenario`, unavailable numbered presets use `unknown preset`.
Malformed options cancel the launch request. An unavailable preset opens its scenario
but makes no preset call. Launch summaries include only recognized, parsed QA values.

### Shell integration

A replacement shell implements `IOctopusSampleQaNavigation` and starts the
`OctopusSampleQaLaunch.ShellReady(this)` coroutine once built. `SelectTab` uses
its normal navigation, `IsTabReady` becomes true after the destination is mounted,
and `OpenScenario` uses the existing pilot registry and returns a bound
`IOctopusSampleQaScenario`. Its `RunPreset` fills values and executes explicitly; ordinary preset taps only fill.
Keep tab, opened, running and changed-result markers in the corresponding view
paths; preserve the single-consumption request and Config callbacks above.
