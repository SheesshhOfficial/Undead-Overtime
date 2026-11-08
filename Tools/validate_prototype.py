#!/usr/bin/env python3
"""Static consistency checks only; this does not compile or execute Unity/C#."""
import json
import hashlib
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
        expected = ("UndeadOvertime.Core" if "CoreEditMode" in path.parts else
                    "UndeadOvertime.LanPrototype" if any(part in ("LanEditMode", "LanPlayMode") for part in path.parts)
                    else "UndeadOvertime.Prototype")
        check(expected in data["references"], f"Missing runtime reference: {path}")
    elif data["name"] == "UndeadOvertime.Core":
        check(data.get("noEngineReferences") is True, "Core must remain engine independent")
        check(not data["references"], "Core must not acquire package dependencies")
    elif data["name"] == "UndeadOvertime.Lan.Editor":
        check(data.get("includePlatforms") == ["Editor"], "LAN build helper must be editor-only")
    else:
        check("Unity.InputSystem" in data["references"], "Missing Input System assembly reference")

bootstrap_guid = guid(ASSETS / "Runtime" / "PrototypeRange.cs.meta")
for name, survival in [("PrototypeRange", False), ("SurvivalYard", True), ("LanRange", False)]:
    scene_guid = guid(ASSETS / "LanRuntime" / "LanRange.cs.meta") if name == "LanRange" else bootstrap_guid
    scene_path = ASSETS / "Scenes" / f"{name}.unity"
    scene = scene_path.read_text()
    anchors = re.findall(r"^--- !u!\d+ &(-?\d+)$", scene, re.MULTILINE)
    check(len(anchors) == len(set(anchors)), f"Duplicate scene object file IDs: {name}")
    anchor_set = set(anchors)
    for ref in re.findall(r"\{fileID: (-?\d+)\}", scene):
        check(ref == "0" or ref in anchor_set, f"Unresolved in-scene file ID: {name}/{ref}")
    check(scene.count(f"guid: {scene_guid}") == 1, f"Expected one range bootstrap: {name}")
    check("worldCamera: {fileID: 330585545}" in scene, f"Missing serialized camera: {name}")
    check("  - {fileID: 1000002}" in scene, f"Bootstrap missing from scene roots: {name}")
    check(("survivalMode: 1" in scene) == survival, f"Incorrect opt-in survival mode: {name}")
check("[ExecuteAlways]" not in "".join(p.read_text() for p in ASSETS.rglob("*.cs")), "Unexpected editor execution hook")
check(not any(p.name in ("Library", "Temp", "Logs") for p in ASSETS.rglob("*")), "Generated Unity data under source assets")

for path in files:
    raw = path.read_bytes()
    if path.suffix.lower() in (".fbx", ".png"):
        approved = {ASSETS / "Resources" / "UndeadOvertime" / name for name in ("DepotFloor", "DepotConsole")}
        check(path.parent in approved, f"Binary asset outside reviewed runtime packet: {path.relative_to(ROOT)}")
        signature = b"Kaydara FBX Binary  \x00\x1a\x00" if path.suffix.lower() == ".fbx" else b"\x89PNG\r\n\x1a\n"
        check(raw.startswith(signature), f"Invalid native asset signature: {path.relative_to(ROOT)}")
        # GUID-only metadata falls back to importer schema version 1 in native Unity,
        # which is rejected before our AssetPostprocessor can enforce the intended settings.
        meta = Path(str(path) + ".meta").read_text()
        importer_name, minimum_version = ("ModelImporter", 25) if path.suffix.lower() == ".fbx" else ("TextureImporter", 10)
        block = re.search(rf"^{importer_name}:\n((?:[ \t].*\n|\n)*)", meta, re.MULTILINE)
        schema = re.search(r"^  serializedVersion: ([0-9]+)$", block[1], re.MULTILINE) if block else None
        check(schema is not None and int(schema[1]) >= minimum_version,
              f"Missing/unsupported {importer_name} serializedVersion in {path.relative_to(ROOT)}.meta")
        if importer_name == "TextureImporter":
            check(block is not None and re.search(r"^  textureShape: 1$", block[1], re.MULTILINE) is not None,
                  f"Runtime texture must explicitly import as Texture2D, not a cubemap: {path.relative_to(ROOT)}.meta")
        required_sections = ("materials", "animations", "meshes", "tangentSpace", "humanDescription") if importer_name == "ModelImporter" else ("mipmaps", "bumpmap", "textureSettings", "platformSettings")
        for section in required_sections:
            check(block is not None and re.search(rf"^  {section}:", block[1], re.MULTILINE) is not None,
                  f"Incomplete {importer_name} {section} schema in {path.relative_to(ROOT)}.meta")
    else:
        check(b"\r" not in raw, f"Expected LF line endings: {path.relative_to(ROOT)}")
        check(raw.endswith(b"\n"), f"Missing final newline: {path.relative_to(ROOT)}")
        check(b"\x00" not in raw, f"Unexpected binary data: {path.relative_to(ROOT)}")

packet_path = ROOT / "Documentation" / "DEPOT_FLOOR_PACKET.json"
if packet_path.exists():
    packet = json.loads(packet_path.read_text())
    check(len(packet["files"]) == 9, "Expected exactly nine original depot floor runtime binaries")
    for entry in packet["files"]:
        relative = Path(entry["path"])
        allowed = Path("Assets/UndeadOvertime/Resources/UndeadOvertime/DepotFloor")
        check(relative.parent == allowed, "Unexpected floor packet path")
        if relative.parent != allowed:
            continue
        path = ROOT / relative
        check(path.is_file(), f"Missing verified floor runtime file: {relative}")
        if path.is_file():
            data = path.read_bytes()
            check(len(data) == entry["bytes"] and hashlib.sha256(data).hexdigest() == entry["sha256"], f"Floor packet hash mismatch: {relative}")

packet_path = ROOT / "Documentation" / "DEPOT_CONSOLE_PACKET.json"
if packet_path.exists():
    packet = json.loads(packet_path.read_text())
    check(len(packet["files"]) == 6, "Expected exactly six original cabinet runtime binaries")
    allowed = Path("Assets/UndeadOvertime/Resources/UndeadOvertime/DepotConsole")
    expected = {f"UO_DepotConsole_LOD{i}.fbx" for i in range(3)} | {"cabinet_basecolor.png", "cabinet_normal_opengl.png", "cabinet_metallic_smoothness.png"}
    check({Path(e["path"]).name for e in packet["files"]} == expected, "Cabinet packet paths are not the exact selected six files")
    for entry in packet["files"]:
        relative = Path(entry["path"])
        check(relative.parent == allowed, "Unexpected cabinet packet path")
        if relative.parent != allowed: continue
        path = ROOT / relative
        check(path.is_file(), f"Missing cabinet runtime file: {relative}")
        if path.is_file():
            data = path.read_bytes()
            check(len(data) == entry["bytes"] and hashlib.sha256(data).hexdigest() == entry["sha256"], f"Cabinet packet hash mismatch: {relative}")

if errors:
    raise SystemExit("STATIC CHECK FAILED\n" + "\n".join(errors))
print(f"Static consistency PASS: {len(files)} source/assets, {len(assembly_names)} assemblies, {len(seen)} metadata GUIDs checked.")
print("Unity compilation, EditMode/PlayMode results, rendering, player build, and LAN behavior are NOT verified by this script.")

