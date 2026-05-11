param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Resolve-HooksDir {
    param([string]$ProjectRoot)

    $gitRoot = (& git -C $ProjectRoot rev-parse --show-toplevel).Trim()
    $configuredHooksPath = (& git -C $ProjectRoot config --get core.hooksPath 2>$null)

    if ($configuredHooksPath) {
        $configuredHooksPath = $configuredHooksPath.Trim()
        if ([System.IO.Path]::IsPathRooted($configuredHooksPath)) {
            return $configuredHooksPath
        }
        return (Join-Path $gitRoot $configuredHooksPath)
    }

    $gitCommonDir = (& git -C $ProjectRoot rev-parse --git-common-dir).Trim()
    if (-not [System.IO.Path]::IsPathRooted($gitCommonDir)) {
        $gitCommonDir = Join-Path $gitRoot $gitCommonDir
    }
    return (Join-Path $gitCommonDir "hooks")
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$hooksDir = Resolve-HooksDir -ProjectRoot $projectRoot
$hookPath = Join-Path $hooksDir "post-commit"

$memoryBlock = @'
# BEGIN RogueCard MemPalace commit memory
# Record completed commits into the local MemPalace diary. This hook is best-effort
# so a memory write failure never makes an already-created commit look failed.
if [ -n "${MEMPALACE_PYTHON:-}" ]; then
  "$MEMPALACE_PYTHON" UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 '%s\n' 'MemPalace commit memory failed; commit kept.'
elif [ -f "UnityProject/harness-workspace/mempalace-github-code/.venv/Scripts/python.exe" ]; then
  "UnityProject/harness-workspace/mempalace-github-code/.venv/Scripts/python.exe" UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 '%s\n' 'MemPalace commit memory failed; commit kept.'
elif [ -f "UnityProject/harness-workspace/mempalace-github-code/.venv/bin/python3" ]; then
  "UnityProject/harness-workspace/mempalace-github-code/.venv/bin/python3" UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 '%s\n' 'MemPalace commit memory failed; commit kept.'
elif command -v py >/dev/null 2>&1; then
  py -3 UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 '%s\n' 'MemPalace commit memory failed; commit kept.'
elif command -v python3 >/dev/null 2>&1; then
  python3 UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 '%s\n' 'MemPalace commit memory failed; commit kept.'
elif command -v python >/dev/null 2>&1; then
  python UnityProject/harness-workspace/tools/mempalace_record_event.py git-commit --allow-failure >&2 || printf >&2 '%s\n' 'MemPalace commit memory failed; commit kept.'
else
  printf >&2 '%s\n' 'MemPalace commit memory skipped: no Python launcher found.'
fi
# END RogueCard MemPalace commit memory
'@

New-Item -ItemType Directory -Path $hooksDir -Force | Out-Null

if (Test-Path $hookPath) {
    $existing = Get-Content -Raw -Path $hookPath
    if ($existing -match "BEGIN RogueCard MemPalace commit memory" -or $existing -match "mempalace_record_event\.py git-commit") {
        Write-Host "MemPalace post-commit hook is already installed: $hookPath"
        exit 0
    }

    if ($Force) {
        $backupPath = "$hookPath.bak"
        Copy-Item -Path $hookPath -Destination $backupPath -Force
        Add-Content -Path $hookPath -Value "`n$memoryBlock"
        Write-Host "Appended MemPalace post-commit hook. Backup: $backupPath"
    }
    else {
        Write-Host "Existing post-commit hook found: $hookPath"
        Write-Host "Re-run with -Force to append the MemPalace memory block."
        exit 1
    }
}
else {
    $content = "#!/bin/sh`n`n$memoryBlock`n"
    Set-Content -Path $hookPath -Value $content -NoNewline -Encoding UTF8
    Write-Host "Installed MemPalace post-commit hook: $hookPath"
}
