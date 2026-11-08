# Cloud pure-core compilation and tests

## Scope and evidence boundary

The first executable cloud check covers the exact 12 production Core C# files and 12 CoreEditMode C# files from published baseline `940d29d91898fbacd280931f0c72bd4b40c15bcf`. Their Git blob hashes were compared against the remote tree before execution. No game-code fix was required by this check.

The standalone harness is under `Tools/CoreTests`. Production files compile into a separate **.NET Standard 2.1** assembly using **C# 9**; the test assembly runs on **.NET 10.0.14**, with **NUnit/NUnitLite 3.14.0**. Microsoft SDK **10.0.403** was installed only in an approved cloud workspace, not on the user's PC. Its 240,199,984-byte official Linux x64 archive matched the SHA-512 from Microsoft's official release metadata before extraction:

```text
b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5
```

Official metadata: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json

Initial CLI restore tried the container's read-only default home runtime directory and failed before package restore. The harness was configured to keep CLI, NuGet and Unix runtime state under its own workspace directory; no machine permission, security setting, user credential or Unity setting was changed. Official NuGet restore then succeeded. No feed substitution or signature-check bypass was used.

## Results

- Locked restore succeeded for both projects.
- Separate core/test compilation succeeded with **0 errors and 0 warnings**.
- Unfiltered NUnit run: **208 passed / 208 discovered**, with **0 failed, 0 skipped, 0 warnings and 0 inconclusive**.
- UTC start: `2026-11-08T18:30:40.0615908Z`. UTC end: `2026-11-08T18:30:40.4407249Z`. NUnit execution: `0.379084` seconds (restore/build are additional).
- All 24 source hashes matched the published baseline after execution; both dependency lockfiles remained unchanged.
- The initial single-assembly diagnostic run also passed 208/208, but the final acceptance above uses separate production/test assemblies and the stricter .NET Standard 2.1 core target.
- Static consistency, 48 Git-attribute assertions and 15 malformed-importer fixtures also passed. Those remain supplementary static checks, not Unity results.

The core cases cover mission power/extraction progression, the host coordinator, bounded mission packet parsing and replay guards, fire/reload, movement intent validation, infected encounters, revive, bounded waves, range snapshots, session lifecycle, loopback command-line validation and solo survival rules. The new mission suites contribute 86 of the cases (21 progression, 29 coordinator and 36 protocol). Random malformed packet sweeps run inside their named NUnit cases; their iterations are not counted as additional tests.

This supersedes the earlier **unrun** label only for these pure-core cases under this standalone runtime. It is not a native Unity test result. The 14 depot-geometry PlayMode tests, imported assets/materials, rendering, physics, Unity assembly compilation, scene integration, player builds, live mission networking and real LAN remain unverified for the newer mission candidates.

## Reproducibility

Run `python Tools/CoreTests/run_core_tests.py --dotnet /approved/sdk/path/dotnet` from the repository root. Use the already approved official SDK; the harness does not install software. See its README for prerequisites and output locations.

The script uses locked restores against official NuGet, warnings-as-errors compilation, no test filter, a fixed seed and zero skips as a required condition. It records and compares source and lockfile hashes before and after execution. A copied snapshot is not assumed to be a Git checkout; compare the recorded Git blob hashes to the intended commit before labeling its result.

The reviewed compact `CLOUD_CORE_VERIFICATION_RESULT.json` contains exact source hashes and fixture counts without local machine paths. Full XML and command logs remain in the execution workspace. No SDK archive, downloaded package, build output or local PC file belongs in this publication.

## What this does not unblock

- Native floor validation remains pinned to the separately reviewed material candidate `4b7e1ab63201afabd3db172ebc4eaf87447eb1dd`, pending its required local approval. This cloud check does not save either material or authorize that step.
- The mission geometry/coordinator/protocol are still inactive building blocks. They have not been silently connected to existing v5 clients or the active range.
- Existing accepted two-process loopback evidence remains evidence for its own earlier revision, not for this latest branch head.
- A standalone .NET Standard build checks API surface and assembly separation but cannot substitute for Unity's defines, asmdef processing, Mono/IL2CPP or platform behavior.
