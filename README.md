# Octopus SDK for Unity

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

> As of 1.12, **tap** handling is data-driven and identical on iOS and Android — there is no `OnNotificationTapped` event. On both platforms you detect the tap, read its payload, and pass it to the SDK. **Displaying** the notification differs: on iOS the OS renders the alert for you, while on Android Octopus messages are **data-only** and your app must post the notification itself (see [Android — Notification Handling](#android--notification-handling)).
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

Unlike iOS, Octopus push notifications reach Android as **data-only** FCM messages: neither the OS nor Firebase displays them for you, even with the app in the background. Your app is responsible for **displaying** the notification, then for handling the **tap**. Unity's C# code is paused while the app is backgrounded, so the display step must run natively.

The **Push Notifications Example** sample ships both halves ready to use — import it via Unity Package Manager and keep the two Android files (they land in the host project, not in the package, so you can edit them freely):

| File | Role |
|---|---|
| `Plugins/Android/OctopusMessagingService.java` | Extends the Firebase Unity plugin's own messaging service (`com.google.firebase.messaging.cpp.ListenerService`, shipped in `firebase-messaging-cpp.aar`). It posts the notification on an `octopus-sdk` channel (app icon, `title`/`body` from the payload), attaches the FCM data map to the tap intent, then calls `super` so `FirebaseMessaging.MessageReceived` / `TokenReceived` keep firing in C#. Framework APIs only — no Kotlin, no androidx, no extra Gradle dependency. |
| `Plugins/Android/OctopusPushSample.androidlib/` | Registers that service in the merged manifest on `com.google.firebase.MESSAGING_EVENT` with `android:priority="1"` (the Firebase Unity plugin's own service sits at 0), so ours is the one FCM binds. |

**1. Display (background included).** Nothing to write: the service handles messages carrying the `is_octopus_notification` marker and ignores your own payloads (add your handling next to `showOctopusNotification` if you want it to display them too). Two things worth adjusting for production:

- **Small icon.** The service uses the app icon (`getApplicationInfo().icon`); an adaptive launcher icon renders as a grey square in the status bar. Ship a monochrome `drawable` and reference it in `showOctopusNotification`.
- **Placement.** Unity only recognises `.androidlib` folders under `Assets/**/Plugins/Android/`. The sample imports there by default; if the manifest is not merged (the service never receives messages), move `OctopusPushSample.androidlib` and the `.java` file to `Assets/Plugins/Android/`.

**2. Tap.** A tap launches (or resumes) your activity with the payload in the intent extras. `PushNotificationsExample.cs` reads the launch intent in `Start()` and `OnApplicationFocus()`, so the payload reaches `HandleTappedPayload` whether the app was cold-started or resumed; it also still listens to `FirebaseMessaging.MessageReceived` with `NotificationOpened == true`, which the Firebase Unity plugin raises for the same tap, and dedupes the two by message id:

```csharp
IDictionary<string, string> payload = ReadLaunchIntentExtras(); // see HandleAndroidLaunchIntent in the sample
if (OctopusSDK.IsOctopusNotification(payload))
    OctopusSDK.Open(OctopusSDK.GetOctopusNotification(payload));
```

Read the extras from whichever scene loads **first** (or a persistent object) — if your app launches into a menu, handle it there, not only inside the community screen.

**Writing your own service instead.** If you already have a native `FirebaseMessagingService`, keep it and drop the sample's two Android files; the [native Android sample's MessagingService](https://github.com/Octopus-Community/octopus-sdk-android/blob/main/samples/src/main/java/com/octopuscommunity/sample/messaging/MessagingService.kt) is the Kotlin reference. Two Unity-specific points: Unity's exported Gradle project has no Kotlin plugin by default (Java, or a prebuilt AAR), and only one service receives `MESSAGING_EVENT` — so extend `com.google.firebase.messaging.cpp.ListenerService` and call `super.onMessageReceived` / `super.onNewToken` if you still want Firebase's C# events. Forwarding the intent with `startService()` does **not** work: `FirebaseMessagingService` ignores the delivered intent.

**Foreground messages.** When a message arrives while the app is in the foreground, the service displays it the same way (there is no foreground check — add one in `onMessageReceived` if you prefer in-app UI), and `Firebase.Messaging.FirebaseMessaging.MessageReceived` still fires in C# thanks to the `super` call — `OctopusSDK.GetOctopusNotification(e.Message.Data)` gives you the title and body.

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

### Community Access Control (Octopus A/B testing)

`TrackAccessToCommunity` above is analytics-only. To actually control and observe access:

```csharp
// Observe whether the current user can access community features
// (respects Octopus' A/B config and any override you set).
OctopusSDK.OnHasAccessToCommunityChanged += hasAccess => communityButton.SetActive(hasAccess);
bool current = OctopusSDK.HasAccessToCommunity;

// Let Octopus control access directly — takes full precedence over A/B config and TrackAccessToCommunity.
OctopusSDK.OverrideCommunityAccess(true,
    onCompleted: () => Debug.Log("granted"),
    onError: err => Debug.LogWarning(err));
```

## Sample

The package includes a sample project. Import it from the Unity Package Manager window under **Octopus SDK for Unity > Samples**.
You will need an API Key to run the sample

## Support

- [Official documentation](https://doc.octopuscommunity.com)
- [GitHub Issues](https://github.com/Octopus-Community/octopus-sdk-unity/issues)
