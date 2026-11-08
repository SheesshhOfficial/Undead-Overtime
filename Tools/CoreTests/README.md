# Pure-core cloud verification

This supplementary harness compiles the actual `Assets/UndeadOvertime/Core/*.cs` into a separate .NET Standard 2.1 assembly and compiles/runs **all** `Tests/CoreEditMode/*.cs` in a .NET 10 NUnitLite runner. Both projects use C# 9 and treat compiler warnings as errors. Production code is not copied into the harness.

It does **not** compile Unity-dependent assemblies or establish Unity EditMode/PlayMode, physics, materials, graphics, player-build, actual LAN or performance acceptance. .NET Standard API and assembly boundaries are useful checks, but they do not reproduce Unity's compiler defines, asmdef resolution, Mono/IL2CPP or runtime. Unity acceptance remains mandatory.

## Prerequisites

- An already installed, approved Microsoft .NET SDK **10.0.403**. `global.json` disables automatic roll-forward. This harness never downloads or installs an SDK.
- Python 3.9+ for the orchestration script.
- Access to official NuGet during the initial restore. The scoped `NuGet.Config` clears other sources. NUnit and NUnitLite are pinned to exactly 3.14.0; checked-in lockfiles include transitive package content hashes.

Official SDK information: https://dotnet.microsoft.com/download/dotnet/10.0 . Do not copy a cloud SDK/cache into the Unity repository or install software on a user's machine without its required permission. The first verified cloud run used Microsoft's Linux x64 SDK archive and matched its SHA-512 against the official release metadata before extracting it into a separate workspace-only location.

## Run

From the repository root:

```text
python Tools/CoreTests/run_core_tests.py
```

Or select the approved SDK executable explicitly:

```text
python Tools/CoreTests/run_core_tests.py --dotnet /absolute/path/to/dotnet
```

On Windows, the optional path can point to `dotnet.exe`. Windows execution of this harness has not been verified. The Unity editor is not needed and should not be launched for this check.

The runner:

1. Resolves the SDK pin from this directory and fails on a mismatch.
2. Records exact source-file bytes, SHA-256 and Git blob SHA-1 before execution.
3. Restores with `--locked-mode`, the scoped feed and workspace-local caches.
4. Compiles the core and test assemblies separately in Release, without a second implicit restore or persistent build server.
5. Runs NUnitLite without a test filter, using a fixed seed and one sequential worker context.
6. Requires a nonzero discovered test count, all cases passed, zero failures/warnings/inconclusive/skips, and byte-identical source files/lockfiles after execution.
7. Retains NUnit XML, build/restore/SDK logs, source records and a compact summary under a unique `results/` directory. Nonzero exit means failed or incomplete verification.

`results/`, `.cache/`, `bin/` and `obj/` are excluded from Git. The script performs no Git writes, Unity calls, source edits or machine-wide configuration changes. It scopes NuGet/CLI/Unix-runtime state to this directory and disables CLI telemetry for its child processes. Allow several minutes for the first restore. Keep raw logs private if they contain local paths; share the compact summary or a reviewed report instead.

If updating packages or the SDK, review the new official versions and regenerate/review both lockfiles deliberately. Do not remove `--locked-mode` merely to silence a dependency mismatch.

## Reproducibility and scope

See `Documentation/CLOUD_CORE_VERIFICATION.md` for the first recorded run and its source baseline. A run verifies the exact hashes it records, not whichever branch happens to exist later. The harness does not infer a Git commit from a copied source folder. Compare its source records to the intended remote commit before attributing results to that revision.
