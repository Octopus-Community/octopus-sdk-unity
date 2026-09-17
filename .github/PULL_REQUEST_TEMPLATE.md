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
Mandatory: run Scripts/unity-local-gates.sh [--android] locally and paste the
attestation line here, or write `not needed: <reason>` when the PR touches neither
UnityPackage/ nor UnityExample/. Use --android to verify the sample Android build.
CI checks the counts, editor version and commit; missing evidence fails for Unity changes. -->

Refs #
