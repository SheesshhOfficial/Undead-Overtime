"""Guarded execution-only handoff for an already saved CityMap.

Run only after the native editor confirms clean, idle Edit Mode. This does not
operate Unity, save a scene, discard edits, stash work, or change branches.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

SCENE = "Assets/Scenes/CityMap.unity"
PREFAB = "Assets/UndeadOvertime/Environment/City/Details/CityDetails.prefab"
SOURCE_SHA = "5a7d057b47ba610862bec6669f51540ab4ac60d082aaeccbd588fad76c5db88f"
TARGET_SHA = "061e4ccec63a014758ebbdb157227a9d8f94a56352ca143f645f4ca61f49c63f"
SOURCE_HEAD = "ac098ef9521ec52704e987b05ba273211769e54d"
ROOT_CHILD = b"  - {fileID: 7000000001}\n"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def git(*args, check=True):
    p = subprocess.run(["git", *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check and p.returncode:
        raise RuntimeError("git " + " ".join(args) + ": " + p.stderr.decode(errors="replace"))
    return p


def text(*args):
    return git(*args).stdout.decode("utf-8").strip()


def changed_paths():
    paths = set()
    for args in [("diff", "--name-only", "-z"),
                 ("diff", "--cached", "--name-only", "-z"),
                 ("ls-files", "--others", "--exclude-standard", "-z")]:
        paths.update(x.decode("utf-8") for x in git(*args).stdout.split(b"\0") if x)
    return paths


def state(path):
    p = Path(path)
    if p.is_symlink():
        working = {"symlink": os.readlink(p)}
    elif p.is_file():
        working = {"sha256": digest(p.read_bytes()), "bytes": p.stat().st_size}
    elif not p.exists():
        working = {"missing": True}
    else:
        raise RuntimeError("Cannot safely fingerprint dirty non-file path: " + path)
    return {"working": working, "index": git("ls-files", "--stage", "-z", "--", path).stdout.hex()}


def verify_preservation(source, target, prefab):
    header = b"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
    if not prefab.startswith(header):
        raise RuntimeError("Unexpected prefab header")
    fragment = prefab[len(header):]
    root = re.search(rb"^--- !u!4 &7000000001\n.*?(?=^--- !u!)", fragment, re.M | re.S)
    if not root or root.group().count(b"  m_Father: {fileID: 0}\n") != 1:
        raise RuntimeError("Unexpected detail parent")
    fragment = (fragment[:root.start()] + root.group().replace(
        b"  m_Father: {fileID: 0}\n", b"  m_Father: {fileID: 1001}\n") + fragment[root.end():])
    if target.count(fragment) != 1 or target.count(ROOT_CHILD) != 1:
        raise RuntimeError("The target is not the reviewed additive scene")
    restored = target.replace(fragment, b"", 1).replace(ROOT_CHILD, b"", 1)
    if restored != source:
        raise RuntimeError("The incoming scene does not preserve the exact saved source")


def run():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--repo", required=True)
    ap.add_argument("--target", required=True, help="Exact reviewed 40-character remote commit")
    ap.add_argument("--apply", action="store_true", help="Apply after the native clean/idle editor guard")
    a = ap.parse_args()
    os.chdir(Path(a.repo).resolve())
    if not re.fullmatch(r"[0-9a-f]{40}", a.target):
        raise RuntimeError("Target must be an exact SHA")
    if text("branch", "--show-current") != "main":
        raise RuntimeError("Not on the existing main branch")
    head = text("rev-parse", "HEAD")
    if head not in (SOURCE_HEAD, a.target):
        raise RuntimeError("HEAD changed: " + head)
    if text("rev-parse", "refs/remotes/origin/main") != a.target:
        raise RuntimeError("Fetched origin/main does not match the reviewed target")
    if git("merge-base", "--is-ancestor", SOURCE_HEAD, a.target, check=False).returncode:
        raise RuntimeError("Target is not a fast-forward of the source revision")
    for sentinel in ("MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply", "index.lock"):
        if Path(text("rev-parse", "--git-path", sentinel)).exists():
            raise RuntimeError("An existing Git operation is active: " + sentinel)
    if git("ls-files", "-u").stdout:
        raise RuntimeError("Unmerged index entries exist")
    hook_config = git("config", "--path", "--get", "core.hooksPath", check=False)
    hooks = Path(hook_config.stdout.decode().strip()) if hook_config.returncode == 0 else Path(text("rev-parse", "--git-path", "hooks"))
    for name in ("post-merge", "reference-transaction"):
        if (hooks / name).is_file():
            raise RuntimeError("Review active Git hook before guarded sync: " + str(hooks / name))

    scene = Path(SCENE)
    if scene.is_symlink() or not scene.is_file():
        raise RuntimeError("CityMap is not an ordinary file")
    current = scene.read_bytes()
    current_sha = digest(current)
    incoming = git("show", a.target + ":" + SCENE).stdout
    if digest(incoming) != TARGET_SHA:
        raise RuntimeError("Incoming scene hash differs from the reviewed integration")
    if head == a.target and current_sha == TARGET_SHA:
        print(json.dumps({"status": "already_applied", "head": head, "scene_sha256": TARGET_SHA}))
        return
    if current_sha != SOURCE_SHA:
        raise RuntimeError("Saved CityMap changed; stop and obtain its new snapshot")
    verify_preservation(current, incoming, git("show", a.target + ":" + PREFAB).stdout)

    dirty = changed_paths()
    other_dirty = dirty - {SCENE}
    incoming_paths = set(x.decode("utf-8") for x in git(
        "diff", "--name-only", "-z", SOURCE_HEAD, a.target).stdout.split(b"\0") if x)
    overlap = sorted(other_dirty & incoming_paths)
    if overlap:
        raise RuntimeError("Incoming changes overlap protected local paths: " + repr(overlap))
    before = {p: state(p) for p in sorted(other_dirty)}
    old_index = git("ls-files", "--stage", "--", SCENE).stdout.decode().strip().split()
    if len(old_index) < 3 or old_index[2] != "0":
        raise RuntimeError("Unexpected scene index entry")
    incoming_blob = text("rev-parse", a.target + ":" + SCENE)
    expected_index = incoming_blob if head == a.target else text("rev-parse", SOURCE_HEAD + ":" + SCENE)
    if old_index[1] != expected_index:
        raise RuntimeError("CityMap has staged edits; do not alter its staging state")
    report = {"status": "preflight_ok", "head": head, "target": a.target,
              "protected_paths": len(before), "saved_scene_sha256": SOURCE_SHA,
              "target_scene_sha256": TARGET_SHA, "exact_saved_bytes_preserved": True}
    if not a.apply:
        print(json.dumps(report, indent=2))
        return

    # Stage only the already reviewed incoming blob, not the working scene.
    # A two-tree fast-forward then leaves the saved working scene untouched.
    if head != a.target:
        git("update-index", "--cacheinfo", old_index[0] + "," + incoming_blob + "," + SCENE)
        merged = git("merge", "--ff-only", "--no-overwrite-ignore", a.target, check=False)
        if merged.returncode:
            now_index = git("ls-files", "--stage", "--", SCENE).stdout.decode().strip().split()
            if text("rev-parse", "HEAD") == head and len(now_index) >= 3 and now_index[1] == incoming_blob and now_index[2] == "0":
                git("update-index", "--cacheinfo", old_index[0] + "," + old_index[1] + "," + SCENE)
            raise RuntimeError("Fast-forward failed; scene bytes not written: " + merged.stderr.decode(errors="replace"))
    if text("rev-parse", "HEAD") != a.target:
        raise RuntimeError("Unexpected HEAD after fast-forward; scene bytes not written")
    if digest(scene.read_bytes()) != SOURCE_SHA:
        raise RuntimeError("Scene changed during sync; HEAD advanced but scene bytes not written")
    after = {p: state(p) for p in before}
    if after != before:
        raise RuntimeError("Protected local content changed; scene bytes not written")
    fd, temp_name = tempfile.mkstemp(prefix=".city-details-", suffix=".tmp", dir=str(scene.parent))
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(incoming)
            f.flush()
            os.fsync(f.fileno())
        if digest(scene.read_bytes()) != SOURCE_SHA:
            raise RuntimeError("Scene changed before installation; scene bytes not written")
        os.replace(temp_name, scene)
    finally:
        if os.path.exists(temp_name):
            os.unlink(temp_name)
    if digest(scene.read_bytes()) != TARGET_SHA:
        raise RuntimeError("Installed scene verification failed; do not reload Unity")
    if {p: state(p) for p in before} != before:
        raise RuntimeError("Protected local content changed; do not reload Unity")
    if git("diff", "--", SCENE).stdout or git("diff", "--cached", "--", SCENE).stdout:
        raise RuntimeError("CityMap differs from committed target; do not reload Unity")
    report.update(status="applied", head=a.target, scene_sha256=TARGET_SHA,
                  protected_paths_unchanged=True, unity_reload_performed=False)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    try:
        run()
    except Exception as exc:
        print(json.dumps({"status": "stopped", "reason": str(exc)}), file=sys.stderr)
        sys.exit(1)
