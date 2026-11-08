#!/usr/bin/env python3
"""Static consistency checks only; this does not compile or execute Unity/C#."""
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets" / "UndeadOvertime"
errors = []


def check(condition, message):
    if not condition:
        errors.append(message)


def guid(meta):
    matches = re.findall(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.MULTILINE)
    check(len(matches) == 1, f"Expected one valid GUID: {meta.relative_to(ROOT)}")
    return matches[0] if len(matches) == 1 else None


files = [p for p in ASSETS.rglob("*") if p.is_file() and p.suffix != ".meta"]
paths = [ASSETS] + [p for p in ASSETS.rglob("*") if p.suffix != ".meta"]
for path in paths:
    meta = Path(str(path) + ".meta")
    check(meta.is_file(), f"Missing metadata: {path.relative_to(ROOT)}")

seen = {}
for meta in (ROOT / "Assets").rglob("*.meta"):
    value = guid(meta)
    if value:
        check(value not in seen, f"Duplicate GUID: {meta} and {seen.get(value)}")
        seen[value] = meta
    if ASSETS in meta.parents or meta == Path(str(ASSETS) + ".meta"):
        check(Path(str(meta)[:-5]).exists(), f"Orphan metadata: {meta.relative_to(ROOT)}")

assembly_names = set()
for path in ASSETS.rglob("*.asmdef"):
    data = json.loads(path.read_text())
    check(data["name"] not in assembly_names, f"Duplicate assembly: {data['name']}")
    assembly_names.add(data["name"])
    if "Tests" in path.parts:
        check("TestAssemblies" in data.get("optionalUnityReferences", []), f"Missing test marker: {path}")
        check("UndeadOvertime.Prototype" in data["references"], f"Missing runtime reference: {path}")
    else:
        check("Unity.InputSystem" in data["references"], "Missing Input System assembly reference")

scene_path = ASSETS / "Scenes" / "PrototypeRange.unity"
scene = scene_path.read_text()
anchors = re.findall(r"^--- !u!\d+ &(-?\d+)$", scene, re.MULTILINE)
check(len(anchors) == len(set(anchors)), "Duplicate scene object file IDs")
anchor_set = set(anchors)
for ref in re.findall(r"\{fileID: (-?\d+)\}", scene):
    check(ref == "0" or ref in anchor_set, f"Unresolved in-scene file ID: {ref}")
bootstrap_guid = guid(ASSETS / "Runtime" / "PrototypeRange.cs.meta")
check(scene.count(f"guid: {bootstrap_guid}") == 1, "Expected one range bootstrap script reference")
check("worldCamera: {fileID: 330585545}" in scene, "Missing serialized range camera")
check("  - {fileID: 1000002}" in scene, "Bootstrap is missing from scene roots")
check("[ExecuteAlways]" not in "".join(p.read_text() for p in ASSETS.rglob("*.cs")), "Unexpected editor execution hook")
check(not any(p.name in ("Library", "Temp", "Logs") for p in ASSETS.rglob("*")), "Generated Unity data under source assets")

for path in files:
    raw = path.read_bytes()
    check(b"\r" not in raw, f"Expected LF line endings: {path.relative_to(ROOT)}")
    check(raw.endswith(b"\n"), f"Missing final newline: {path.relative_to(ROOT)}")
    check(b"\x00" not in raw, f"Unexpected binary data: {path.relative_to(ROOT)}")

if errors:
    raise SystemExit("STATIC CHECK FAILED\n" + "\n".join(errors))
print(f"Static consistency PASS: {len(files)} source/assets, {len(assembly_names)} assemblies, {len(seen)} metadata GUIDs checked.")
print("Unity compilation, EditMode/PlayMode results, rendering, player build, and LAN behavior are NOT verified by this script.")
