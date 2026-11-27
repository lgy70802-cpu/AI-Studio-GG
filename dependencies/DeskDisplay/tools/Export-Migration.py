"""Create and verify a source/data handoff ZIP; no repository or user settings mutations."""
import hashlib
import json
import os
from pathlib import Path
import sys
import zipfile

repo = Path(__file__).resolve().parent.parent
destination = Path(sys.argv[1]).resolve()
if destination.exists():
    raise SystemExit("Destination already exists; choose a new archive name.")
excluded = {".git", "bin", "obj", "node_modules", "__pycache__", ".vs"}
files = []
for folder, dirs, names in os.walk(repo):
    base = Path(folder)
    dirs[:] = [d for d in dirs if d not in excluded and not (base == repo / "artifacts" and d == "migration")]
    for name in names:
        source = base / name
        if source == destination or name == ".migration-manifest.json" or name.endswith((".pyc", ".suo", ".user")):
            continue
        if source.is_symlink():
            raise SystemExit(f"Review symbolic link before packaging: {source}")
        files.append((source.relative_to(repo).as_posix(), source))
data = Path(os.environ["APPDATA"]) / "DeskDisplay"
if data.exists():
    files.extend(("migration/DeskDisplay-data/" + p.relative_to(data).as_posix(), p) for p in data.rglob("*") if p.is_file())
manifest = {"PreparedDate": "2026-10-08", "GitHistory": "No commits at source; .git excluded", "Files": []}
destination.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(destination, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for relative, source in sorted(files):
        payload = source.read_bytes()
        archive.writestr("MLB/" + relative, payload)
        manifest["Files"].append({"Path": relative, "Bytes": len(payload), "Sha256": hashlib.sha256(payload).hexdigest()})
    archive.writestr("MLB/.migration-manifest.json", json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8"))
with zipfile.ZipFile(destination) as archive:
    for entry in manifest["Files"]:
        actual = hashlib.sha256(archive.read("MLB/" + entry["Path"])).hexdigest()
        if actual != entry["Sha256"]:
            raise SystemExit(f"Archive verification failed: {entry['Path']}")
digest = hashlib.sha256(destination.read_bytes()).hexdigest()
destination.with_suffix(".zip.sha256").write_text(f"{digest}  {destination.name}\n", encoding="utf-8")
print(json.dumps({"archive": str(destination), "files": len(files), "bytes": destination.stat().st_size, "sha256": digest, "verified": True}))
