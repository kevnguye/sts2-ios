# Audio and screen-margin update

This update is prepared for physical-device verification. Compilation and the
synthetic GUI tests do not confirm that audio is audible on an iPhone or that
all game interactions work correctly with nonzero margins.

## Sound

The native iOS bootstrap configures a Playback audio session, activates it on
launch/foreground and reactivates it after an interruption when iOS allows resume.
Playback permits sound with Silent Mode enabled; Mix With Others preserves other
apps' audio. The system chooses the output route, including headphones and Bluetooth.
Game volume settings still control game audio. No microphone access or background
audio entitlement is added. Existing game background muting remains in place.

This addresses an unconfigured or inactive audio session as a possible cause of
silence. The reported no-sound issue still requires confirmation on the phone.
Audio banks are present in the local full-content pack; none are published here.
See [Apple's Playback category](https://developer.apple.com/documentation/avfaudio/avaudiosession/category-swift.struct/playback)
and [FMOD's iOS session guidance](https://www.fmod.com/docs/2.03/api/platforms-ios.html).

## Screen margins

Open **Settings → Graphics** and find **Horizontal Screen Margin** and
**Vertical Screen Margin** above Reset Graphics. These reuse the game's volume
slider row and percentage label, with explanations on hover/long press where
supported by the native control. Each slider ranges from **0% to 15% per side**.

Start with Vertical at 5% and Horizontal at 0%, then adjust to taste. Horizontal
insets the left/right edges; Vertical insets the top/bottom. The axes resize the
canvas independently. Values apply immediately and persist locally across launches.
Set both to **0%** to restore the original frame. These controls have their own
preference file; they do not change master volume or the UI Scale preference.

The margin transform applies across 2D canvas layers. Godot's final viewport
transform maps normal input and emulated mouse positions back into logical game
coordinates. No extra remapping is applied to the game's card-drag polling.
Margins move the hand away from the bottom edge; they do **not disable iOS Home gestures**.

## Human verification

1. Install over the existing app with the same bundle ID. Do not uninstall.
2. Check menu music, menu clicks, combat music, card sounds and volume sliders.
   Repeat with Silent Mode on/off, then headphones/Bluetooth if available.
3. Background/resume the app; interrupt it with another audio app or Siri and
   return. Check sound resumes and does not continue while the game is backgrounded.
4. In Graphics, move each margin slider separately. Confirm the displayed value,
   immediate inset and explanations, then reopen Settings/relaunch for persistence.
5. At 0%, 5% and 15%, drag cards to enemies, cancel a drag, scroll, select rewards
   and use Settings near all four edges. Check there is no pointer/target offset.
6. Change UI Scale and rotate between landscape orientations. Confirm margins
   remain centered and inputs aligned. Reset both sliders to 0% to restore full frame.
7. Verify master volume and the three Extras preferences were not changed by
   operating either margin slider. Observe whether the raised hand reduces
   accidental Home gestures during normal play.
