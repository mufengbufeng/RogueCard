#!/usr/bin/env python3
"""Record project lifecycle events into the local MemPalace diary."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from datetime import datetime
from pathlib import Path


def script_dir() -> Path:
    return Path(__file__).resolve().parent


def workspace_root() -> Path:
    return script_dir().parent


def project_root() -> Path:
    return workspace_root().parent


def mempalace_repo() -> Path:
    return workspace_root() / "mempalace-github-code"


def palace_root() -> Path:
    return workspace_root() / ".mempalace_local" / "palace"


def resolve_active_palace(root: Path) -> Path:
    pointer = root / "current.json"
    if not pointer.is_file():
        return root

    payload = json.loads(pointer.read_text(encoding="utf-8"))
    raw_path = payload.get("active_relative_path") or payload.get("active_path")
    if not raw_path:
        return root

    active = Path(raw_path)
    if not active.is_absolute():
        active = root / active
    return active


def resolve_python() -> Path:
    env_python = os.environ.get("MEMPALACE_PYTHON")
    if env_python:
        return Path(env_python)

    candidates = [
        mempalace_repo() / ".venv" / "Scripts" / "python.exe",
        mempalace_repo() / ".venv" / "bin" / "python3",
        mempalace_repo() / ".venv" / "bin" / "python",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("MemPalace venv python not found. Run mempalace setup first.")


def run_git(args: list[str]) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=project_root(),
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return result.stdout.strip()


def commit_entry() -> tuple[str, str]:
    sha = run_git(["rev-parse", "--short", "HEAD"]) or "unknown"
    subject = run_git(["log", "-1", "--pretty=%s"]) or "(no subject)"
    body = run_git(["log", "-1", "--pretty=%b"])
    stats = run_git(["show", "--stat", "--oneline", "--decorate=short", "--no-renames", "--format=short", "HEAD"])

    lines = [
        f"Git commit recorded for RogueCard.",
        f"- Commit: {sha}",
        f"- Subject: {subject}",
    ]
    if body:
        lines.append(f"- Body: {body}")
    if stats:
        lines.extend(["", "Changed files summary:", stats])
    return "git-commit", "\n".join(lines)


def openspec_archive_entry(args: argparse.Namespace) -> tuple[str, str]:
    change = args.change or os.environ.get("OPENSPEC_CHANGE") or "unknown"
    archive_path = args.archive_path or os.environ.get("OPENSPEC_ARCHIVE_PATH") or ""
    schema = args.schema or os.environ.get("OPENSPEC_SCHEMA") or ""
    specs = args.specs or os.environ.get("OPENSPEC_SPECS") or ""

    lines = [
        "OpenSpec change archived for RogueCard.",
        f"- Change: {change}",
    ]
    if schema:
        lines.append(f"- Schema: {schema}")
    if archive_path:
        lines.append(f"- Archive path: {archive_path}")
    if specs:
        lines.append(f"- Specs: {specs}")
    return "openspec-archive", "\n".join(lines)


def write_diary(topic: str, entry: str, agent_name: str) -> int:
    python_exe = resolve_python()
    active_palace = resolve_active_palace(palace_root())
    active_palace.mkdir(parents=True, exist_ok=True)

    code = (
        "from datetime import datetime\n"
        "import hashlib, json, sys\n"
        "from mempalace.backends.chroma import ChromaBackend\n"
        "from mempalace.config import sanitize_content, sanitize_name\n"
        "agent_name = sys.argv[1]\n"
        "topic = sys.argv[2]\n"
        "palace_path = sys.argv[3]\n"
        "entry = sys.stdin.read()\n"
        "agent_name = sanitize_name(agent_name, 'agent_name')\n"
        "entry = sanitize_content(entry)\n"
        "wing = f\"wing_{agent_name.lower().replace(' ', '_')}\"\n"
        "now = datetime.now()\n"
        "entry_id = f\"diary_{wing}_{now.strftime('%Y%m%d_%H%M%S%f')}_{hashlib.sha256(entry.encode()).hexdigest()[:12]}\"\n"
        "col = ChromaBackend().get_collection(palace_path, 'mempalace_drawers', create=True)\n"
        "col.add(ids=[entry_id], documents=[entry], metadatas=[{\n"
        "    'wing': wing,\n"
        "    'room': 'diary',\n"
        "    'hall': 'hall_diary',\n"
        "    'topic': topic,\n"
        "    'type': 'diary_entry',\n"
        "    'agent': agent_name,\n"
        "    'filed_at': now.isoformat(),\n"
        "    'date': now.strftime('%Y-%m-%d'),\n"
        "    'source_file': 'mempalace_record_event.py',\n"
        "}])\n"
        "result = {'success': True, 'entry_id': entry_id, 'agent': agent_name, 'topic': topic, 'timestamp': now.isoformat()}\n"
        "print(json.dumps(result, ensure_ascii=False))\n"
    )
    env = os.environ.copy()
    env["MEMPALACE_PALACE_PATH"] = str(active_palace)
    env.setdefault("PYTHONUTF8", "1")
    env.setdefault("PYTHONIOENCODING", "utf-8")

    result = subprocess.run(
        [str(python_exe), "-c", code, agent_name, topic, str(active_palace)],
        cwd=mempalace_repo(),
        input=entry,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env,
        check=False,
    )
    if result.returncode != 0:
        sys.stderr.write(result.stderr or result.stdout)
    return result.returncode


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("event", choices=["git-commit", "openspec-archive"])
    parser.add_argument("--agent-name", default=os.environ.get("MEMPALACE_AGENT_NAME", "codex"))
    parser.add_argument("--change", default="")
    parser.add_argument("--archive-path", default="")
    parser.add_argument("--schema", default="")
    parser.add_argument("--specs", default="")
    parser.add_argument("--allow-failure", action="store_true")
    args = parser.parse_args(argv)

    if args.event == "git-commit":
        topic, entry = commit_entry()
    else:
        topic, entry = openspec_archive_entry(args)

    entry = f"{entry}\n- Recorded at: {datetime.now().isoformat(timespec='seconds')}\n"
    exit_code = write_diary(topic, entry, args.agent_name)
    if exit_code != 0 and args.allow_failure:
        print("MemPalace event recording failed; continuing because --allow-failure is set.", file=sys.stderr)
        return 0
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
