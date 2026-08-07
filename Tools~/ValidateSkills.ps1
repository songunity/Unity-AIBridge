param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$failures = [System.Collections.Generic.List[string]]::new()
$aibridgeSkillPath = Join-Path $RepoRoot 'Skill~/aibridge/SKILL.md'
$runtimeSkillPath = Join-Path $RepoRoot 'Skill~/aibridge-runtime/SKILL.md'
$installerPath = Join-Path $RepoRoot 'Editor/Utils/SkillInstaller.cs'
$packagePath = Join-Path $RepoRoot 'package.json'

foreach ($path in @($aibridgeSkillPath, $runtimeSkillPath, $installerPath, $packagePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("Missing file: $path")
    }
}

if (Test-Path -LiteralPath $aibridgeSkillPath -PathType Leaf) {
    $aibridgeSkill = Get-Content -LiteralPath $aibridgeSkillPath -Raw
    if ($aibridgeSkill -notmatch '(?m)^name: aibridge\r?$') {
        $failures.Add('aibridge skill name must be aibridge.')
    }
    if ($aibridgeSkill -match '(?m)^\s*AIBridgeCLI runtime\s') {
        $failures.Add('aibridge skill must not document the standalone Runtime CLI surface.')
    }
}

if (Test-Path -LiteralPath $runtimeSkillPath -PathType Leaf) {
    $runtimeSkill = Get-Content -LiteralPath $runtimeSkillPath -Raw
    foreach ($required in @('name: aibridge-runtime', 'runtime status', 'runtime perf', 'runtime ui_snapshot', 'runtime exec --dll')) {
        if ($runtimeSkill -notmatch [regex]::Escape($required)) {
            $failures.Add("Runtime skill missing: $required")
        }
    }
}

if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
    $installer = Get-Content -LiteralPath $installerPath -Raw
    foreach ($skillName in @('aibridge', 'aibridge-runtime')) {
        if ($installer -notmatch [regex]::Escape('"' + $skillName + '"')) {
            $failures.Add("SkillInstaller does not install $skillName.")
        }
    }
}

if (Test-Path -LiteralPath $packagePath -PathType Leaf) {
    $package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
    foreach ($skillFile in @('Skill~/aibridge/SKILL.md', 'Skill~/aibridge-runtime/SKILL.md')) {
        if ($package.files -notcontains $skillFile) {
            $failures.Add("package.json does not include $skillFile.")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'Skill validation passed.'
