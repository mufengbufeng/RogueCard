#!/bin/sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
project_root=$(CDPATH= cd -- "$script_dir/../.." && pwd -P)
git_root=$(git -C "$project_root" rev-parse --show-toplevel)
configured_hooks_path=$(git -C "$project_root" config --get core.hooksPath || true)

if [ -n "$configured_hooks_path" ]; then
  case "$configured_hooks_path" in
    /*) hooks_dir="$configured_hooks_path" ;;
    *) hooks_dir="$git_root/$configured_hooks_path" ;;
  esac
else
  git_common_dir=$(git -C "$project_root" rev-parse --git-common-dir)
  case "$git_common_dir" in
    /*) hooks_dir="$git_common_dir/hooks" ;;
    *) hooks_dir="$git_root/$git_common_dir/hooks" ;;
  esac
fi

hook_path="$hooks_dir/post-commit"
mkdir -p "$hooks_dir"

memory_block='
# BEGIN RogueCard MemPalace commit memory
# Record completed commits into the local MemPalace diary. This hook is best-effort
# so a memory write failure never makes an already-created commit look failed.
if [ -n "${MEMPALACE_PYTHON:-}" ]; then
  "$MEMPALACE_PYTHON" UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 "%s\n" "MemPalace commit memory failed; commit kept."
elif [ -f "UnityProject/harness-workspace/mempalace-github-code/.venv/Scripts/python.exe" ]; then
  "UnityProject/harness-workspace/mempalace-github-code/.venv/Scripts/python.exe" UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 "%s\n" "MemPalace commit memory failed; commit kept."
elif [ -f "UnityProject/harness-workspace/mempalace-github-code/.venv/bin/python3" ]; then
  "UnityProject/harness-workspace/mempalace-github-code/.venv/bin/python3" UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 "%s\n" "MemPalace commit memory failed; commit kept."
elif command -v py >/dev/null 2>&1; then
  py -3 UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 "%s\n" "MemPalace commit memory failed; commit kept."
elif command -v python3 >/dev/null 2>&1; then
  python3 UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 "%s\n" "MemPalace commit memory failed; commit kept."
elif command -v python >/dev/null 2>&1; then
  python UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 "%s\n" "MemPalace commit memory failed; commit kept."
else
  printf >&2 "%s\n" "MemPalace commit memory skipped: no Python launcher found."
fi
# END RogueCard MemPalace commit memory
'

if [ -f "$hook_path" ] && { grep -q "BEGIN RogueCard MemPalace commit memory" "$hook_path" || grep -q "mempalace_record_event.py git-commit" "$hook_path"; }; then
  printf '%s\n' "MemPalace post-commit hook is already installed: $hook_path"
  exit 0
fi

if [ -f "$hook_path" ]; then
  cp "$hook_path" "$hook_path.bak"
  printf '\n%s\n' "$memory_block" >> "$hook_path"
  printf '%s\n' "Appended MemPalace post-commit hook. Backup: $hook_path.bak"
else
  {
    printf '%s\n\n' '#!/bin/sh'
    printf '%s\n' "$memory_block"
  } > "$hook_path"
  printf '%s\n' "Installed MemPalace post-commit hook: $hook_path"
fi

chmod +x "$hook_path"
