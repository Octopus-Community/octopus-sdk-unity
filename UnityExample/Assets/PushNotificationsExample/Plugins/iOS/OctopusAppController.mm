#import "UnityAppController.h"
#import <UserNotifications/UserNotifications.h>
#import <objc/runtime.h>

// Auto-generated header that exposes @objc Swift classes (e.g. OctopusNotificationHelper)
// from the UnityFramework target.
#if __has_include(<UnityFramework/UnityFramework-Swift.h>)
#import <UnityFramework/UnityFramework-Swift.h>
#else
#import "UnityFramework-Swift.h"
#endif

// ─────────────────────────────────────────────────────────────────────────────
// OctopusNotificationSwizzler
//
// Unity's Mobile Notifications package (com.unity.mobile.notifications) sets
// UnityNotificationManager as the UNUserNotificationCenter delegate, overriding
// any delegate set by the app. Rather than fighting for delegate ownership, we
// swizzle UnityNotificationManager's didReceiveNotificationResponse at load time
// so Octopus is notified of every notification tap.
// ─────────────────────────────────────────────────────────────────────────────

/// Stores the original IMP of UnityNotificationManager's didReceiveNotificationResponse.
static void (*sOriginalDidReceiveResponse)(id, SEL, UNUserNotificationCenter *,
                                            UNNotificationResponse *, void (^)(void));

/// Replacement implementation that forwards to OctopusNotificationHelper first.
static void swizzled_didReceiveNotificationResponse(id self, SEL _cmd,
                                                     UNUserNotificationCenter *center,
                                                     UNNotificationResponse *response,
                                                     void (^completionHandler)(void))
{
    [OctopusNotificationHelper handleNotificationResponse:response];

    // Call the original Unity implementation.
    if (sOriginalDidReceiveResponse) {
        sOriginalDidReceiveResponse(self, _cmd, center, response, completionHandler);
    } else {
        completionHandler();
    }
}

@interface OctopusNotificationSwizzler : NSObject
@end

@implementation OctopusNotificationSwizzler

+ (void)load
{
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Class cls = NSClassFromString(@"UnityNotificationManager");
        if (!cls) return;

        SEL sel = @selector(userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:);
        Method method = class_getInstanceMethod(cls, sel);
        if (!method) return;

        sOriginalDidReceiveResponse = (void (*)(id, SEL, UNUserNotificationCenter *,
                                                 UNNotificationResponse *, void (^)(void)))
            method_getImplementation(method);
        method_setImplementation(method, (IMP)swizzled_didReceiveNotificationResponse);
    });
}

@end

// ─────────────────────────────────────────────────────────────────────────────
// OctopusAppController
//
// Registers as the app controller subclass via IMPL_APP_CONTROLLER_SUBCLASS.
// Handles foreground notification presentation (banner + sound) and serves as
// a fallback delegate for setups without com.unity.mobile.notifications.
// ─────────────────────────────────────────────────────────────────────────────

@interface OctopusAppController : UnityAppController <UNUserNotificationCenterDelegate>
@end

@implementation OctopusAppController

- (BOOL)application:(UIApplication *)application
    didFinishLaunchingWithOptions:(NSDictionary *)launchOptions
{
    BOOL result = [super application:application didFinishLaunchingWithOptions:launchOptions];
    [UNUserNotificationCenter currentNotificationCenter].delegate = self;
    return result;
}

#pragma mark - UNUserNotificationCenterDelegate

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
    didReceiveNotificationResponse:(UNNotificationResponse *)response
         withCompletionHandler:(void (^)(void))completionHandler
{
    [OctopusNotificationHelper handleNotificationResponse:response];
    completionHandler();
}

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
       willPresentNotification:(UNNotification *)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler
{
    // Show the notification as a banner with sound even when the app is in foreground.
    if (@available(iOS 14.0, *)) {
        completionHandler(UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionSound);
    } else {
        completionHandler(UNNotificationPresentationOptionAlert | UNNotificationPresentationOptionSound);
    }
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(OctopusAppController)
