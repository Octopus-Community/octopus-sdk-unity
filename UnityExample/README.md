# Unity sample

## QA automation

Android reads launch extras once at process startup. Use `am start -S` to stop the
previous process first; extras delivered to an already-running activity are not replayed.
The Editor and iOS do not read launch options. All QA markers come from sample code.

| Extra | ADB type | Effect |
| --- | --- | --- |
| `qaTab` | `--es` | Select `home`, `scenarios`, `community` or `settings` after shell readiness. |
| `qaScenario` | `--es` | Open a catalog id through the Scenarios list and pilot registry. Takes precedence over `qaTab`. |
| `qaPreset` | `--es` | With `qaScenario`, run the preset at its one-based position after binding (the displayed Preset number, including actions whose catalog id ends in `clear`). |
| `qaAutoStart` | `--ez` | Invoke the Config screen's existing Start action once, using its persisted configuration, if that screen is shown. |

This base has no Config screen: `qaAutoStart` currently has no effect. A future
Config screen must call `OctopusSampleQaLaunch.ConfigShown(startAction)` after
loading its configuration, and `ConfigStarted()` from its successful Start path.
No configuration or credentials are supplied through launch extras. Use a QA-only
persisted configuration when running presets; they execute the same SDK actions as taps.

```bash
# Each tab (replace home with scenarios, community or settings).
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaTab home

# Open without running a preset.
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaScenario customEvents

# Open and run preset 1.
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --es qaScenario customEvents --es qaPreset 1

# Start a Config screen if present; currently a no-op on this base.
adb shell am start -S -n com.octopuscommunity.sdk.unity.sample/com.google.firebase.MessagingUnityPlayerActivity --ez qaAutoStart true

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
[OctopusQA] scenario=customEvents result=Ready — no SDK call made yet. Tap a preset to run one.
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
`IOctopusSampleQaScenario`. Its `RunPreset` must use the same path as preset taps.
Keep tab, opened, running and changed-result markers in the corresponding view
paths; preserve the single-consumption request and Config callbacks above.
