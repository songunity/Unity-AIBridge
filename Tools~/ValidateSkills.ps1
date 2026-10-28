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
    # 从源码属性检查目录，不另维护一份命令白名单。Editor 集成测试再与真实注册表交叉验证。
    $expectedCommands = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    [void]$expectedCommands.Add('Compile')
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'Editor/Commands') -Filter '*.cs') {
        $source = Get-Content -LiteralPath $file.FullName -Raw
        $class = [regex]::Match($source, 'public\s+(?:(?:static|partial)\s+)*class\s+(\w+)').Groups[1].Value
        if ($class -in @('RuntimeCommand', 'RuntimeExecuteCommand')) { continue }
        foreach ($method in [regex]::Matches($source, '\[AIBridge\((?<args>[\s\S]*?)\)\]\s*public\s+static\s+IEnumerator\s+(?<method>\w+)\s*\(')) {
            $arguments = $method.Groups['args'].Value
            if ($arguments -match 'exposeToSkill\s*:\s*false') { continue }
            $name = [regex]::Match($arguments, '\bname\s*:\s*"([^"]+)"').Groups[1].Value
            if (-not $name) {
                $strings = [regex]::Matches($arguments, '@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*"')
                if ($strings.Count -ge 3) { $name = $strings[2].Value.Trim('"') }
            }
            if (-not $name) { $name = $class + '_' + $method.Groups['method'].Value }
            [void]$expectedCommands.Add($name)
        }
    }
    $section = [regex]::Match($aibridgeSkill, '(?s)<!-- AUTO-GENERATED-COMMANDS-START -->(.*?)<!-- AUTO-GENERATED-COMMANDS-END -->').Groups[1].Value
    $documented = @([regex]::Matches($section, '\*\*([A-Za-z][A-Za-z0-9_]*)\*\*') | ForEach-Object { $_.Groups[1].Value })
    foreach ($name in $expectedCommands) {
        if ($name -notin $documented) { $failures.Add("Skill command catalog missing: $name") }
    }
    foreach ($name in $documented) {
        if (-not $expectedCommands.Contains($name)) { $failures.Add("Skill command catalog contains obsolete command: $name") }
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

foreach ($name in @('unity-profiler', 'frame-debugger', 'memory-profiler', 'yooasset-debugger')) {
    $directory = Join-Path $RepoRoot "Skill~/$name"
    if (-not (Test-Path (Join-Path $directory 'SKILL.md'))) { throw "Missing diagnostic skill: $name" }
    if (-not (Get-ChildItem (Join-Path $directory 'scripts') -Filter '*.cs')) { throw "Missing diagnostic scripts: $name" }
    if ($installer -notmatch [regex]::Escape('"' + $name + '"')) { throw "Installer missing diagnostic skill: $name" }
    if ($package.files -notcontains "Skill~/$name") { throw "Package missing diagnostic skill: $name" }
}
foreach ($file in @('run.py', '_common.cs')) {
    if (-not (Test-Path (Join-Path $RepoRoot "Skill~/diagnostics-common/$file"))) { throw "Missing diagnostic support: $file" }
}
if ($package.files -notcontains 'Skill~/diagnostics-common') { throw 'Package missing shared diagnostic support' }

Write-Output 'Skill validation passed.'
