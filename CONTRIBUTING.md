# Contributing

This is the **private source** repository for the Octopus SDK for Unity. Host-facing
integration docs live in the [README](README.md); this file is for people working *on* the
package itself.

The public [`octopus-sdk-unity`](https://github.com/Octopus-Community/octopus-sdk-unity) mirror
is produced by archiving `main` with `git archive` (`Scripts/release-on-public-repo.sh`) — a
**deny-list**, not an allowlist: every path in this repo ships to the mirror unless
`.gitattributes` marks it `export-ignore`. Today that excludes `AndroidBridge/` (the Android
bridge Kotlin source — only the compiled `octopus-bridge.aar` under `UnityPackage/` ships),
`ci/`, `Scripts/`, `.claude/`, `docs/`, `TechnicalDocumentation.md`, `UserManual.md` and
`CLAUDE.md` itself. This file is **not** on that list and does ship, on the same footing as
`README.md`, `CHANGELOG.md` and `MIGRATING.md` — but development happens in *this* private repo,
so a few paths below (`ci/`, `AndroidBridge/`, `.claude/commands/...`) will not exist if you are
instead looking at the public mirror. Nothing in this repo may carry a secret, an API key or a
client name regardless of export-ignore status — `ci/mirror-export-guard/` checks the archive
itself, not the rules that produce it.

## Repository layout

```
UnityPackage/                 the published UPM package — the ONLY thing integrators get
  Runtime/                    OctopusSDK.Runtime.asmdef — public C# API + native bridges
  Editor/                     OctopusSDK.Editor.asmdef — build post-processing, theme UI
    ruby/                     vendored xcodeproj gem + patch_xcode_proj.rb (iOS SwiftPM wiring)
  Tests/Editor/               OctopusSDK.Tests.Editor.asmdef — EditMode test files
  Samples~/                   3 UPM samples, one project per sample
UnityExample/                 demo Unity project, references UnityPackage via a local path
AndroidBridge/                Gradle project producing octopus-bridge.aar (private only)
ci/                           local + CI gates (compile-check, native-pins, bridge-contract, mirror-export-guard)
Scripts/                      release tooling, incl. the mirror push
```

Full architecture, module boundaries and API-stability rules: [CLAUDE.md](CLAUDE.md).

## Toolchain

| Need | For | Version |
|---|---|---|
| Unity Editor | `UnityExample/`, EditMode tests, the legacy `.unitypackage` build | Read from [`UnityExample/ProjectSettings/ProjectVersion.txt`](UnityExample/ProjectSettings/ProjectVersion.txt) (currently `6000.3.3f1`) — do not hardcode a version, that file is the source of truth |
| .NET 8 SDK | `ci/compile-check/compile-check.sh` | `dotnet --version` should report an 8.x SDK; override the binary with `DOTNET=/path/to/dotnet` |
| JDK 11 | `AndroidBridge/build.sh` | `AndroidBridge/bridge/build.gradle.kts` pins `sourceCompatibility`/`targetCompatibility`/`jvmTarget` to `11` |
| Ruby | `UnityPackage/Editor/ruby/patch_xcode_proj.rb` (the iOS Xcode post-process) | Any recent system Ruby — the `xcodeproj` gem is vendored and committed under `UnityPackage/Editor/ruby/vendor/bundle`, so no `bundle install` is needed. This script runs automatically during an Xcode export (`iOSBuildPostProcessor.cs`); you only invoke it by hand if you are changing the script itself |
| `actionlint` | linting `.github/workflows/*.yml` | Installed via Homebrew (`brew install actionlint`) or the pinned release CI downloads (see `workflow-lint` job in `pr.yml`) |

No Unity licence is required for the compile gate, the native-pin check, the bridge-contract
check or the mirror-export guard — only for actually opening `UnityExample/` or running the
EditMode tests.

## Local gates, in the order `pr.yml` runs them

All of these are read from the scripts themselves — run them before opening a PR:

```bash
export PATH="$HOME/.dotnet:$PATH"        # or wherever your dotnet 8 SDK lives
ci/compile-check/compile-check.sh                        # Compile UnityPackage (dotnet vs Unity stubs) — REQUIRED context
ci/native-pins/verify-native-pins.sh                      # Native SDK pin coherence
ci/bridge-contract/check-bridge-contract.sh               # Bridge string contract (C# vs Kotlin vs Swift)
ci/mirror-export-guard/check-mirror-export.sh             # Mirror export guard (git archive) — REQUIRED context
actionlint .github/workflows/*.yml                        # Lint workflows (actionlint), reports only
```

What each one is actually protecting against — full rationale in each script's own header
comment, which is the documentation, not this list:

- **`compile-check.sh`** builds every asmdef (plus each sample under `UnityPackage/Samples~/`)
  as a plain `dotnet` project against hand-written Unity stubs, once per platform-define
  permutation, and prints the permutation count it ran — quote the printed total, never a
  remembered one. No Unity installation needed.
- **`verify-native-pins.sh`** checks that the Unity package version, the eight Android pin
  lines (resolver, bridge classpath, and the example's two generated copies) and the iOS
  SwiftPM pin all agree — `MAJOR.MINOR` locked across all three, `PATCH` free, package never
  leading the natives' minor.
- **`check-bridge-contract.sh`** compares the `On*` message names the C# side exposes against
  every literal Kotlin and Swift call into them by string — no compiler catches a rename on
  either side.
- **`check-mirror-export.sh`** unpacks `git archive HEAD` and fails if the export would carry a
  known secret file name or a credential-shaped string, checking the archive itself rather than
  the `.gitattributes` rules that produce it.

Run against this repo's own tree while writing this file: compile-check passed 18/18
permutations, native-pins and bridge-contract were both coherent, mirror-export reported the
export clean (no known secret name or credential-shaped string — the file count it prints
moves with every commit, so quote the one you see), and `actionlint` reported nothing.

**EditMode tests are not part of any of the gates above and are not run by CI** — no runner in
this org has a licensed Unity Editor. Run them yourself before touching logic they cover:

```bash
/Applications/Unity/Hub/Editor/<version-from-ProjectVersion.txt>/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath UnityExample \
  -runTests -testPlatform EditMode \
  -testResults UnityExample/TestResults-EditMode.xml \
  -logFile -
```

Do not add `-quit` — Unity exits on its own after `-runTests`, and combining the two can
truncate the run before results are written. State whether you ran them in the PR's test plan;
silence there reads as "verified" and is not.

### If you touched `AndroidBridge/`

Rebuild and commit the AAR in the **same PR** — it is a committed build output, and nothing in
CI can tell it went stale:

```bash
cd AndroidBridge && ./build.sh          # == ./gradlew :bridge:generate
```

This copies the result to `UnityPackage/Runtime/Android/octopus-bridge.aar`. Nothing automated
compiles `AndroidBridge/` on its own (no `src/test`, no CI job) — say in the PR body what you ran
to verify it, since no gate will.

### Bumping a native SDK pin

Never hand-edit a pin. `.claude/commands/bump-native-sdk.md` walks a coherent bump (Android
and/or iOS) end to end, including which files move together and what CI checks afterward.

## Sending a pull request

- Branch names follow the commit type: `type/description` (e.g. `docs/contributing`,
  `fix/theme-color-crash`), and releases use `release/vX.Y.Z`.
- Commits and PR titles are both **Conventional Commits**, `type(scope): description`, under 70
  characters — `pr-title-guard.yml` checks the title on every PR and re-checks on every edit.
  Scopes in use: `runtime`, `editor`, `bridge`, `ios`, `android`, `samples`, `ci`, `docs`.
- PR body: `## Summary` (bullets) + `## Test plan` (checklist) — see
  [`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md) for the exact shape
  this repo expects.
- PRs target `main`.
- A change to the public C# API needs a `CHANGELOG.md` entry under `## Unreleased`; a breaking
  one needs a `MIGRATING.md` section with before/after C# — integrators read those two files,
  not the PR. `version:` in `UnityPackage/package.json` is bumped only as part of a release, never
  in a feature PR.
- Discuss API changes before writing them — there is no binary-compatibility tool on this
  package, review is the only gate on the public surface.

## What never gets committed

Gitignored, and none of these should ever appear in a diff: `local.properties`, keystores,
`*.p12`, `*.pem`, `UnityExample/Assets/google-services.json` and
`UnityExample/Assets/Resources/OctopusExampleConfig.asset` (holds the sample's Octopus `apiKey`
and SSO `authToken`). The one documented exception is
`UnityExample/Assets/GoogleService-Info.plist`: it holds a real Firebase key, is committed
because the sample needs it to build, and is kept off the public mirror by `export-ignore`
alone — never copy its contents anywhere else.

## The public mirror push

`Scripts/release-on-public-repo.sh` is manual, has no dry-run flag and no CI: it archives
`main` (not your branch), wipes the public working tree and unpacks the archive over it. This
is an internal release process with a human checkpoint, not something a contributing PR
triggers.

## More context

- [README.md](README.md) — host-facing integration guide
- [CLAUDE.md](CLAUDE.md) — the same ground truth, written for agents (not on the public mirror)
- [CHANGELOG.md](CHANGELOG.md) · [MIGRATING.md](MIGRATING.md) — what integrators read
- [`.claude/rules/definition-of-done.md`](.claude/rules/definition-of-done.md) — the full,
  non-deferrable gate list (not on the public mirror)
- [`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md) — the PR checklist
