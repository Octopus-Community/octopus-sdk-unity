// Pauses/resumes the Unity player loop the same way Unity's own trampoline does when the app
// backgrounds (UnityAppController.mm: UnityWillPause -> repaint -> UnityPause(1)), so the game
// receives OnApplicationPause(true/false) and OnApplicationFocus — matching Android, where opening
// the Octopus Activity backgrounds Unity. Idempotent. Driven by the Octopus view controller's
// appear/disappear lifecycle (see OctopusHostingController in OctopusSwiftPlugin.swift).
#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import "UnityAppController.h"
#import "UnityAppController+Rendering.h"  // declares -[UnityAppController repaint]
#import "UnityInterface.h"

extern "C" {

void OctopusUnityPause(void) {
    if (UnityIsPaused()) return;                       // idempotent
    UnityWillPause();                                  // schedule OnApplicationPause(true)
    UnityAppController *controller = GetAppController();
    // Flush the scheduled message through one player-loop tick before stopping the loop. Guard the
    // Metal-display-link case exactly as the trampoline does (it cannot repaint without a drawable).
    if (controller != nil && ![controller unityUsesMetalDisplayLink]) {
        [controller repaint];
    }
    UnityPause(1);                                     // stop the loop
}

void OctopusUnityResume(void) {
    if (!UnityIsPaused()) return;                      // idempotent
    UnityWillResume();                                 // schedule OnApplicationPause(false)
    UnityPause(0);                                     // resume; the message delivers on the next tick
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
