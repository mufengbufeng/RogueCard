

## UnitySkills
- unity-skills: Unity Editor automation via REST API

## MemPalace lifecycle memory
- Git commits SHOULD trigger a best-effort local MemPalace diary write via `harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure`.
- If the local hook is missing, install or repair it with `powershell -ExecutionPolicy Bypass -File harness-workspace/tools/install-mempalace-git-hooks.ps1 -Force` on Windows, or `sh harness-workspace/tools/install-mempalace-git-hooks.sh` on POSIX shells.
- `$openspec-archive-change` MUST run the skill's MemPalace archive-recording step after a successful archive and before the final response.
- MemPalace write failures are warnings only; they must not roll back a git commit or an OpenSpec archive.
