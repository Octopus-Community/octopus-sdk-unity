#import <UIKit/UIKit.h>
#include <string.h>

// Only linked into the sample; no SDK surface or dependency on the native Octopus SDK.
// No @try/@catch: Unity's Xcode export builds with Objective-C exceptions disabled, and none of
// these reads throws. A nil or unexpected value falls back on the C# side.

// 2 = dark, 1 = light, 0 = unknown (UIUserInterfaceStyle values).
extern "C" int OctopusSampleReadInterfaceStyle()
{
    __block NSInteger style = 0;
    void (^read)(void) = ^{
        style = UIScreen.mainScreen.traitCollection.userInterfaceStyle;
    };
    if (NSThread.isMainThread) read();
    else dispatch_sync(dispatch_get_main_queue(), read);
    return (int)style;
}

// CFBundleVersion, or NULL. The string is malloc'd: the IL2CPP marshaller frees it.
extern "C" const char* OctopusSampleReadBundleVersion()
{
    id value = [NSBundle.mainBundle objectForInfoDictionaryKey:@"CFBundleVersion"];
    if (![value isKindOfClass:[NSString class]] || [(NSString *)value length] == 0) return NULL;
    const char *utf8 = [(NSString *)value UTF8String];
    return utf8 == NULL ? NULL : strdup(utf8);
}
