# Native acceptance before playable mission wiring

## Exact baseline

`802ce58b7aa30639291c9e21834e52e84fd5fdc7`, main, Unity **6000.6.5f1**, Windows. The guarded fast-forward from `4b7e1ab` confirmed 54 incoming paths with no overlap against 19 modified tracked and two untracked protected local files. No reset, stash, branch, floor reimport or local source repair was used.

- Compilation completed with no errors.
- EditMode: **288/288 passed**, zero failures/skips/inconclusive, **42.86 seconds**. The native execution transcript retained the summary and 288 result-item count. No separate complete EditMode XML/JSON was found, and the suite was not rerun merely to manufacture one.
- PlayMode: **56/56 passed**, zero failures/skips/inconclusive, **83.63 seconds**. Complete native JSON retained as `Logs/mission-802ce58-playmode-results.json`, 10,950 bytes.
- One Windows build succeeded. `UO_LAN_BUILD_OK` appeared at **2026-11-08T20:50:06.7009893Z**; BuildPipeline duration **37,327 ms**.

Managed build fingerprints:

- Core: 45,568 bytes, SHA-256 `53b48142428bef131f70ac9308ab4f66ffd7d549c760b1facd4361a30a17ef0a`.
- LAN runtime: 69,120 bytes, SHA-256 `96f8caf2567f1eddd48b3fb6e419a985d93e68abb1e7ae0437871e2bbe8c7251`.
- Prototype: 30,208 bytes, SHA-256 `e8459f54b6bb4aee2bb205a0cf458b58cb9b92790af3a9c84a81ab0784314ff6`.
- The unchanged player EXE stub hash was `b70442f90f50cd91f7af7bf2272602645fc5d02c4d3156fb221fc27c328d2436`; its unchanged timestamp alone is not evidence of a stale managed build.

## Actual two-process range test

Run `1380d650482d44c686a3d850ce9425a6`: **2026-11-08T20:50:41.6885302Z to 20:51:48.8252327Z**, loopback port **49549**. Host PID **31056** and client PID **32472** both exited zero and were confirmed gone. Both reports recorded success and zero runtime callback errors/warnings; all **29 host /21 client** required steps were present, including revive/reload, three waves and shutdown.

Full logs were also reviewed: host 49,681 bytes, client 37,592 bytes. Each contained **six native Curl error 7** connection failures for Unity analytics/configuration endpoints. No other error/exception/assert/warning matches were found. Zero callback counters therefore do not mean entirely error-free native logs.

At **20:52:45Z**, Editor was ready/stopped, not compiling/reloading, background mode false, index clean. All protected hashes matched the post-floor baseline. Unity automatically removed then restored the existing InputSystem preloaded-asset serialization during tests/build; no manual restoration or source/settings edit was performed.

## Floor acceptance and scope

The preceding pinned `4b7e1ab` run passed **146 EditMode /42 PlayMode**, Windows build and its full range loopback. Its actual 1280-by-720 FPS/TPS Game View captures were inspected: original floor textures rendered without magenta; the expected HUD and TPS capsule were present. This established floor integration, not a polished map.

The `802ce58` mission rules/codec/presentation components were tested natively, but **mission mode was not wired into the scene or transport at that baseline**. This record is not acceptance of later playable mission wiring, real LAN, ten players, manual controls, production character animation or performance.
