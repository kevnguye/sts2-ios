# Agent setup and maintenance instructions

Read [docs/BEGINNER_GUIDE.md](docs/BEGINNER_GUIDE.md),
[docs/NATIVE_LIBRARIES.md](docs/NATIVE_LIBRARIES.md),
[docs/PUBLICATION.md](docs/PUBLICATION.md), and
[docs/IOS_PERFORMANCE.md](docs/IOS_PERFORMANCE.md) first. The beginner guide ends
with a portable setup prompt for coding assistants.

## Boundaries

- Use only the user's locally owned Steam game files and legitimately obtained
  dependencies. Do not download or publish a compiled game, IPA or content pack.
- Keep originals intact. Work with ignored references and generated outputs.
- Never uninstall the app to update it: its container holds saves and settings.
  Re-sign/install over the same bundle ID and team. Back up saves before changes.
- Signing values belong only in ignored `ios-export/config.sh` and generated
  local files. Do not guess Team IDs; certificate suffixes can differ from them.
- Let the user complete authentication, legal agreements and device trust prompts.
- Do not weaken validations or alter tests to manufacture success. Report failed
  checks and verification limits honestly.

## Reproducible Mac route

1. Verify Xcode/iOS SDK, .NET 9, Godot 4.5.1 .NET/mono and matching export templates.
2. Verify the owned macOS Steam app and `data_sts2_macos_arm64` assemblies.
3. Prepare the locally licensed native dependencies using the pinned recipe.
   FMOD uses a **static device XCFramework**, plus its two SDK `.a` files. The
   export host also loads matching macOS libraries from the owned Steam app.
4. Fill the ignored config template with the user's actual values. Keep save
   synchronization opt-ins at zero unless explicitly requested.
5. Run `bash ios-export/build-ios.sh`. It copies ignored game references,
   compiles/weaves, publishes NativeAOT, prepares a private bootstrap export
   driver in `.work/`, exports/finalizes Xcode and patches the local game pack.
6. Open `ios-export/build/StS2.xcodeproj`. Prefer a connected Xcode MCP when
   available. Set automatic Development signing with the user's team, confirm
   Increased Memory Limit and choose the physical iPhone (not a simulator).
7. Build/sign/install with the same bundle ID, then verify first launch, touch
   play, sound, save/resume and any changed settings on the phone. Do not equate
   compilation with complete runtime compatibility.

The documented simple route installs the full content pack inside the app.
Slim deployment is an optional advanced route requiring BOTH `deploy-slim.sh`
and `push-pck.sh` in the documented order; do not silently mix the two layouts.

## Runtime behavior

Keep engine time at 1x. Forced 4x can make the game's unclamped hand-card
interpolation overshoot on slow frames. Use the game's Fast Mode for supported
animation shortening. Shader warmup does not prove every future effect is cached.

Run Save Slots, Card Suggestions and Restart Room default **off**. They are
independent native controls under Settings → General → Extras, with local
persistence. Disabled advisors must not score, write diagnostic snapshots or run
timers; disabled snapshots must not modify/delete slots. Normal autosaves stay
separate. Preserve other profiles and previous-tree recovery in save operations.

## Verification and publication

Run the relevant commands in `tests/README.md`. Include practical human checks
in any PR. Build/SDK/tool outputs stay local, including the one-shot device UI
verification report and screenshot. Confirm the working tree, staged diff and
Git history contain no excluded files before pushing:

```bash
python3 tools/check_publication.py --history
```

Check actual signing/profile expiration rather than assuming every account needs
weekly renewal. Free Personal Team provisioning normally expires after 7 days;
other development teams can differ. Do not publish the user's certificate data.
