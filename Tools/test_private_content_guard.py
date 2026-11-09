#!/usr/bin/env python3
"""Exercise real temporary Git indexes; never write to project assets or its index."""
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from validate_private_content import PRIVATE_ROOT, is_private_path, validate_repository


IGNORE = "/Assets/UndeadOvertimePrivate/\n/Assets/UndeadOvertimePrivate.meta\n"


class PrivatePathTests(unittest.TestCase):
    def test_boundary_meta_and_case_variants_are_private(self):
        for path in (
            PRIVATE_ROOT, PRIVATE_ROOT + "/character.fbx", PRIVATE_ROOT + "/nested/clip.fbx.meta",
            PRIVATE_ROOT + ".meta", "assets/undeadOVERTIMEprivate/character.fbx",
            r"Assets\UndeadOvertimePrivate\character.fbx", PRIVATE_ROOT + ".meta/child",
        ):
            with self.subTest(path=path):
                self.assertTrue(is_private_path(path))

    def test_similarly_named_public_files_are_not_private(self):
        for path in (
            "Documentation/PRIVATE_CONTENT_BOUNDARY.md", "Assets/UndeadOvertime/LanEditor/PrivateContentBuildGuard.cs",
            "Assets/UndeadOvertimePrivatePreview.txt", "Assets/UndeadOvertimePrivate.meta.txt",
            "Assets/Other/UndeadOvertimePrivate/a.txt",
        ):
            with self.subTest(path=path):
                self.assertFalse(is_private_path(path))


class GitBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="uo-private-content-git-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.run_git("init", "--quiet")
        self.write(".gitignore", IGNORE)
        self.run_git("add", ".gitignore")

    def run_git(self, *args):
        return subprocess.run(["git", "-C", str(self.root), *args], check=True, capture_output=True)

    def write(self, path, data="synthetic test marker\n"):
        destination = self.root / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(data)

    def test_clean_index_passes(self):
        self.assertEqual([], validate_repository(self.root))

    def test_ignored_private_content_is_not_staged_by_add_all(self):
        self.write(PRIVATE_ROOT + "/synthetic.txt")
        self.write(PRIVATE_ROOT + "/nested/synthetic.txt.meta")
        self.write(PRIVATE_ROOT + ".meta")
        self.run_git("add", "--all")
        self.assertEqual(b".gitignore\0", self.run_git("ls-files", "-z").stdout)
        self.assertEqual([], validate_repository(self.root))

    def test_force_added_private_content_is_rejected(self):
        path = PRIVATE_ROOT + "/synthetic.txt"
        self.write(path)
        self.run_git("add", "--force", path)
        self.assertTrue(any(path in error for error in validate_repository(self.root)))

    def test_force_added_root_meta_is_rejected(self):
        path = PRIVATE_ROOT + ".meta"
        self.write(path)
        self.run_git("add", "--force", path)
        self.assertTrue(any(path in error for error in validate_repository(self.root)))

    def test_case_variant_is_rejected(self):
        path = "Assets/undeadOvertimePRIVATE/synthetic.txt"
        self.write(path)
        self.run_git("add", "--force", path)
        self.assertTrue(any(path in error for error in validate_repository(self.root)))

    def test_deleted_working_file_remains_rejected_if_still_in_index(self):
        path = PRIVATE_ROOT + "/synthetic.txt"
        self.write(path)
        self.run_git("add", "--force", path)
        (self.root / path).unlink()
        self.assertTrue(any(path in error for error in validate_repository(self.root)))

    def test_file_occupying_boundary_root_is_rejected(self):
        self.write(PRIVATE_ROOT)
        self.run_git("add", "--force", PRIVATE_ROOT)
        self.assertTrue(any(PRIVATE_ROOT in error for error in validate_repository(self.root)))

    def test_missing_ignore_rules_fail(self):
        self.write(".gitignore", "# missing boundary rules\n")
        errors = validate_repository(self.root)
        self.assertEqual(2, len(errors))
        self.assertTrue(all("not ignored" in error for error in errors))

    def test_missing_meta_ignore_rule_fails(self):
        self.write(".gitignore", "/Assets/UndeadOvertimePrivate/\n")
        self.assertEqual(["Required local-only path is not ignored by the repository .gitignore: " + PRIVATE_ROOT + ".meta"], validate_repository(self.root))

    def test_local_exclude_cannot_replace_shared_repository_rules(self):
        self.write(".gitignore", "# missing shared rules\n")
        self.write(".git/info/exclude", IGNORE)
        self.assertEqual(2, len(validate_repository(self.root)))

    def test_negated_ignore_rules_do_not_count_as_protection(self):
        self.write(".gitignore", IGNORE + "!/Assets/UndeadOvertimePrivate.meta\n")
        self.assertTrue(validate_repository(self.root))

    def test_non_git_directory_fails_closed(self):
        with tempfile.TemporaryDirectory(prefix="uo-not-git-") as directory:
            self.assertTrue(validate_repository(Path(directory)))

    def test_nested_non_root_fails_closed(self):
        nested = self.root / "subdirectory"
        nested.mkdir()
        self.assertTrue(validate_repository(nested))

    def test_missing_git_executable_fails_closed(self):
        with patch("validate_private_content.subprocess.run", side_effect=FileNotFoundError("git unavailable")):
            self.assertTrue(validate_repository(self.root))


if __name__ == "__main__":
    unittest.main(verbosity=2)
