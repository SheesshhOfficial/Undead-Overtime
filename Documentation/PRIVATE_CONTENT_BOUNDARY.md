# Private licensed-content staging boundary

## Purpose and current state

`Assets/UndeadOvertimePrivate/` is the single local-only import boundary for an upcoming actual Mixamo character/animation trial. It is outside the public runtime asset tree. The root directory, all children and the sibling `Assets/UndeadOvertimePrivate.meta` must stay out of public Git history. The directory and its metadata are not supplied by this change; Unity may generate them locally during an authorized trial.

This is a **temporary staging gate**, not a claim that licensed assets cannot appear in properly permitted compiled games. No licensed character or animation is added, downloaded, approved for distribution or imported by this change. The private `fbxfiles` source repository remains the source/provenance destination. Recheck repository visibility and the actual source/distribution terms before transferring assets. A game-use license does not by itself permit publishing raw downloadable sources to this public repository.

The upcoming trial can use this folder in the editor. Its contents currently have no approved player-build route. The staging policy is deliberately strict until specific character/clip provenance, permitted destinations and the compiled distribution path have been reviewed.

## Repository gate

The root `.gitignore` ignores the canonical directory and its root `.meta`. Do not add a tracked placeholder, `.gitkeep`, license receipt or import log inside this boundary. Public documentation can describe the workflow without including raw assets or private account information.

Before publication, run from the Git worktree root:

```sh
python Tools/validate_private_content.py
python Tools/test_private_content_guard.py
python Tools/validate_prototype.py
```

The private-content validator is also called by the ordinary prototype static check. It inspects `git ls-files --cached -z`, rejecting the boundary itself, every child and its root `.meta`, including case variants. This catches force-added content and a tracked file deleted only from the working tree. It also verifies that the shared repository `.gitignore`, rather than a machine-local exclusion, ignores canonical probe paths. Missing Git access or a non-worktree export fails closed.

This is a validation step, **not an installed Git hook, branch rule, history scan or universal license detector**. It must be run against the actual index/commit candidate before publication. It does not prevent someone from bypassing the command, and cannot identify restricted content renamed or copied elsewhere. Never use `git add -f` for the boundary. Do not remove original files or rewrite history automatically after a failure; stop publication and inspect the exact staged paths. If restricted material was already published, removing it in a later commit is not a history purge.

Use precisely `Assets/UndeadOvertimePrivate`, not a case variant. Case-variant paths are rejected by validation even on case-sensitive hosts; the canonical ignore rules are not a promise to ignore every alternate spelling.

## Ordinary Unity player builds

`PrivateContentBuildGuard` implements `IPreprocessBuildWithReport` in the existing editor-only assembly. This makes it apply to ordinary Unity player builds, including the normal editor build UI and the existing `LanRangeBuild.BuildWindows` route. It does not depend on callers using the project's custom menu.

The guard fails if the private boundary contains any entry, even an unused asset, a nested empty folder or a child `.meta`. A truly empty boundary and its normal sibling root `.meta` are allowed. A file or filesystem link at the boundary is rejected; unreadable/missing Assets inspection fails closed. The guard inspects directory entries only and never deletes, moves, rewrites or uploads trial content. No global allow boolean, environment override, editor preference or automatic build-setting edit is provided.

A failed build should be performed from a clean public-only checkout instead. Do not move restricted assets to another location under `Assets`, remove the guard or change project settings to bypass it. Public prototype builds should retain their current content while the private editor-only trial is reviewed separately.

**Limits:** this callback is not an asset export, AssetBundle, Addressables, package-export, Git-upload or release-publication interceptor. It also cannot police content copied outside the canonical boundary or deliberately introduced by later build callbacks. Git ignores alone never exclude referenced assets or `Resources` content from a Unity player. Do not export/share source packages, content bundles or player artifacts containing private trial content without the separate review below.

## Before a licensed-content player route is introduced

Review the exact selected character/clip sources, provenance, source hashes, license terms, permitted compiled distribution and permitted recipients. Keep restricted raw source and its private records outside public history. Any later implementation must be scoped to the reviewed assets and output route, preserve the default deny behavior for all other private content, and test ordinary player builds and other relevant export routes. Do not replace this gate with a broad bypass. This change does not perform that review or introduce that route.

## Verification and native handoff

Authored against `main` commit `d3fdd7a10b16e593d944d6c6ca2aa8ba64d89c1e`, Unity `6000.6.5f1`, URP `17.6.0`.

Cloud checks exercise synthetic temporary Git repositories only. The accompanying Unity EditMode tests use synthetic temporary directories outside the actual project assets. They cover clean/empty boundaries, root metadata, child assets and metadata, Resources/StreamingAssets/Editor paths, case variants, file collisions, missing inspection roots, non-mutation and callback interface registration. They do not include licensed material.

**Not yet verified:** real Unity compilation, execution of the new EditMode tests, native callback dispatch through both the standard build UI and `LanRangeBuild`, filesystem-link behavior on the target OS, player build output, actual Mixamo import or license/distribution approval. Run the native EditMode suite and a clean-public build at the final combined cloud commit. A native guard smoke test must use synthetic markers only in a disposable test copy: confirm an ordinary build is rejected with the private marker present, that the marker is unchanged, and that the clean-public build succeeds. Preserve all existing local/user content and return logs and test results to cloud for fixes.
