#!/usr/bin/env python3
"""Verify the selected raw runtime packet never invokes LFS; keep unrelated LFS rules intact."""
from pathlib import Path
import json
import os
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
packet = json.loads((ROOT / "Documentation/DEPOT_FLOOR_PACKET.json").read_text())
selected = [entry["path"] for entry in packet["files"]]
assert len(selected) == 9 and len(set(selected)) == 9
console = json.loads((ROOT / "Documentation/DEPOT_CONSOLE_PACKET.json").read_text())
selected += [entry["path"] for entry in console["files"]]
assert len(selected) == 15 and len(set(selected)) == 15
facade = json.loads((ROOT / "Documentation/DEPOT_FACADE_PACKET.json").read_text())
selected += [entry["path"] for entry in facade["files"]]
assert len(selected) == 21 and len(set(selected)) == 21
controls = ["Assets/Unrelated/model.fbx", "Assets/Unrelated/image.png", "Assets/Unrelated/model.obj"]
env = dict(os.environ, GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull)
with tempfile.TemporaryDirectory(prefix="uo-attributes-") as temp:
    subprocess.run(["git", "init", "--quiet", temp], check=True, env=env)
    (Path(temp) / ".gitattributes").write_bytes((ROOT / ".gitattributes").read_bytes())
    run = subprocess.run(["git", "-C", temp, "check-attr", "filter", "diff", "merge", "text", "--", *selected, *controls],
                         check=True, capture_output=True, text=True, env=env)
    results = {}
    for line in run.stdout.splitlines():
        path, attribute, value = line.rsplit(": ", 2)
        results[path, attribute] = value
    for path in selected:
        for attribute in ("filter", "diff", "merge", "text"):
            assert results[path, attribute] == "unset", (path, attribute, results.get((path, attribute)))
    for path in controls:
        for attribute in ("filter", "diff", "merge"):
            assert results[path, attribute] == "lfs", (path, attribute, results.get((path, attribute)))
        assert results[path, "text"] == "unset"
print("PASS: twenty-one reviewed binaries use ordinary Git storage with LFS filtering unset; unrelated LFS rules retained (96 attribute assertions).")
