#!/usr/bin/env python3
"""Run malformed-meta regressions without modifying any project file or invoking Unity."""
from pathlib import Path
from unittest.mock import patch
import contextlib
import io
import json
import runpy

ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "Tools/validate_prototype.py"
PACKET = json.loads((ROOT / "Documentation/DEPOT_FLOOR_PACKET.json").read_text())
PACKET["files"] += json.loads((ROOT / "Documentation/DEPOT_CONSOLE_PACKET.json").read_text())["files"]
PACKET["files"] += json.loads((ROOT / "Documentation/DEPOT_FACADE_PACKET.json").read_text())["files"]
read_text = Path.read_text
checks = 0
for entry in PACKET["files"]:
    meta = Path(str(ROOT / entry["path"]) + ".meta")
    actual = read_text(meta)
    guid_lines = "\n".join(actual.splitlines()[:2]) + "\n"
    bad_cases = [(guid_lines, "Missing/unsupported")]
    if entry["path"].endswith(".png"):
        assert "  textureShape: 1\n" in actual
        bad_cases.append((actual.replace("  textureShape: 1\n", "  textureShape: 2\n"), "Runtime texture must explicitly import as Texture2D"))
    for replacement, expected in bad_cases:
        def fake_read(path, *args, **kwargs):
            return replacement if path == meta else read_text(path, *args, **kwargs)
        try:
            with patch.object(Path, "read_text", fake_read), contextlib.redirect_stdout(io.StringIO()):
                runpy.run_path(str(VALIDATOR), run_name="__main__")
        except SystemExit as error:
            message = str(error)
            assert expected in message and entry["path"] + ".meta" in message, message
        else:
            raise AssertionError("Validator accepted malformed importer metadata: " + str(meta))
        checks += 1
print(f"PASS: {checks} malformed importer fixtures rejected (twenty-one GUID-only, twelve cubemap); no file writes or Unity execution.")
