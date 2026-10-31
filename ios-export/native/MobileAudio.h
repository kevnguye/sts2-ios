// Included once by the generated Objective-C++ bootstrap. No game/SDK code.
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

static void sts2_activate_audio() {
    AVAudioSession *session = [AVAudioSession sharedInstance];
    NSError *error = nil;
    // Play game audio in Silent Mode, preserve music from other apps, and let
    // iOS choose the output route (including headphones/Bluetooth).
    if (![session setCategory:AVAudioSessionCategoryPlayback
                        mode:AVAudioSessionModeDefault
                     options:AVAudioSessionCategoryOptionMixWithOthers error:&error]) {
        NSLog(@"[StS2 audio] category failed: %@", error);
        return;
    }
    if (![session setActive:YES error:&error])
        NSLog(@"[StS2 audio] activation failed: %@", error);
    else
        NSLog(@"[StS2 audio] playback session active (%ld output channels)", (long)session.outputNumberOfChannels);
}

__attribute__((constructor)) static void sts2_register_audio() {
    @autoreleasepool {
        NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
        [center addObserverForName:UIApplicationDidFinishLaunchingNotification object:nil
            queue:[NSOperationQueue mainQueue] usingBlock:^(NSNotification *) { sts2_activate_audio(); }];
        [center addObserverForName:UIApplicationDidBecomeActiveNotification object:nil
            queue:[NSOperationQueue mainQueue] usingBlock:^(NSNotification *) { sts2_activate_audio(); }];
        [center addObserverForName:AVAudioSessionInterruptionNotification object:nil
            queue:[NSOperationQueue mainQueue] usingBlock:^(NSNotification *note) {
                if ([note.userInfo[AVAudioSessionInterruptionTypeKey] unsignedIntegerValue] == AVAudioSessionInterruptionTypeEnded &&
                    ([note.userInfo[AVAudioSessionInterruptionOptionKey] unsignedIntegerValue] & AVAudioSessionInterruptionOptionShouldResume) &&
                    [UIApplication sharedApplication].applicationState == UIApplicationStateActive)
                    sts2_activate_audio();
            }];
    }
}
