# Octopus SDK for Unity

[![Latest release](https://img.shields.io/github/v/release/Octopus-Community/octopus-sdk-unity?label=release)](https://github.com/Octopus-Community/octopus-sdk-unity/releases/latest)
[![Unity](https://img.shields.io/badge/Unity-2019.4%2B-black?logo=unity)](https://unity.com/releases/editor/archive)
[![Platforms](https://img.shields.io/badge/platforms-Android%20%7C%20iOS-blue)](#requirements)
[![License](https://img.shields.io/badge/license-Octopus%20SDK%20License-lightgrey)](LICENSE.md)

Add a moderated, white-label community to your Android and iOS Unity game or app: native UI,
hosted backend, a few lines of C#.

<img src="https://raw.githubusercontent.com/Octopus-Community/octopus-sdk-android/main/docs/images/singleactivity.png" width="280" alt="The Octopus community feed, as opened by OctopusSDK.Open() (Android shown)">

## What you get

- **Native screens, no webview.** The package wraps the Octopus Android and iOS SDKs;
  `OctopusSDK.Open()` presents their native community UI over your Unity player.
- **Your brand.** Colors (light and dark), logo and fonts, set without code in the Editor
  (**Octopus SDK > Theme Configuration**) or at runtime with `OctopusSDK.SetTheme`.
- **Your accounts.** SSO: your backend signs a JWT and your players join with the identity they
  already have. You choose which profile fields (nickname, bio, picture) your app owns.
- **Hosted by Octopus.** Backend, storage, moderation, back office and analytics are run by
  Octopus. On your side, SSO needs one backend route that signs user tokens.
- **A C# API for the rest.** Push notifications, unseen-notification badge count, groups,
  opening a given post or group, custom analytics events. In the Editor, Play mode runs against
  a built-in [mock backend](https://doc.octopuscommunity.com/SDK/sso/unity-editor-mock-mode), so you can
  iterate without a device build.

## Requirements

| | Minimum |
|---|---|
| Unity | 2019.4 |
| Android | API 21 |
| iOS | 13.0 |
| Dependency | [External Dependency Manager for Unity](https://github.com/googlesamples/unity-jar-resolver) (resolves the native Android SDK) |
| Native SDKs in 1.13.0 | Android 1.13.4, iOS 1.13.2 (added as a Swift package at Xcode export) |

> **Known issue: Xcode 27 / iOS 27.** A Unity 6000.3 iOS export is refused at launch on iOS 27
> (a Unity trampoline limitation, not an SDK bug). Use Unity 6000.5+ and re-export.
> [Details](docs/integration-guide.md#known-issue-xcode-27-and-ios-27)

## Installation

**Unity Package Manager (recommended).** Add both entries to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.google.external-dependency-manager": "https://github.com/googlesamples/unity-jar-resolver.git?path=upm#v1.2.187",
    "com.octopuscommunity.octopus_sdk_for_unity": "https://github.com/Octopus-Community/octopus-sdk-unity.git?path=UnityPackage#v1.13.0"
  }
}
```

Replace `v1.13.0` with the [latest release](https://github.com/Octopus-Community/octopus-sdk-unity/releases/latest)
tag. Without the `#v…` suffix the URL tracks this repo's `main` branch.

**Legacy `.unitypackage`.** Import the
[External Dependency Manager](https://github.com/googlesamples/unity-jar-resolver/blob/master/external-dependency-manager-latest.unitypackage)
and [OctopusCommunitySDK.unitypackage](https://raw.githubusercontent.com/Octopus-Community/octopus-sdk-unity/refs/heads/main/OctopusCommunitySDK.unitypackage)
by dragging both into the Unity Editor.

## Quickstart

```csharp
using System.Threading.Tasks;
using UnityEngine;

public class Community : MonoBehaviour
{
    void Start() => OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO());

    // Call once your player is signed in to your app.
    public async void OnPlayerSignedIn(string userId, string nickname) =>
        await OctopusSDK.ConnectUser(userId, nickname, null, null, FetchOctopusToken);

    // Wire this to your "Community" button.
    public void OnCommunityButton() => OctopusSDK.Open();

    // Return the JWT your backend signs for this player (see the SSO guide below).
    static Task<string> FetchOctopusToken() => Task.FromResult("SIGNED_JWT_FROM_YOUR_BACKEND");
}
```

The token provider can run on a background thread while the game loop is paused: fetch with
`System.Net.Http`, not `UnityWebRequest`. Backend side:
[SSO setup](https://doc.octopuscommunity.com/SDK/sso) and
[JWT generation](https://doc.octopuscommunity.com/backend/jwt/generate_jwt).

## Sample app

- **Package samples**: in the Package Manager window, select **Octopus SDK for Unity >
  Samples** and import *Octopus Auth Example*, *Push Notifications Example* or *Groups Example*.
- **`UnityExample`**, the full demo project in this repo:

  ```bash
  git clone https://github.com/Octopus-Community/octopus-sdk-unity.git
  ```

  Open `UnityExample/` in Unity Hub, create **Assets > Create > Octopus Example Config** and
  fill in `apiKey`. [Full configuration](docs/integration-guide.md#sample-configuration-unityexample).

Both need an API key. Request a sandbox community through the form on
[octopuscommunity.com](https://www.octopuscommunity.com); the key arrives within 24 hours.

## Links

- [Integration guide](docs/integration-guide.md): connection modes, push notifications (iOS and
  Android), groups, badges, theming, locale, analytics and community access control
- [Documentation](https://doc.octopuscommunity.com) · [Changelog](CHANGELOG.md) ·
  [Migration guide](MIGRATING.md)
- Other Octopus SDKs: [Android](https://github.com/Octopus-Community/octopus-sdk-android) ·
  [iOS](https://github.com/Octopus-Community/octopus-sdk-swift) ·
  [Flutter](https://github.com/Octopus-Community/octopus-sdk-flutter) ·
  [React Native](https://github.com/Octopus-Community/octopus-sdk-react-native)
- Support: [GitHub Issues](https://github.com/Octopus-Community/octopus-sdk-unity/issues)

## License

The SDK is distributed under the
[Octopus Community Mobile SDK License Agreement](LICENSE.md).
