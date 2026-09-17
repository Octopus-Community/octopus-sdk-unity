#import <UIKit/UIKit.h>

// Only linked into the sample; no SDK surface or dependency on the native Octopus SDK.
extern "C" float OctopusSampleReadTextScale()
{
    __block CGFloat ratio = 1.0;
    void (^read)(void) = ^{
        UIContentSizeCategory category = UIApplication.sharedApplication.preferredContentSizeCategory;
        UITraitCollection *traits = [UITraitCollection traitCollectionWithPreferredContentSizeCategory:category];
        ratio = [[UIFontMetrics metricsForTextStyle:UIFontTextStyleBody]
            scaledValueForValue:17.0 compatibleWithTraitCollection:traits] / 17.0;
    };
    if (NSThread.isMainThread) read();
    else dispatch_sync(dispatch_get_main_queue(), read);
    return (float)ratio;
}
