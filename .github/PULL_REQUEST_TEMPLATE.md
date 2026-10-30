## Summary

<!-- What does this PR change, and why? 1-3 bullets. -->

-

## Test plan

<!-- How did you verify this? Check what applies. -->

- [ ] `AndroidBridge` compiles (`./gradlew :bridge:assembleRelease`)
- [ ] Tested on a device (Android / iOS) if behaviour changed
- [ ] `UnityExample` still runs (if the sample is affected)
- [ ] `CHANGELOG.md` updated under `## Unreleased` (if user-visible)

### Unity local gates

<!-- Private repo only — on the public mirror, leave this section as is.
Mandatory: run Scripts/unity-local-gates.sh [--android] locally — or dispatch the
`Unity editor gates` workflow on your branch — and paste the attestation line here, or write `not needed: <reason>` when the PR touches
neither UnityPackage/ nor UnityExample/.
CI checks the counts, editor version and commit; missing evidence fails for Unity changes. -->

### Native sources

<!-- Private repo only — on the public mirror, leave this section as is.
Required when the PR sets, changes or clears native-dev-sources.json, or touches AndroidBridge/
while a manifest exists: the manifest state (or "cleared"), then what you ran and its result —
`cd AndroidBridge && ./build.sh` with the AAR committed, and an iOS export resolved and built in
Xcode — with `env_out="$(ci/native-dev/native-dev.sh prepare)" && eval "$env_out"` while the manifest exists, with no
OCTOPUS_* variable set on the clearing PR. Otherwise write `not needed`. -->

Refs #
