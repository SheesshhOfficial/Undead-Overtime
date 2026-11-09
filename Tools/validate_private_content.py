#!/usr/bin/env python3
"""Read-only Git-index gate for the local-only licensed-content boundary.

This is not a license detector, history scrubber, or Unity build validator.
"""
import argparse
from pathlib import Path
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
PRIVATE_ROOT = "Assets/UndeadOvertimePrivate"
PRIVATE_META = PRIVATE_ROOT + ".meta"
IGNORE_PROBES = (PRIVATE_ROOT + "/.private-content-ignore-probe", PRIVATE_META)


def is_private_path(path):
    """Reject the boundary, its sibling meta, and case variants on any host OS."""
    path = path.replace("\\", "/").casefold()
    root = PRIVATE_ROOT.casefold()
    meta = PRIVATE_META.casefold()
    return path in (root, meta) or path.startswith(root + "/") or path.startswith(meta + "/")


def git(root, *args, input_data=None):
    return subprocess.run(
        ["git", "-C", str(root), *args], input=input_data,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False,
    )


def validate_repository(root=ROOT):
    """Return errors; a missing Git index or failed inspection never means PASS."""
    root = Path(root).resolve()
    try:
        top = git(root, "rev-parse", "--show-toplevel")
        if top.returncode or Path(top.stdout.decode(errors="surrogateescape").strip()).resolve() != root:
            return ["Private-content validation requires the actual Git worktree root (not an exported source folder)."]
        tracked = git(root, "ls-files", "--cached", "-z")
        if tracked.returncode:
            return ["Cannot inspect the Git index for private content; publication must stop."]
        errors = []
        for path in sorted(set(tracked.stdout.decode(errors="surrogateescape").split("\0"))):
            if path and is_private_path(path):
                errors.append("Private content is tracked/staged in the public repository: " + repr(path))
        probes = b"\0".join(path.encode() for path in IGNORE_PROBES) + b"\0"
        ignored = git(root, "check-ignore", "--no-index", "--verbose", "-z", "--stdin", input_data=probes)
        if ignored.returncode not in (0, 1):
            errors.append("Cannot verify private-content ignore rules; publication must stop.")
        else:
            fields = ignored.stdout.decode(errors="surrogateescape").split("\0")
            records = [fields[index:index + 4] for index in range(0, len(fields) - 1, 4)]
            ignored_paths = {
                record[3] for record in records
                if len(record) == 4 and record[0] == ".gitignore" and not record[2].startswith("!")
            }
            for path in IGNORE_PROBES:
                if path not in ignored_paths:
                    errors.append("Required local-only path is not ignored by the repository .gitignore: " + path)
        return errors
    except (OSError, UnicodeError) as error:
        return ["Cannot run private-content Git validation; publication must stop: " + str(error)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT, help="Git worktree root to inspect")
    args = parser.parse_args()
    errors = validate_repository(args.root)
    if errors:
        print("PRIVATE CONTENT CHECK FAILED\n" + "\n".join(errors), file=sys.stderr)
        return 1
    print("Private-content Git-index boundary PASS. No licensed-content or Unity build approval is implied.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
