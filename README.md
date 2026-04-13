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

### iOS — Notification Tap Handling

On iOS, notification taps are handled natively by your `OctopusAppController.mm` (see below). The SDK fires `OnNotificationTapped` for every Octopus notification tap, regardless of app state (cold start, background, or foreground):

```csharp
OctopusSDK.OnNotificationTapped += () =>
{
    OctopusSDK.Open(); // navigates to the notification's content
};
```

No Firebase dependency is needed on iOS. You can retrieve the APNs device token using Unity Mobile Notifications (`com.unity.mobile.notifications`) or any other method.

> **How it works:** The native `UNNotificationResponse` is intercepted via method swizzling on `UnityNotificationManager` and forwarded to `OctopusNotificationHelper`. This avoids a delegate-ownership conflict with Unity's Mobile Notifications package, which sets its own `UNUserNotificationCenter` delegate. The SDK notifies your C# code through `OnNotificationTapped` so you can call `Open()`.

#### iOS — Native File Required

Notification deep-link navigation requires a native Objective-C++ file that hooks into `UnityNotificationManager`. Without it, `Open()` will open the community home feed instead of the specific content.

Add `OctopusAppController.mm` to your project under `Assets/Plugins/iOS/`:

```objc
#import "UnityAppController.h"
#import <UserNotifications/UserNotifications.h>
#import <objc/runtime.h>
#if __has_include(<UnityFramework/UnityFramework-Swift.h>)
#import <UnityFramework/UnityFramework-Swift.h>
#else
#import "UnityFramework-Swift.h"
#endif

// Swizzle UnityNotificationManager so Octopus is notified of every notification
// tap, regardless of who owns the UNUserNotificationCenter delegate.

static void (*sOriginalDidReceiveResponse)(id, SEL, UNUserNotificationCenter *,
                                            UNNotificationResponse *, void (^)(void));

static void swizzled_didReceiveNotificationResponse(id self, SEL _cmd,
        UNUserNotificationCenter *center, UNNotificationResponse *response,
        void (^completionHandler)(void)) {
    [OctopusNotificationHelper handleNotificationResponse:response];
    if (sOriginalDidReceiveResponse)
        sOriginalDidReceiveResponse(self, _cmd, center, response, completionHandler);
    else
        completionHandler();
}

@interface OctopusNotificationSwizzler : NSObject @end
@implementation OctopusNotificationSwizzler
+ (void)load {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Class cls = NSClassFromString(@"UnityNotificationManager");
        if (!cls) return;
        SEL sel = @selector(userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:);
        Method m = class_getInstanceMethod(cls, sel);
        if (!m) return;
        sOriginalDidReceiveResponse = (void (*)(id, SEL, UNUserNotificationCenter *,
            UNNotificationResponse *, void (^)(void)))method_getImplementation(m);
        method_setImplementation(m, (IMP)swizzled_didReceiveNotificationResponse);
    });
}
@end

@interface OctopusAppController : UnityAppController <UNUserNotificationCenterDelegate>
@end

@implementation OctopusAppController

- (BOOL)application:(UIApplication *)application
    didFinishLaunchingWithOptions:(NSDictionary *)launchOptions {
    BOOL result = [super application:application didFinishLaunchingWithOptions:launchOptions];
    [UNUserNotificationCenter currentNotificationCenter].delegate = self;
    return result;
}

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
    didReceiveNotificationResponse:(UNNotificationResponse *)response
         withCompletionHandler:(void (^)(void))completionHandler {
    [OctopusNotificationHelper handleNotificationResponse:response];
    completionHandler();
}

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
       willPresentNotification:(UNNotification *)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler {
    if (@available(iOS 14.0, *)) {
        completionHandler(UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionSound);
    } else {
        completionHandler(UNNotificationPresentationOptionAlert | UNNotificationPresentationOptionSound);
    }
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(OctopusAppController)
```

> **Note:** `OctopusNotificationSwizzler` hooks into Unity's `UnityNotificationManager` at load time so notification taps are forwarded to Octopus even though `com.unity.mobile.notifications` owns the delegate. `OctopusAppController` serves as a fallback for projects that do not use that package.

A ready-to-use version of this file is available in the **Push Notifications Example** sample (importable via Unity Package Manager).

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
