# Native acceptance: b7e07802

Exact main revision: `b7e07802ce7189b701fc8bbe11be8648046acec3`. Native execution used Windows and Unity **6000.6.5f1**. This accepts the original cabinet, concrete architecture, safety paint, objective guidance and expanded mission replay candidate. It is not final city artwork, separate-PC LAN, ten-player, performance or manual-input acceptance.

## Compile and complete native suites

The guarded fast-forward preserved all 23 protected hashes. An initial **305-test result was from stale assemblies** and was retained separately, not counted as this candidate's acceptance. Explicit existing-Editor recompilation succeeded, and fresh discovery listed 340 EditMode cases.

- **340/340 EditMode**, 42.77 seconds, zero failures/skips/inconclusive. `Logs/acceptance-b7e078-editmode-340-response.json`: 65,717 bytes, SHA-256 `7d9fa70ecae1613795541d426bdfc07f086b5680bc8fa93a7569357c8bf8dfc8`.
- **94/94 PlayMode**, 132.41 seconds, zero failures/skips/inconclusive. `Logs/acceptance-b7e078-playmode-94-response.json`: 18,614 bytes, SHA-256 `b10a8854fce9d599c1d6a50f17f1485358e8cae404c4104dd9af80fed510c7bf`.

The rendered architecture, cabinet and paint cases actually ran; they were not counted as skipped graphics checks. Python was unavailable in this local execution environment, so no local Python pass is claimed.

## One Windows rebuild

Fresh `UO_LAN_BUILD_OK` console sequence 2140 at **2026-11-08T23:25:27.1811956Z** confirmed success. Managed fingerprints:

- Core.dll: 48,128 bytes, SHA-256 `bc81dc48d0fb23ac581fff2537082395ae9251890018b9d3589843085662bcf6`.
- LanPrototype.dll: 102,400 bytes, `62ca4fe15af3cdc64547e6ea2557be5ff4483ddd28e613622d2046398f1bec2a`.
- Prototype.dll: 30,208 bytes, `4847e0901055db60797ef41f568b7e299a597305c59a00d11e5e63fceb6ac0f5`.

The unchanged Unity EXE stub was not used alone as freshness evidence. The Editor console reported zero errors and 66 warnings, including captured ConvGeneric shader notices; this was not a warning-free build claim.

## Actual two-process loopback

Both scenarios used one host and one client on the **same PC**, bound to loopback. No firewall or security settings were changed. All four host/client reports have `success=true`, `runtimeErrorCount=0`, `warningCount=0`, empty `firstRuntimeError` and `error`. Native log messages are reported separately below.

### Default range

Run `bc3689ade64b4b5394a5d728189a39f5`, port 51211, host PID 27456/client PID 30644, both exit 0. UTC **2026-11-08T23:26:38.9705718Z–23:27:50.4111511Z**. All **29 host /21 client** required markers passed, including movement, combat/reload, rejoin/reset, infected, revive, three waves and shutdown.

- `Builds/LanRange/SmokeResults/bc3689ade64b4b5394a5d728189a39f5/summary.json`: 1,460 bytes, SHA-256 `fa5ae06b735676b1eec1211d0a7fc1f434c4ffcd2fc2f1fcadde9be8bbf47f6c`.
- `host.log`: 49,096 bytes, SHA-256 `a17ec4f6d7156d24f83c3089fdf1bd190f3385895e2015372a4509f00345462e`.

The full host log contains **one native `Curl error42: Callback aborted`**; the client diagnostic scan found none. This native message is outside the zero managed error counter and must not be hidden by it.

### Depot mission and replay

Run `a522792bb0fa4cf2b1fad3c84742429e`, port 55932, host PID 34972/client PID 29628, both exit 0. UTC **2026-11-08T23:28:37.4484977Z–23:29:15.8193433Z**. All **17 host /15 client** markers passed. Coverage includes remote power, replicated gate, shot/reload, waves, extraction, lobby reset, disconnect/rejoin IDs 2 to 3, and power/reload in a fresh second round.

- `Builds/LanRange/SmokeResults/a522792bb0fa4cf2b1fad3c84742429e/summary.json`: SHA-256 `168cc9b97a4426c713b0879571313654c74bbe9cb9dfa2d509c266e18b708d61`.

Literal error/exception/assert/warning/Curl scans over the complete mission host and client logs returned zero matches. Do not extend this statement to the range host log above.

## Visual review

Actual **1280 × 720** Game View images were captured and inspected in RestorePower with zero enemies:

- `Logs/acceptance-b7e078-fps.png`: cabinet readable, first-person HUD and objective guidance.
- `Logs/acceptance-b7e078-tps.png`: cyan local capsule and third-person view.
- `Logs/acceptance-b7e078-inspection-overview.png`: four loading bays, dashed lane, closed gate, concrete and blue extraction area.

No missing pink materials were observed. The overview uses a temporary inspection camera, not a supported player view. These are programmatic captures, not hands-on mouse/keyboard playtesting. The depot remains visibly sparse greybox art.

## Preservation and cleanup

Host stopped, disconnected and released controls; Editor stopped/ready, background false, scene clean. All 23 protected hashes matched the prior accepted post-build baseline. The familiar automatic ProjectSettings InputSystem preload transition occurred during tests/build and returned to the `1125DD…` baseline; it was not manually restored.

Unity normalized the six new cabinet metadata files: empty-string/inline YAML formatting plus default, non-overridden Standalone/Android/WebGL texture entries. Model animation/tangent settings and normal-map type/shape/sRGB/green-channel/conversion semantics were unchanged. Preserve these generated local diffs; do not stage, discard or restore them automatically. Existing floor material and settings changes remain preserved.

No new source fixes, branches, backups or security changes were made locally. Launcher-owned exits and cleanup were used; earlier denied broad process enumeration was not retried. The next cloud façade candidate must have its own exact revision and acceptance, rather than borrowing this pass.
