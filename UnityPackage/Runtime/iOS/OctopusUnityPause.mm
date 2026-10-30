// Pauses/resumes the Unity player loop the same way Unity's own trampoline does when the app
// backgrounds (UnityAppController.mm, applicationWillResignActive / applicationDidBecomeActive), so
// the game receives OnApplicationPause(true/false) and OnApplicationFocus — matching Android, where
// opening the Octopus Activity backgrounds Unity. Idempotent. Driven by the Octopus view
// controller's appear/disappear lifecycle (see OctopusHostingController in OctopusSwiftPlugin.swift).
#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#include <atomic>
#import "UnityAppController.h"
#import "UnityAppController+Rendering.h"  // declares -[UnityAppController repaint]
#import "UnityInterface.h"

// Unity 6000.5 moved the pause entry points out of UnityInterface.h: UnityWillPause(),
// UnityWillResume() and UnityIsPaused() are no longer declared there, UnityPause(int) is deprecated,
// and the trampoline drives everything through UnitySetPlayerPause(mode, flags), declared in
// UnityInternalInterface.h — a header that did not exist before 6000.5. The generated Xcode project
// exposes no editor-version macro to plugin sources, so the presence of that header is the feature
// test (both spellings, since Classes/ and Classes/Unity/ are both on the header search path).
#if __has_include("UnityInternalInterface.h")
#import "UnityInternalInterface.h"
#define OCTOPUS_UNITY_HAS_SET_PLAYER_PAUSE 1
#elif __has_include("Unity/UnityInternalInterface.h")
#import "Unity/UnityInternalInterface.h"
#define OCTOPUS_UNITY_HAS_SET_PLAYER_PAUSE 1
#else
#define OCTOPUS_UNITY_HAS_SET_PLAYER_PAUSE 0
#endif

// Set once the app starts terminating, and never cleared. Unity's applicationWillTerminate: runs
// UnityCleanup() and then UnityCleanupTrampoline(), which sets the window's rootViewController to
// nil: that dismisses a still-presented Octopus controller synchronously, and its viewDidDisappear
// would resume a player that no longer exists (EXC_BAD_ACCESS in
// BackgroundJobQueue::CreateMainThreadGroup). The flag is raised by the applicationWillTerminate:
// swizzle below, before Unity's own implementation runs, so it does not depend on whether UIKit
// posts UIApplicationWillTerminateNotification before or after calling the delegate.
static BOOL gOctopusAppTerminating = NO;

// Unequal generations mean Unity posted kUnityDidUnload since the last completed embedded start.
// Atomics also cover notifications posted from an engine thread. A new unload during startup must
// not be cleared by that startup's return. Unlike the terminating flag, this guard is resettable.
static std::atomic<NSUInteger> gOctopusUnityUnloadGeneration(0);
static std::atomic<NSUInteger> gOctopusUnityReloadedGeneration(0);

typedef void (*OctopusUnityRunEmbeddedIMP)(id, SEL, int, char **, NSDictionary *);
static OctopusUnityRunEmbeddedIMP gOctopusUnityRunEmbedded = NULL;

static void OctopusUnityRunEmbedded(id framework, SEL selector, int argc, char **argv,
    NSDictionary *appLaunchOpts) {
    NSUInteger generation = gOctopusUnityUnloadGeneration.load();
    gOctopusUnityRunEmbedded(framework, selector, argc, argv, appLaunchOpts);
    // In 6000.3/6000.5/6000.6 main.mm, the reload branch calls the engine's scene-load entry point,
    // unpauses Unity and shows its window before returning, without restoring AppReady. Use that
    // return as the reset signal; the scene-load implementation itself is in the closed engine.
    if (gOctopusUnityUnloadGeneration.load() == generation) {
        gOctopusUnityReloadedGeneration.store(generation);
    }
}

static BOOL OctopusUnityCanPauseOrResume(void) {
    if (gOctopusAppTerminating) return NO;
    if (gOctopusUnityUnloadGeneration.load() != gOctopusUnityReloadedGeneration.load()) return NO;
    UnityAppController *controller = GetAppController();
    if (controller == nil) return NO;

    // The inspected 6000.3/6000.5/6000.6 trampolines set RenderingInitialized (3) on unload
    // and do not raise it in their embedded reload branch. The notification guard distinguishes them;
    // the engine-state check only rejects values below RenderingInitialized (quit/teardown).
    // We assume the closed engine calls UnityEngineDidQuit (which posts the notification) before
    // UnityCleanupTrampoline dismisses our controller; the trampoline cannot prove that ordering.
    // Older trampolines (including the 2019.4 floor) may not expose this property: preserve their
    // engine-state fallback without requiring the newer property or enum at compile time.
    if (![controller respondsToSelector:NSSelectorFromString(@"engineLoadState")]) return YES;
    @try {
        id state = [controller valueForKey:@"engineLoadState"];
        const NSInteger renderingInitialized = 3;
        return [state isKindOfClass:[NSNumber class]] && [state integerValue] >= renderingInitialized;
    } @catch (NSException *exception) {
        // A controller exposing the selector but rejecting KVC must not touch an unknown player.
        return NO;
    }
}

extern "C" {

void OctopusUnityPause(void) {
    if (!OctopusUnityCanPauseOrResume()) return;       // no player while terminating or unloaded
    if (UnityIsPaused()) return;                       // idempotent
    UnityAppController *controller = GetAppController();
    // Flush the scheduled OnApplicationPause(true) through one player-loop tick before stopping the
    // loop. Guard the Metal-display-link case exactly as the trampoline does (it cannot repaint
    // without a drawable).
    BOOL canRepaint = controller != nil && ![controller unityUsesMetalDisplayLink];
#if OCTOPUS_UNITY_HAS_SET_PLAYER_PAUSE
    if (canRepaint) {
        // Schedule the message, deliver it inside the repaint, then stop the loop — the 6000.5
        // trampoline's own sequence in applicationWillResignActive.
        UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSchedulePauseMessage);
        [controller repaint];
        if (!OctopusUnityCanPauseOrResume()) return;   // the tick may have unloaded or quit Unity
        UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSetEngineRunState);
    } else {
        // No tick available to carry a scheduled message: send it right away, as the trampoline does.
        UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSetEngineRunState | kPauseFlagSendPauseMessage);
    }
#else
    UnityWillPause();                                  // schedule OnApplicationPause(true)
    if (canRepaint) {
        [controller repaint];
    }
    if (!OctopusUnityCanPauseOrResume()) return;       // the tick may have unloaded or quit Unity
    UnityPause(1);                                     // stop the loop
#endif
}

void OctopusUnityResume(void) {
    if (!OctopusUnityCanPauseOrResume()) return;       // no player while terminating or unloaded
    if (!UnityIsPaused()) return;                      // idempotent
#if OCTOPUS_UNITY_HAS_SET_PLAYER_PAUSE
    // Resume the loop with OnApplicationPause(false) scheduled for the next tick — what the 6000.5
    // trampoline does in applicationDidBecomeActive.
    UnitySetPlayerPause(kUnityPauseModeResume, kPauseFlagSetEngineRunState | kPauseFlagSchedulePauseMessage);
#else
    UnityWillResume();                                 // schedule OnApplicationPause(false)
    UnityPause(0);                                     // resume; the message delivers on the next tick
#endif
}

// Orientation forcing (set from Swift around present/dismiss of the Octopus UI). 0 = not forcing;
// otherwise the raw UIInterfaceOrientationMask to inject while the community is shown.
static NSUInteger gOctopusForcedMask = 0;
void OctopusSetForcedOrientationMask(NSUInteger mask) { gOctopusForcedMask = mask; }

}

// Unity's UnityAppController implements application:supportedInterfaceOrientationsForWindow: and
// returns the Player-Settings mask (e.g. landscape for a landscape game). That app-level mask
// overrides the Info.plist and is intersected with the top VC's supportedInterfaceOrientations, so a
// portrait Octopus controller has no common orientation with it and UIKit asserts. Swizzle the
// method to WIDEN the mask (union) with the forced orientation while the community is up — never
// replace it, or Unity's still-frontmost game VC loses its own orientation and UIKit asserts on it.
// Each VC then constrains itself: the game VC keeps landscape, the Octopus VC takes the forced one.
@interface UnityAppController (OctopusOrientation)
@end

@implementation UnityAppController (OctopusOrientation)

- (NSUInteger)octopus_application:(UIApplication *)application
    supportedInterfaceOrientationsForWindow:(UIWindow *)window {
    // After the exchange this selector points at Unity's original implementation.
    NSUInteger original = [self octopus_application:application supportedInterfaceOrientationsForWindow:window];
    if (gOctopusForcedMask != 0) {
        return original | gOctopusForcedMask;
    }
    return original;
}

+ (void)load {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Class cls = [UnityAppController class];
        SEL origSel = @selector(application:supportedInterfaceOrientationsForWindow:);
        SEL swizSel = @selector(octopus_application:supportedInterfaceOrientationsForWindow:);
        Method origM = class_getInstanceMethod(cls, origSel);
        Method swizM = class_getInstanceMethod(cls, swizSel);
        if (origM != NULL && swizM != NULL) {
            method_exchangeImplementations(origM, swizM);
        } else {
            // No app-delegate method to widen: the app mask comes from the Info.plist instead, which
            // the build post-processor already patches to include the forced orientation.
            NSLog(@"[Octopus SDK] supportedInterfaceOrientationsForWindow: not found on UnityAppController "
                  @"(origM=%p swizM=%p); relying on Info.plist for forced orientation", origM, swizM);
        }
    });
}

@end

// UnityFramework is located at runtime so no newer framework header is required by this plugin.
// The category's class object lives for the process lifetime and observes notifications directly,
// without dispatching asynchronously past a possible synchronous controller dismissal.
@interface UnityAppController (OctopusLibraryLifecycle)
+ (void)octopus_unityDidUnload:(NSNotification *)notification;
@end

@implementation UnityAppController (OctopusLibraryLifecycle)

+ (void)octopus_unityDidUnload:(NSNotification *)notification {
    gOctopusUnityUnloadGeneration.fetch_add(1);
}

+ (void)load {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Class frameworkClass = NSClassFromString(@"UnityFramework");
        SEL selector = NSSelectorFromString(@"runEmbeddedWithArgc:argv:appLaunchOpts:");
        Method method = class_getInstanceMethod(frameworkClass, selector);
        if (method != NULL) {
            gOctopusUnityRunEmbedded = (OctopusUnityRunEmbeddedIMP)method_setImplementation(
                method, (IMP)OctopusUnityRunEmbedded);
        } else {
            NSLog(@"[Octopus SDK] UnityFramework embedded startup method not found; "
                  @"the unload guard cannot reset on reload");
        }
        // LifeCycleListener.mm defines kUnityDidUnload as this literal in all three trampolines.
        // Use the literal to avoid requiring that exported symbol from older engine versions.
        [NSNotificationCenter.defaultCenter addObserver:self selector:@selector(octopus_unityDidUnload:)
            name:@"kUnityDidUnload" object:nil];
    });
}

@end

// Raises gOctopusAppTerminating (see above) before Unity's applicationWillTerminate: tears the
// engine down. A subclass installed with IMPL_APP_CONTROLLER_SUBCLASS reaches this through super;
// one that skips super bypasses this flag. Its own cleanup is guarded only if the trampoline
// posts the unload notification or lowers engineLoadState below RenderingInitialized before
// dismissing our controller.
@interface UnityAppController (OctopusTermination)
@end

@implementation UnityAppController (OctopusTermination)

- (void)octopus_applicationWillTerminate:(UIApplication *)application {
    gOctopusAppTerminating = YES;
    // After the exchange this selector points at Unity's original implementation.
    [self octopus_applicationWillTerminate:application];
}

+ (void)load {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Class cls = [UnityAppController class];
        Method origM = class_getInstanceMethod(cls, @selector(applicationWillTerminate:));
        Method swizM = class_getInstanceMethod(cls, @selector(octopus_applicationWillTerminate:));
        if (origM != NULL && swizM != NULL) {
            method_exchangeImplementations(origM, swizM);
        } else {
            NSLog(@"[Octopus SDK] applicationWillTerminate: not found on UnityAppController "
                  @"(origM=%p swizM=%p); the Unity loop is not guarded against resume on termination",
                  origM, swizM);
        }
    });
}

@end
