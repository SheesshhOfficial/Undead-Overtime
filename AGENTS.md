# Undead Overtime agent workflow

## Scope and read order

This repository holds the Unity game implementation. The user has authorized starting game development in parallel with remaining assets. Read this file, the current README, relevant Documentation files, the current task's handoff and applicable local instructions before acting. Preserve original scenes, user changes and unrelated project content.

The private source/planning counterpart is [fbxfiles / Undead Overtime Extrafiles](https://github.com/SheesshhOfficial/fbxfiles/tree/main/Undead%20Overtime%20Extrafiles). Its [Unity workflow](https://github.com/SheesshhOfficial/fbxfiles/blob/main/Undead%20Overtime%20Extrafiles/production/UNITY_WORKFLOW.md) contains the full cross-repository process. Access to that private folder is not implied by this public link.

## User-confirmed division of work

- **Lob/dot in the cloud** owns research, planning, design, architecture, code authoring, code edits, debugging, fixes, asset preparation, tests and documentation. Use the user's requested highest available dot capability and effort for substantive cloud work; report unavailable configuration rather than pretending it is selected.
- **Local Codex** performs the least necessary local execution only, inside the existing Undead Overtime project chat. The requested local configuration is **GPT-6 Sol, medium effort** when available.
- Local execution includes inspecting checkout/editor state, fetching and pulling the exact reviewed cloud commit, executing the supplied Unity/CLI steps, and returning logs, test XML, screenshots and outcomes.
- Do not shift substantive code writing or fixes into the local execution chat. Return failures to cloud with evidence; the cloud author prepares the fix, publishes it, then local execution tests that revision.
- Reuse the correct project chat. Check an uncertain or timed-out task launch before creating another. A connected PC is not evidence that a task started or Unity ran.
- Continue independent work in parallel, but serialize repository writes and shared-file edits. Keep blocked actions on hold rather than bypassing them.

## Repository boundaries and asset licensing

- Game code, Unity scenes/prefabs, game tests and runtime implementation documentation belong here.
- Plans, research, original references, editable Blender sources, private licensed raw material, export specifications and source QA belong in the extras folder.
- This game repository was verified **public** on 2026-11-08; extras was **private**. Recheck before asset transfer.
- Do not copy the extras tree into this repository. Import only selected, provenance-checked runtime content whose source-distribution rights permit the destination.
- Mixamo and other third-party game-use licenses are not blanket permission to republish raw downloadable assets. Keep restricted files out of public Git history and document a private/local import route when needed.
- Keep secrets and unrelated personal information out of code, logs, screenshots and commits. Record configuration variable names, not secret values.

## Unity baseline and safe execution

Observed baseline: Unity **6000.6.5f1**, URP **17.6.0**. Recheck ProjectSettings/ProjectVersion.txt and Packages manifest/lock before running. Do not upgrade the editor, renderer, packages or build settings as an automatic troubleshooting step.

Before pull or execution, record the repository remote, branch, HEAD, git status, expected cloud commit and editor version. If user edits or a diverged branch are present, stop the affected step and report; do not reset, discard or auto-stash them.

Cloud publication must use a fresh head with a non-forced or lease-protected update and preserve concurrent files. Verify the remote commit before reporting a push. The local runner then fetches, confirms the expected commit and fast-forwards only.

Keep an implementation/test record for every cycle:
1. Narrow goal and acceptance criteria.
2. Exact code/asset files and source/export hashes.
3. Cloud checks actually run and unrun checks.
4. Published branch and commit.
5. Local commands/steps, exit codes, editor logs, test XML, screenshots and machine/editor version.
6. Failures, unresolved warnings and next cloud fix.
7. Final acceptance stage and remaining work.

Use the relevant Documentation runbook. For the initial range, see [PROTOTYPE_RANGE.md](Documentation/PROTOTYPE_RANGE.md) and [NETWORKING_PLAN.md](Documentation/NETWORKING_PLAN.md). Static check: `python Tools/validate_prototype.py`. Unity EditMode/PlayMode tests require the actual editor and their result files; code containing tests is not a passed test run.

Never claim Unity compilation, Play Mode, a build, LAN play, performance or visual acceptance from a Blender render or static check. Label authored, exported, published, imported, compiled, play-tested and multiplayer-tested separately.

## Game direction to preserve

Windows PC, solo plus 1–10-player LAN co-op, switchable FPS/TPS, revive, named survivors with abilities, human infected and custom creatures, a progression-gated original American-style city with backtracking, story missions and an ending plus waves. Preserve these goals while implementing bounded, testable slices. The current solo prototype is not the completed game or a multiplayer build.

The desired 4 GB RTX 20-series reference hardware is not a verified minimum specification. Exact CPU/GPU/RAM, host role, resolution, frame-rate and performance evidence must be recorded before making claims.

## Characters, animation and import checks

Use suitable **actual Mixamo characters and animations**, as requested. Original Mara/Drifter studies are optional concept/rig experiments, not replacements for acquiring the requested Mixamo material or finished realistic characters.

For downloads, record character/clip names, source/date, format, frame rate, skin inclusion and root-motion/in-place choice. An animation downloaded without skin is not a character. Verify a single character/idle/locomotion chain before expanding it. Check scale, pose, avatar mapping, weights, shoulders/hands/feet, root motion and retargeting. Custom monsters may need Blender rigs; do not claim Mixamo compatibility without testing.

For every imported asset, verify importer/version, axes/pivots, materials/normal convention, texture settings, LOD transitions, colliders and gameplay behavior. Validate the actual GLB import route or choose a tested alternative; a GLB file alone does not supply Unity integration.

Maintain shared interface constraints from asset records. Stair ramp and step colliders are alternatives; gate pivots and moving collision need runtime tests. Reassemble split Blender sources with their supplied combiner and verify SHA-256; never import a binary part file.

## Documentation and handoff

Document all meaningful changes, decisions, failures and tests. Keep confirmed, proposed, verified, blocked and superseded states distinct. Retain provenance and license records. Do not replace a failed result with an unsupported success label.

Each handoff states exact files, revision, checks/results, unrun checks, artifact links, blockers and next step. Current user instructions govern when they supersede historical phase notes; update the relevant records rather than silently reviving an old hold.
