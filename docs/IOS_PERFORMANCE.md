# iOS performance checks

The port keeps `Engine.TimeScale` at 1. The game's hand-card animation interpolates
position, scale and rotation with `delta * 7`, `delta * 8` and `delta * 10`, without
clamping the interpolation weight. The former forced 4x multiplier can make these
weights exceed 1 during frame drops, causing overshoot. A debug speed limit of 4
is not evidence that these UI animations remain stable at 4x. Use the game's own
Fast Mode setting to shorten supported animations.

Normal timing was installed and the user confirmed that cards animate, drag and
play again on an iPhone 15 Pro. Some visual hitches remain; this is not a claim
that all performance issues are fixed.

## Distinguish first-use work from sustained low frame rate

The first launch scans materials/scenes in `ShaderWarmupPatch`. Its completion
marker only means that routine finished. It does not prove every future effect
and graphics pipeline is cached. Godot 4.5 creates 2D canvas pipelines when they
are first drawn, and loading a resource alone does not exercise every effect.
See [Godot's pipeline compilation documentation](https://docs.godotengine.org/en/4.5/tutorials/performance/pipeline_compilations.html).

In the local normal-speed test, the menu settled near 60 FPS. Early combat had
isolated frame gaps of roughly 0.3–0.9 seconds; later repeated interactions had
smaller gaps. A subsequent 30-second CPU trace captured synchronous resource
loading in card reward screen initialization and some graphics pipeline creation.
The user still reports consistent hitches on repeated card plays. First-use
loading therefore does not explain the whole issue. That trace does not establish
the cause of every card-play hitch, and includes the overhead of profiling. Do not infer a sustained GPU bottleneck from one
isolated frame gap or change renderers solely on this evidence.

## Temporary diagnostics

Place a file named `mobile_diagnostics_on` containing `1` in the app's Documents
directory before starting the app. The patch records five-second wall-clock frame summaries,
rendering backend, time scale, and card-play mouse/touch press/release positions
to `mobile_diagnostics.log`. It rotates at about 1 MiB and retains one previous
log. The flag is checked once per process; change its contents to `0` (or remove
it) and restart to disable frame/touch diagnostics. These files remain local and
are not uploaded automatically. Use diagnostics only while investigating an
issue: synchronous input logging and profiling can affect frame pacing.

Suspension, screen locking and profiler overhead can produce apparent long
frames. Correlate timestamps with lifecycle messages and user actions. Average
FPS alone can hide a perceptible stall.

## Human verification

1. Enter combat and confirm the hand settles without cards leaving the screen.
2. Drag an attack onto an enemy, release, and verify energy, damage and card
   removal. Check a skill and cancelling a drag back over the hand too.
3. Compare first use of an effect with repeated use of the same card type over
   multiple turns; distinguish card animation stalls from combat/reward loading.
4. Check the next combat, then save, quit and resume. Preserve the app container
   when installing updates.

Build checks and save-tool tests do not establish animation smoothness or replace
these physical-device checks. Save migration is independent of frame pacing.
