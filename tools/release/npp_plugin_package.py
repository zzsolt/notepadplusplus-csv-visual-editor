"""Validate the stable ZIP and generate the Notepad++ Plugins Admin x64 entry."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import zipfile

PLUGIN_NAME = "CsvVisualEditor"
DISPLAY_NAME = "CSV Visual Editor"
AUTHOR = "Zolnai Zsolt"
HOMEPAGE = "https://github.com/zzsolt/notepadplusplus-csv-visual-editor"
MIN_NPP_VERSION = "8.9.8"
DESCRIPTION = (
    "View, edit, filter and inspect CSV data in a docked table with "
    "conflict-checked Apply and Notepad++ undo."
)
STABLE_VERSION = re.compile(r"^\d+\.\d+\.\d+$")
REQUIRED_ROOT_FILES = {
    f"{PLUGIN_NAME}.dll",
    "LICENSE.txt",
    "THIRD_PARTY_NOTICES.txt",
}


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_version(version: str) -> None:
    if not STABLE_VERSION.fullmatch(version):
        raise ValueError("Release version must be a stable three-component version such as 1.0.0.")


def release_url(version: str) -> str:
    return (
        f"{HOMEPAGE}/releases/download/v{version}/"
        f"{PLUGIN_NAME}-{version}-win-x64.zip"
    )


def plugin_admin_entry(version: str, archive_sha256: str) -> dict[str, str]:
    validate_version(version)
    if not re.fullmatch(r"[0-9a-f]{64}", archive_sha256):
        raise ValueError("Archive SHA-256 must be 64 lowercase hexadecimal characters.")
    return {
        "folder-name": PLUGIN_NAME,
        "display-name": DISPLAY_NAME,
        "version": version,
        "npp-compatible-versions": f"[{MIN_NPP_VERSION},]",
        "id": archive_sha256,
        "repository": release_url(version),
        "description": DESCRIPTION,
        "author": AUTHOR,
        "homepage": HOMEPAGE,
    }


def verify_archive(zip_path: Path, dll_path: Path | None, version: str) -> tuple[str, str]:
    validate_version(version)
    if not zip_path.is_file():
        raise ValueError(f"Release ZIP does not exist: {zip_path}")
    with zipfile.ZipFile(zip_path) as archive:
        infos = archive.infolist()
        names = [info.filename.replace("\\", "/") for info in infos if not info.is_dir()]
        if len(names) != len(set(names)):
            raise ValueError("Release ZIP contains duplicate file names.")
        for name in names:
            pure = PurePosixPath(name)
            if pure.is_absolute() or ".." in pure.parts:
                raise ValueError(f"Unsafe ZIP path: {name}")
        missing = REQUIRED_ROOT_FILES.difference(names)
        if missing:
            raise ValueError(f"Plugins Admin package is missing root files: {sorted(missing)}")
        nested_plugin = [name for name in names if name.endswith(f"/{PLUGIN_NAME}.dll")]
        if nested_plugin:
            raise ValueError("Plugin DLL must be at the ZIP root, not inside an outer plugin folder.")
        dll_bytes = archive.read(f"{PLUGIN_NAME}.dll")
    if dll_path is not None:
        if not dll_path.is_file():
            raise ValueError(f"Published DLL does not exist: {dll_path}")
        if dll_bytes != dll_path.read_bytes():
            raise ValueError("ZIP DLL differs from the production DLL that passed host validation.")
    return sha256_file(zip_path), sha256_bytes(dll_bytes)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--zip", type=Path, required=True)
    parser.add_argument("--dll", type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--entry-out", type=Path, required=True)
    parser.add_argument("--manifest-out", type=Path, required=True)
    args = parser.parse_args()

    archive_hash, dll_hash = verify_archive(args.zip, args.dll, args.version)
    entry = plugin_admin_entry(args.version, archive_hash)
    args.entry_out.write_text(json.dumps(entry, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    manifest = {
        "version": args.version,
        "zip": args.zip.name,
        "zipSha256": archive_hash,
        "dll": f"{PLUGIN_NAME}.dll",
        "dllSha256": dll_hash,
        "pluginAdminList": "src/pl.x64.json",
        "pluginAdminEntry": entry,
    }
    args.manifest_out.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"Plugins Admin package PASS: {args.zip.name} sha256={archive_hash}")


if __name__ == "__main__":
    main()
