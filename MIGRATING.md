# Migrating

This guide consolidates the migration notes for releases of the Octopus SDK
for Unity that contain breaking changes. Each section lists the breaking
changes first, then how to adapt. The most recent version is at the top.
Full release notes live in [CHANGELOG.md](CHANGELOG.md).

- [To 1.16.0 (from 1.15.x)](#to-1160-from-115x)
- [To 1.15.0 (from 1.14.x) — SSO-only authentication](#to-1150-from-114x--sso-only-authentication)
- [To 1.12.8 (from 1.12.7)](#to-1128-from-1127)
- [To 1.12.0 (from 1.10.x)](#to-1120-from-110x)

---

## To 1.16.0 (from 1.15.x)

### Build requirements — compile SDK 36

The native Android SDK 1.16 compiles against Android 16 (API 36). Android host apps must compile
against API 36 or later and build with Android Gradle Plugin (AGP) 8.9.1 or later, the
[minimum AGP for API 36](https://developer.android.com/build/releases/about-agp#api-level-support).
The minimum Android version an app runs on stays API 23. No C# change is required.

Unity compiles the Android project against the **Target API Level** of
*Player Settings > Android > Other Settings*:

**Before:** Target API Level 34 or 35.

**After:** Target API Level 36 or later — or *Automatic (highest installed)* with the Android 16
(API 36) SDK platform installed.

The Target API Level also becomes the app's `targetSdkVersion`, so this opts the app into the
[Android 16 behaviour changes for apps targeting it](https://developer.android.com/about/versions/16/behavior-changes-16):
among them, on screens 600dp wide or more the system ignores orientation and resizability
restrictions, and apps can no longer opt out of edge-to-edge. Test a locked-orientation game on a
tablet before shipping. To keep your current target SDK, leave the Target API Level as it is and
set `compileSdk 36` in a custom Gradle template (`mainTemplate.gradle` and
`launcherTemplate.gradle`, enabled under *Player Settings > Publishing Settings > Build*) in place
of the template's `compileSdk **APIVERSION**`.

The AGP version comes with the Unity Editor. Unity's Gradle version compatibility tables list these
Editors with a bundled AGP older than 8.9.1:

| Unity Editor | Bundled AGP |
|---|---|
| 6000.1 | 8.7.2 |
| 6000.0.45f1 – 6000.0.60f1 | 8.7.2 |
| 6000.0.1f1 – 6000.0.44f1 | 8.3.0 |
| 2022.3 (2022.3.38f1 and later) and 2021.3 (2021.3.41f1 and later) | 7.4.2 |
| Earlier 2022.3 and 2021.3 patches | 7.1.2 or older |
| 2020.3 and 2019.4 | the manual lists Gradle 6.1.1 or older only |

Unity 6000.0.61f1 and later 6.0 patches, 6.2, 6.3 (from 6000.3.1f1), 6.4, 6.5 and 6.6 bundle AGP
8.10.0 or later and need no change here. For any other Editor, read the *Gradle version compatibility* page
of its own manual. On an Editor with an older AGP, raise it in a custom base Gradle template
(`baseProjectTemplate.gradle`, enabled under *Player Settings > Publishing Settings > Build*).
AGP 8.9 needs Gradle 8.11.1 or later and JDK 17
([AGP 8.9 release notes](https://developer.android.com/build/releases/past-releases/agp-8-9-0-release-notes)):
if the Editor bundles older ones, point *Preferences > External Tools* at a matching Gradle and
JDK. A build on these older Editors with a raised AGP has not been verified.

### Several photos per post

Not breaking: a post now carries up to 10 photos (`OctopusPrefilledPost.MaxImageCount`), and the
single-image fields keep working with an `Obsolete` warning. Move to the list forms when you touch
the code.

`OctopusPrefilledPost` takes `ImagePaths`, in display order, instead of `ImagePath`:

**Before:**

```csharp
OctopusSDK.OpenCreatePost(new OctopusPrefilledPost { Text = text, ImagePath = photoPath });
```

**After:**

```csharp
OctopusSDK.OpenCreatePost(new OctopusPrefilledPost { Text = text, ImagePaths = { photoPath } });
```

Set one or the other: `ImagePath` together with a non-empty `ImagePaths` makes `OpenCreatePost`
throw an `ArgumentException`. More than 10 images throw an
`OctopusPrefilledPostTooManyImagesException`, which is an `ArgumentException` too; catch it first
when you want to tell it apart. Nothing opens in either case:

```csharp
try
{
    OctopusSDK.OpenCreatePost(new OctopusPrefilledPost { Text = text, ImagePaths = photoPaths });
}
catch (OctopusPrefilledPostTooManyImagesException e)
{
    // e.Count photos were passed, at most e.Max are accepted
}
```

`OctopusClientObject` takes `Medias` instead of `ImagePath` or `ImageUrl`:

**Before:**

```csharp
var post = new OctopusClientObject
{
    ObjectId = article.Id, Text = article.Title, ImageUrl = article.ImageUrl
};
```

**After:**

```csharp
var post = new OctopusClientObject
{
    ObjectId = article.Id, Text = article.Title,
    Medias =
    {
        OctopusClientObjectMedia.FromUrl(article.ImageUrl),
        OctopusClientObjectMedia.FromPath(thumbnailPath)
    }
};
```

Do not combine them: `FetchOrCreateClientObjectRelatedPost` then fails with
`OctopusClientPostErrorCode.Other`, before reaching the native SDK. More than 10 medias are refused
the same way.

### Button labels per language

Not breaking: the label of a Bridge Share CTA and of a bridge post button can now be given per
language. `OctopusPrefilledPost.CtaLabel` and `OctopusClientObject.ViewObjectButtonText` remain
public fields with an `Obsolete` warning. Octopus automatically translates a non-empty legacy
label when the corresponding new field is null.

**Before:**

```csharp
var prefilled = new OctopusPrefilledPost { Text = text, CtaLabel = "Visit our shop", CtaUrl = shopUrl };
var post = new OctopusClientObject { ObjectId = "article-1", Text = text, ViewObjectButtonText = "Read article" };
```

**After** (same behaviour):

```csharp
var prefilled = new OctopusPrefilledPost
{
    Text = text, CtaUrl = shopUrl,
    CtaLocalizedLabel = OctopusLocalizedText.AutoTranslated("Visit our shop")
};
var post = new OctopusClientObject
{
    ObjectId = "article-1", Text = text,
    ViewObjectButton = OctopusLocalizedText.AutoTranslated("Read article")
};
```

**Or, per language:**

```csharp
var label = OctopusLocalizedText.PerLanguage(new Dictionary<string, string>
{
    { "en", "Read article" },
    { "fr", "Lire l'article" },
    { "pt-BR", "Ler o artigo" }
}, "en");
```

Give BCP-47 language tags. `PerLanguage` does not check them: the native SDK validates the texts when
the label is used. A bridge post button it refuses fails `FetchOrCreateClientObjectRelatedPost` with
`OctopusClientPostErrorCode.InvalidLocalizedText` (`LocalizedTextErrorKind` says why); a CTA label it
refuses is logged and the CTA left out. The labels of a bridge post are taken when it is created: an
existing post keeps its button.

---

## To 1.15.0 (from 1.14.x) — SSO-only authentication

Your app now owns sign-in, aligned with the native SDKs' removal of Magic Link authentication.
`ConnectionMode.OctopusAuth()` and `OctopusSDK.SwitchCommunityOctopusAuth` are removed,
without compatibility shims. The mode parameter of `Initialize` and `SwitchCommunity` was
already required in Unity and remains so.

**Before:**

```csharp
OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.OctopusAuth());
OctopusSDK.SwitchCommunityOctopusAuth("YOUR_OTHER_API_KEY", onCompleted, onError);
```

**After:**

```csharp
OctopusSDK.OnLoginRequired += OpenAppSignIn;
OctopusSDK.Initialize("YOUR_API_KEY", ConnectionMode.SSO());
// Once your own sign-in succeeds, supply your backend's fresh SSO JWT on demand:
await OctopusSDK.ConnectUser(userId, nickname, bio, picture, FetchTokenFromBackend);
// After switching, connect the host user again in onCompleted:
OctopusSDK.SwitchCommunity("YOUR_OTHER_API_KEY", ConnectionMode.SSO(), onCompleted, onError);
```

`OpenAppSignIn` opens your app's sign-in UI. `FetchTokenFromBackend` is your
`Func<Task<string>>` that obtains a fresh client-signed token from your backend; it must work
without the Unity player loop. Call `DisconnectUser` when your app signs the user out.
Use `ConnectionMode.SSO(ProfileField.NICKNAME, ...)` if your app manages profile fields.

`RefreshEntitlements` returns `NoClientTokenProvider` until `ConnectUser` registers a provider
in this SDK session, including when the native SDK restored a profile after relaunch. Register
the provider again after initialization, a community switch, or Stop followed by initialization.
A native stored session is not logged out at launch by this change.

`OctopusScreen.SettingsList`, `SettingsAccount`, `DeleteAccount`, and the settings account,
logout and delete-warning `OctopusIconSlot` values remain available with warning-level
`Obsolete` attributes. Their numeric values and parsing are unchanged. Handle accounts in your
app and customize `SettingsHelp` / `SettingsInfo`; Unity has no Settings icon initializer to replace.

The auth-only scene and UPM sample are removed. Remaining examples initialize with SSO;
connect your own user for authenticated actions. Generic legacy UnityExample scenes route
`OnLoginRequired` to the SSO sign-in scene. The shell already uses SSO configuration and
Account screens. Its saved selections accept only the existing SSO profiles; invalid selections
return to Configuration.

Native dependencies now target 1.15.0 on Android and iOS. These releases remove native auth
repositories and native account/logout/delete screens, and clean up pending Magic Link state.
The wrapper neither accesses private native storage nor forces a logout of a stored session.
Localization stays owned by the native SDKs.

---

## To 1.12.8 (from 1.12.7)

This release drops the `SettingsAbout` screen event. The "About the community"
screen no longer exists in the native SDKs — Android removed it in 1.13.0 and
iOS in 1.13.0 — and both native pins are now past those versions, so no
platform emits the event any more. It is **removed rather than deprecated**, to
stay iso with the Android and iOS SDKs, which removed it outright.

### Breaking — `OctopusScreen.SettingsAbout` removed

The enum member is gone from `OctopusScreen`. This is source-breaking for any
code that names it — typically a `case OctopusScreen.SettingsAbout:` branch in a
`switch`, whether or not that `switch` has a `default:`. Nothing changes at
runtime, because no native build in the supported range emits it. A `"SettingsAbout"` token arriving from an
older native build parses as `OctopusScreen.Unknown` through the SDK's
unknown-token fallback, so a stale native cannot throw in your event handler.
`OctopusScreen` has implicit ordinals, so the members after the removed one
shift down by one: compare and persist screens by name (`ToString()`), never
by their integer value — the wire protocol itself is name-based.

**Before (1.12.x):**

```csharp
OctopusSDK.OnOctopusEvent += OnOctopusEvent;

void OnOctopusEvent(OctopusEvent e)
{
    var evt = e as ScreenDisplayedEvent;
    if (evt == null) return;

    switch (evt.Screen)
    {
        case OctopusScreen.SettingsList:    TrackScreen("octopus_settings"); break;
        case OctopusScreen.SettingsAbout:   TrackScreen("octopus_about");    break;
        case OctopusScreen.SettingsAccount: TrackScreen("octopus_account");  break;
    }
}
```

**After:**

```csharp
OctopusSDK.OnOctopusEvent += OnOctopusEvent;

void OnOctopusEvent(OctopusEvent e)
{
    var evt = e as ScreenDisplayedEvent;
    if (evt == null) return;

    switch (evt.Screen)
    {
        case OctopusScreen.SettingsList:    TrackScreen("octopus_settings"); break;
        case OctopusScreen.SettingsAccount: TrackScreen("octopus_account");  break;
        // A default branch handles the screens this build does not list —
        // including ones added by a later native SDK — instead of letting them
        // fall through unhandled. It does not protect a reference to a removed
        // member: a `case` naming one must go, default or not.
        default:                                                             break;
    }
}
```

Full release notes live in [CHANGELOG.md](CHANGELOG.md).

---

## To 1.12.0 (from 1.10.x)

1.12.0 moved iOS push notifications to the data-driven native 1.11 API. Push
handling is now symmetric on iOS and Android: detect the notification tap with
your push library (e.g. Unity Mobile Notifications, Firebase Messaging), then
hand the payload to the SDK.

### Breaking — iOS-only `OctopusSDK.OnNotificationTapped` event removed

The SDK no longer detects notification taps itself on iOS. Detect the tap with
your push library on both platforms, then open the SDK with the payload.

**Before (1.10.x, iOS only):**

```csharp
// The native layer (OctopusAppController.mm) captured the tap itself.
OctopusSDK.OnNotificationTapped += () =>
{
    OctopusSDK.Open();
};
```

**After (1.12.0, both platforms):**

```csharp
// In your push library's notification-tap callback:
if (OctopusSDK.IsOctopusNotification(payload))
{
    OctopusSDK.Open(OctopusSDK.GetOctopusNotification(payload));
}
```

See the `PushNotificationsExample` sample for a complete, working wiring on
both platforms.

### Breaking — `OctopusAppController.mm` no longer required

The native `UnityAppController` subclass (`OctopusAppController.mm`) and its
method swizzling are no longer used. Remove the file from your Xcode project
(or your `Assets/Plugins/iOS` folder) when upgrading — leaving it in place is
unnecessary.

### Breaking — notification payload parsing accepts both platform shapes

`OctopusSDK.IsOctopusNotification(payload)` and
`OctopusSDK.GetOctopusNotification(payload)` now accept either:

- a **flat** key/value payload (Android FCM data message), or
- the **iOS** `UserInfo` dictionary, where the Octopus keys are a JSON string
  under the `data` key.

Pass the raw payload from your push library directly — the SDK unwraps the
right shape itself. If you previously pre-extracted the iOS `data` envelope
before calling these methods, remove that code and pass the full `UserInfo`
dictionary instead.
