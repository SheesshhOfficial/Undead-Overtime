# Native acceptance: 917d94

Exact main revision: `917d94aea47d25911b730fac6f2a5cd599783d14`, Unity **6000.6.5f1**, Windows local execution. This accepted the opt-in greybox depot mission and its default range regression. It is not complete-game, real-LAN, ten-player, visual mission, audio or performance acceptance.

## Native compilation and tests

Guarded fast-forward changed only the four reviewed test/documentation paths and preserved all 23 protected hashes. Native compilation passed. Full EditMode **305/305** passed in **22.83 s**; full PlayMode **65/65** passed in **125.13 s**, with zero failures, skips or inconclusive results. The blocked-placement negative case now passed with its bounded same-round terminal-frame check; the full solo mission also passed.

- `Logs/mission-917d94-editmode-response.json`: 58,776 bytes, SHA-256 `d432d7a45f581696e207f30c30bf785109326f5e514533faf321a705862ebd04`.
- `Logs/mission-917d94-playmode-status-06.json`: 12,715 bytes, SHA-256 `f84cfa958de720887a63c8c6778bae3bac842e0528ce3e793bd8f4008885045f`.

## One explicit Windows build

The build helper completed once. Fresh console sequence 1592 reported `UO_LAN_BUILD_OK` at **2026-11-08T21:36:27.0984075Z**; CLI duration 17.919 s. `Logs/mission-917d94-build-console-response.json`: 42,340 bytes, SHA-256 `c6d65c41b332edb581c2cc3105cbb2faaa49fee146e650208ff03fd8e718e09d`.

Fresh managed code fingerprints:
- Core: 46,592 bytes, `4da78437dc11016e0b69f91cf6d38fd5c9d54c0285def12bf75bb6c0467f2e9b`.
- LanPrototype: 83,968 bytes, `35e324c2b36bdcf692f9d07c3381f678924ebf9ef2d3f061293c00284a16a3aa`.
- Prototype: 30,208 bytes, `8a4fed8a252135fb43919860c4d0a3cd69827aa970f83b65316b791371d5fe9d`.

The unchanged Unity executable stub was not used alone as freshness evidence.

## Two standalone loopback scenarios

Both use **127.0.0.1 only**, one host and one client process, launched by the existing script with exact revision and build fingerprints. No firewall or other security/network settings were changed.

### Default range

Run `b7f533cf33ca4969a9d8db989bc16e84`, port 64630. UTC **21:37:54.2996011Z–21:39:00.1447611Z**, 2026-11-08. Host PID 37976, client PID 17060; both exit 0. Both reports success, zero runtime errors/warnings, all 29 host and 21 client required steps complete. Coverage includes movement/expiry, combat/reload, disconnect/rejoin/reset, infected pursuit/injury/kill, timed revive/restored actions, three waves and shutdown.

Evidence under `Builds/LanRange/SmokeResults/b7f533cf33ca4969a9d8db989bc16e84/`:
- summary.json: 1,459 bytes, `6050949fc32119e614b039b047077633165c874124f0bde17aef8e3be83e73a8`.
- host.json: 2,918 bytes, `7d55bb5790b02758c76cdcc7ce925d5add74c1fe06f2680a21ba59e6d93009f9`.
- client.json: 2,367 bytes, `2e953d25713d16f7403692596809e32a1f4ab4793089622808bda0357738e36d`.

### Opt-in mission

Run `89b4ddc467b84a4f8c4b5a2b02bb6e6b`, port 53014. UTC **21:39:29.7530972Z–21:40:02.7840107Z**, 2026-11-08. Host PID 23564, client PID 36880; both exit 0. Both reports success, runtime errors/warnings zero, all 11 host and 10 client required steps complete. Remote client walked to the console and supplied power, both observed the opened gate, each verified its shot/reload state, both fought through three waves and walked to whole-team extraction. Client completion receipt preceded host shutdown.

Evidence under `Builds/LanRange/SmokeResults/89b4ddc467b84a4f8c4b5a2b02bb6e6b/`:
- summary.json: 1,461 bytes, `24695f543f4968d46ceefa0ed4b86e89a9455ff2a816e43f6b893a754c90cf44`.
- host.json: 1,567 bytes, `c9686a813ad46d630084e1bc61a20babf0935f3cbbe15b9c44c60badcd566d2a`.
- client.json: 1,508 bytes, `cbf8df11cc62e1e460b69980a329cf263b8f91932753233ded4a66b334292ab8`.

Literal case-insensitive searches of all four complete logs returned zero error/exception/assert/warning/Curl terms. This is distinct from earlier runs that retained Unity telemetry Curl diagnostics. The reports and logs, not earlier histories, are the evidence for this revision. No screenshots were requested or captured in this pass.

## Preservation and cleanup

The editor ended ready/stopped, background false, active LanRange scene not dirty. BuildPipeline automatically restored the known InputSystem preload entry in ProjectSettings (F79CBE… to the prior 1125DD… hash); other 22 protected hashes remained unchanged. Final checkout retained 19 tracked dirty and two untracked files, clean index. Nothing was staged, reset, restored or rewritten manually.

Both smoke scenarios verified process exits through their launcher-owned handles and reports. A prior broad CIM enumeration was denied and was not retried; do not turn the owned-process evidence into a claim that every process on the PC was enumerated.

## Next bounded verification

Mission-mode lobby return, reconnect and a fresh playable second round were not covered by this accepted mission run. The next scoped candidate extends the mission smoke for those practical restart boundaries, without changing normal gameplay or extending the 90-second peer deadline. See `MISSION_LIFECYCLE.md`.
