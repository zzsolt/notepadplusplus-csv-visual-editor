from pathlib import Path
import importlib.util
import json
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "npp_release", ROOT / "tools/release/npp_plugin_package.py"
)
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class ReleasePackageTests(unittest.TestCase):
    def test_entry_matches_plugins_admin_contract(self):
        entry = release.plugin_admin_entry("1.0.1", "a" * 64)
        self.assertEqual("CsvVisualEditor", entry["folder-name"])
        self.assertEqual("CSV Visual Editor", entry["display-name"])
        self.assertEqual("1.0.1", entry["version"])
        self.assertEqual("[8.9.8,]", entry["npp-compatible-versions"])
        self.assertEqual("a" * 64, entry["id"])
        self.assertTrue(entry["repository"].endswith(
            "/releases/download/v1.0.1/CsvVisualEditor-1.0.1-win-x64.zip"
        ))
        self.assertEqual("Zolnai Zsolt", entry["author"])

    def test_archive_requires_root_dll_and_notices(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp = Path(temp_dir)
            dll = temp / "CsvVisualEditor.dll"
            dll.write_bytes(b"native-aot-test")
            package = temp / "release.zip"
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr("CsvVisualEditor.dll", dll.read_bytes())
                archive.writestr("LICENSE.txt", "GPL")
                archive.writestr("THIRD_PARTY_NOTICES.txt", "third-party")
            zip_hash, dll_hash = release.verify_archive(package, dll, "1.0.1")
            self.assertEqual(64, len(zip_hash))
            self.assertEqual(release.sha256_bytes(dll.read_bytes()), dll_hash)

    def test_nested_dll_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp = Path(temp_dir)
            package = temp / "bad.zip"
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr("CsvVisualEditor/CsvVisualEditor.dll", b"x")
                archive.writestr("LICENSE.txt", "GPL")
                archive.writestr("THIRD_PARTY_NOTICES.txt", "third-party")
            with self.assertRaises(ValueError):
                release.verify_archive(package, None, "1.0.1")

    def test_repository_declares_stable_gpl_release(self):
        project = (ROOT / "src/CsvVisualEditor/CsvVisualEditor.csproj").read_text(encoding="utf-8")
        self.assertIn("<Version>1.0.1</Version>", project)
        self.assertIn("<PackageLicenseExpression>GPL-3.0-only</PackageLicenseExpression>", project)
        workflow = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        self.assertIn("PACKAGE_VERSION: 1.0.1", workflow)
        build = (ROOT / "tools/build-local.ps1").read_text(encoding="utf-8")
        self.assertIn("Join-Path $layout 'CsvVisualEditor.dll'", build)
        self.assertIn("LICENSE.txt", build)
        self.assertIn("THIRD_PARTY_NOTICES.txt", build)
        self.assertNotIn("package/CsvVisualEditor'", build)
        license_text = (ROOT / "LICENSE").read_text(encoding="utf-8")
        self.assertIn("GNU GENERAL PUBLIC LICENSE", license_text)
        notices = (ROOT / "THIRD_PARTY_NOTICES.txt").read_text(encoding="utf-8")
        self.assertIn("Npp.DotNet.Plugin 1.0.0-alpha.10", notices)
        self.assertIn(".NET Runtime / Native AOT 10.0.12", notices)

    def test_shutdown_does_not_double_free_plugin_name(self):
        main = (ROOT / "src/CsvVisualEditor/Main.cs").read_text(encoding="utf-8")
        self.assertIn("PluginData.PluginNamePtr = IntPtr.Zero;", main)
        self.assertNotIn("Marshal.FreeHGlobal(PluginData.PluginNamePtr)", main)

    def test_template_has_required_plugins_admin_fields(self):
        data = json.loads((ROOT / "packaging/nppPluginList/entry.template.json").read_text(encoding="utf-8"))
        for key in ("folder-name", "display-name", "version", "npp-compatible-versions",
                    "id", "repository", "description", "author", "homepage"):
            self.assertIn(key, data)


if __name__ == "__main__":
    unittest.main()
