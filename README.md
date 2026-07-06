# Octopus SDK for Unity

[![Release](https://img.shields.io/github/v/release/Octopus-Community/octopus-sdk-unity)](https://github.com/Octopus-Community/octopus-sdk-unity/releases)
[![Unity](https://img.shields.io/badge/Unity-2019.4%2B-222c37?logo=unity)](https://unity.com)
[![Platforms](https://img.shields.io/badge/platforms-Android%20%7C%20iOS-blue)](https://doc.octopuscommunity.com)

Octopus is an SDK that enables you to **integrate a fully customizable social network** into your Unity app, perfectly **aligned with your branding**.

**Minimum Unity version:** 2019.4
**Supported platforms:** Android, iOS

## Documentation

For complete integration guides and API reference, visit the [official documentation](https://doc.octopuscommunity.com).

## Installation

### Option 1 — Unity Package Manager (recommended)

Add the Octopus SDK and the External Dependency Manager to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.google.external-dependency-manager": "https://github.com/googlesamples/unity-jar-resolver.git?path=upm#v1.2.187",
    "com.octopuscommunity.octopus_sdk_for_unity": "https://github.com/Octopus-Community/octopus-sdk-unity.git?path=UnityPackage"
  }
}
```

### Option 2 — Legacy .unitypackage

1. Download the [External Dependency Manager](https://github.com/googlesamples/unity-jar-resolver/blob/master/external-dependency-manager-latest.unitypackage).
2. Download [OctopusCommunitySDK.unitypackage](https://raw.githubusercontent.com/Octopus-Community/octopus-sdk-unity/refs/heads/main/OctopusCommunitySDK.unitypackage).
3. Import both files into your project by dragging them into the Unity Editor.

## Quick Start

### Initialize the SDK

Call `Initialize` as early as possible (for example in a MonoBehaviour `Start` method). You will need an API key — see [Get an API Key](https://doc.octopuscommunity.com) for more info.

**SSO mode with app-managed profile fields:**

```csharp
OctopusSDK.Initialize("YOUR_API_KEY",
    ConnectionMode.SSO(
        ProfileField.NICKNAME
        // ,ProfileField.BIO
        // ,ProfileField.PICTURE
    )
);
```

**SSO mode without app-managed fields:**

```csharp
OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO());
```

### Connect a User

When using SSO, inform the SDK that your user is signed in:

```csharp
await OctopusSDK.ConnectUser(
    userId,
    nickname,
    bio,
    pictureUrl,
    GetToken
);

async Task<string> GetToken()
{
    // Contact your backend to obtain a signed JWT
    return "signed_jwt_from_your_backend";
}
```

When the user signs out:

```csharp
await OctopusSDK.DisconnectUser();
```

### Display the Community UI

Open the Octopus Community screen from any button or event:

```csharp
OctopusSDK.Open();
```

## Profile Management

The SDK supports two profile management modes depending on how you configure the `ConnectionMode`:

| Mode | Setup | Description |
|------|-------|-------------|
| **SSO, no managed fields** | `ConnectionMode.SSO()` | Your app handles authentication; users edit their profile inside the community. |
| **SSO, with managed fields** | `ConnectionMode.SSO(ProfileField.NICKNAME, ...)` | Your app owns specific profile fields and provides them via `ConnectUser`. |

When using app-managed fields, listen for edit requests from the community UI:

```csharp
OctopusSDK.OnModifyUser += (ProfileField? field) =>
{
    // The user tapped "edit" on a field your app manages.
    // Open your own profile editor here.
};
```

If your SSO setup requires forced login (contact us for this setting), listen for the login-required event:

```csharp
OctopusSDK.OnLoginRequired += () =>
{
    // The community UI needs the user to sign in.
    // Trigger your app's sign-in flow, then call ConnectUser.
};
```

For full details, see the [SSO documentation](https://doc.octopuscommunity.com/SDK/sso).

## Push Notifications

Octopus can send push notifications to your users when others interact with them in the community. The SDK does **not** request notification permissions — your app is responsible for that.

**Register the device push token:**

The native Octopus SDK expects a raw device token — APNs on iOS, FCM on Android:

```csharp
OctopusSDK.RegisterNotificationsToken(deviceToken);
```

> As of 1.12, push handling is **data-driven and identical on iOS and Android** — there is no native `.mm` file to add and no `OnNotificationTapped` event. On both platforms you detect the tap, read its payload, and pass it to the SDK:
>
> ```csharp
> if (OctopusSDK.IsOctopusNotification(payload))
>     OctopusSDK.Open(OctopusSDK.GetOctopusNotification(payload));
> ```
>
> `payload` is an `IDictionary<string, string>`: the FCM data map on Android, the notification's `UserInfo` on iOS.

### iOS — Notification Handling

iOS uses Unity Mobile Notifications (`com.unity.mobile.notifications`) for both APNs token retrieval and tap detection. **No Firebase dependency and no native file are required** — the previous `OctopusAppController.mm` and method swizzling are no longer needed.

Request authorization and register the APNs token:

```csharp
using (var req = new AuthorizationRequest(
    AuthorizationOption.Alert | AuthorizationOption.Sound | AuthorizationOption.Badge,
    registerForRemoteNotifications: true))
{
    while (!req.IsFinished) yield return null;
    if (req.Granted && !string.IsNullOrEmpty(req.DeviceToken))
        OctopusSDK.RegisterNotificationsToken(req.DeviceToken);
}
```

Forward the payload's `UserInfo` to the SDK. A **tap** is exposed via `GetLastRespondedNotification()` — check it on cold start **and** when the app regains focus (a tap on a backgrounded notification resumes the app without raising `OnRemoteNotificationReceived`). `OnRemoteNotificationReceived` fires only when a notification *arrives* in the foreground, not on a tap:

```csharp
void HandleTappedPayload(IDictionary<string, string> payload)
{
    if (OctopusSDK.IsOctopusNotification(payload))
        OctopusSDK.Open(OctopusSDK.GetOctopusNotification(payload));
}

// A tap — the notification that launched the app (cold start) or was tapped while backgrounded.
void HandleRespondedNotification()
{
    var responded = iOSNotificationCenter.GetLastRespondedNotification();
    if (responded != null) HandleTappedPayload(responded.UserInfo);
}

void Start() => HandleRespondedNotification();                                  // cold start
void OnApplicationFocus(bool hasFocus) { if (hasFocus) HandleRespondedNotification(); } // resume after a tap

// A notification arriving while the app is already in the foreground:
iOSNotificationCenter.OnRemoteNotificationReceived += n => HandleTappedPayload(n.UserInfo);
```

> The responded notification must be read from whichever scene loads first — if your app launches into a menu, handle it there (or in a persistent object), not only inside the community screen. The sample includes a small `PushNotificationLauncher` that routes a tap to the example scene for exactly this reason.

A ready-to-use version of this flow is available in the **Push Notifications Example** sample (importable via Unity Package Manager).

### Android — Firebase Setup

Add a `google-services.json` for your Firebase project to `Assets/` in your Unity project. Create the file from the [Firebase Console](https://console.firebase.google.com) by adding an **Android** app with your package name (e.g. `com.octopuscommunity.example`).

### Android — Notification Handling

On Android, use Firebase Messaging (or your push provider) to detect notification taps and pass the deep link to the SDK:

```csharp
if (OctopusSDK.IsOctopusNotification(e.Message.Data) && e.Message.NotificationOpened)
{
    var notification = OctopusSDK.GetOctopusNotification(e.Message.Data);
    OctopusSDK.Open(notification);
}
```

No additional native file is needed on Android.

> As on iOS, attach the `MessageReceived` listener from whichever scene loads **first** (or a persistent object) so a tap that launches a closed app is handled — not only after the user navigates into the community screen. The sample's `PushNotificationLauncher` reads the launch intent's extras and routes a tap to the example scene to cover this.

## Groups

List groups, synchronize follow/unfollow choices, react to changes, and open a specific group's feed:

```csharp
// Fetch the available groups
OctopusSDK.FetchGroups(
    onCompleted: groups => { /* OctopusGroup: Id, Name, IsFollowed, CanChangeFollowStatus */ },
    onError: msg => Debug.LogError(msg));

// Batch-sync follow/unfollow choices
var actions = new List<OctopusSyncFollowGroupAction>
{
    new OctopusSyncFollowGroupAction { GroupId = groupId, Followed = true, ActionDate = DateTime.UtcNow },
};
OctopusSDK.SyncFollowGroups(
    actions,
    onCompleted: results => { /* OctopusSyncFollowGroupResult: GroupId, Status (OctopusSyncFollowGroupStatus) */ },
    onError: msg => Debug.LogError(msg));

// React to changes
OctopusSDK.OnGroupsChanged += groups => { /* ... */ };

// Open a specific group's feed
OctopusSDK.OpenGroup(groupId);

// Open a specific post's detail screen (empty string → main feed)
OctopusSDK.OpenPost("post_123");

// Open the post editor — pass null for a blank editor, or prefill it:
OctopusSDK.OpenCreatePost(new OctopusPrefilledPost {
    Text      = "Check this out!",
    TopicId   = "grp_42",
    ImagePath = Application.persistentDataPath + "/shot.png"
});
```

A ready-to-use example is available in the **Groups Example** sample (importable via Unity Package Manager).

## Notification Badges

Display a badge in your app to let users know they have unseen community notifications:

```csharp
OctopusSDK.OnNotSeenNotificationsCount += (int count) =>
{
    // Update your badge UI with 'count'
};

// Request the latest count at any time:
OctopusSDK.UpdateNotSeenNotificationsCount();
```

## Theme Customization

### Option 1 — Unity Editor (no code required)

Open **Octopus SDK > Theme Configuration** from the Unity menu bar. The editor window lets you pick colors, logos, and fonts. Assets configured here are automatically embedded as native resources at build time.

### Option 2 — Runtime API

Set the theme from code (all parameters are optional):

```csharp
OctopusSDK.SetTheme(
    colorScheme: new OctopusColorScheme(
        primary:     new Color32(255, 0, 0, 255),
        primaryLow:  new Color32(255, 179, 179, 255),
        primaryHigh: new Color32(204, 0, 0, 255),
        onPrimary:   new Color32(255, 255, 255, 255)
    ),
    logo: new OctopusLogo(
        androidDrawableName: "my_logo",
        iOSResourceName: "Data/Raw/my_logo.png"
    )
);
```

> **Note:** When using the runtime API for logos and fonts, you must add the native resources (Android drawables, iOS bundle resources) to your project manually. The Unity Editor approach handles this automatically.

For more detailed theming documentation, visit the [official documentation](https://doc.octopuscommunity.com).

## Locale Override

Override the SDK's display language:

```csharp
OctopusSDK.OverrideDefaultLocale("fr");
```

## Analytics

### Custom Events

Track custom events to enrich the analytics provided by Octopus:

```csharp
OctopusSDK.Track("Purchase", new Dictionary<string, string>
{
    { "price", "1.99" },
    { "currency", "EUR" },
    { "product_id", "product1" }
});
```

### Community Visibility

If only a subset of your users can access the community, inform the SDK so analytics reflect this accurately. Call this after every initialization:

```csharp
OctopusSDK.TrackAccessToCommunity(true);
```

## Sample

The package includes a sample project. Import it from the Unity Package Manager window under **Octopus SDK for Unity > Samples**.
You will need an API Key to run the sample

## Support

- [Official documentation](https://doc.octopuscommunity.com)
- [GitHub Issues](https://github.com/Octopus-Community/octopus-sdk-unity/issues)
