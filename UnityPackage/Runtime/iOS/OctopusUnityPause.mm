// Pauses/resumes the Unity player loop the same way Unity's own trampoline does when the app
// backgrounds (UnityAppController.mm: UnityWillPause -> repaint -> UnityPause(1)), so the game
// receives OnApplicationPause(true/false) and OnApplicationFocus — matching Android, where opening
// the Octopus Activity backgrounds Unity. Idempotent. Driven by the Octopus view controller's
// appear/disappear lifecycle (see OctopusHostingController in OctopusSwiftPlugin.swift).
#import <UIKit/UIKit.h>
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

}
