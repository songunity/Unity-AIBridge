[CmdletBinding()]
param(
    [switch]$IncludeEditor,
    [string]$UnityPath,
    [ValidateRange(30, 900)][int]$StartupTimeoutSeconds = 300
)

# 默认只跑快速检查；Editor 测试仅使用仓库 Temp 中的隔离工程。
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$testRoot = Join-Path $repoRoot 'Temp/protocol-validation'
$projectRoot = Join-Path $testRoot 'UnityProject'
$runRoot = Join-Path $testRoot ('runs/' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$summary = [ordered]@{ passed = $false; editorExecuted = $false; phases = @(); error = $null }
$ownedUnity = $null

function Assert-TestPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($testRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "目标不在隔离测试目录中：$full"
    }
    # 不允许目录联接把覆盖操作指向其他工程。
    $cursor = $full
    while ($cursor -and $cursor -ne $repoRoot) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "隔离路径包含链接：$cursor" }
        }
        $cursor = Split-Path -Parent $cursor
    }
}

function Invoke-TestStep([string]$Name, [string]$Command, [string[]]$Arguments) {
    $log = Join-Path $runRoot ($Name + '.log')
    Write-Host "执行 $Name"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & $Command @Arguments *> $log
    $code = $LASTEXITCODE
    $summary.phases += [ordered]@{ name = $Name; exitCode = $code; durationMs = $timer.ElapsedMilliseconds; log = $log }
    if ($code -ne 0) {
        Get-Content -LiteralPath $log -Tail 12 | Write-Host
        throw "$Name 失败，退出码 $code，日志：$log"
    }
}

Assert-TestPath $runRoot
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
Push-Location $repoRoot
try {
    Get-Command dotnet -ErrorAction Stop | Out-Null
    Invoke-TestStep 'cli-tests' 'dotnet' @('test', 'Tools~/AIBridgeCLI/AIBridge.Test/AIBridge.Test.csproj', '--nologo', '--logger', 'trx', '--results-directory', $runRoot)
    & (Join-Path $PSScriptRoot 'ValidateSkills.ps1')
    $summary.phases += [ordered]@{ name = 'skills'; exitCode = 0 }
    Invoke-TestStep 'diff-check' 'git' @('diff', '--check')

    if ($IncludeEditor) {
        if ($env:OS -ne 'Windows_NT') { throw '隔离 Editor 入口目前仅支持 Windows。' }
        Get-Command python -ErrorAction Stop | Out-Null
        Assert-TestPath $projectRoot
        $unityProcesses = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
        foreach ($process in $unityProcesses) {
            if ($process.CommandLine -match '(?i)-projectpath\s+(?:"([^"]+)"|([^\s]+))') {
                $runningProject = if ($Matches[1]) { $Matches[1] } else { $Matches[2] }
                if ([IO.Path]::GetFullPath($runningProject).TrimEnd('\', '/') -eq $projectRoot.TrimEnd('\', '/')) {
                    throw "隔离工程已在运行（PID $($process.ProcessId)）；先关闭该隔离实例后再测试。"
                }
            }
        }
        if (-not $UnityPath) { $UnityPath = $env:UNITY_EDITOR_PATH }
        if (-not $UnityPath) {
            $candidates = @($unityProcesses.ExecutablePath | Where-Object { $_ } | Sort-Object -Unique)
            if ($candidates.Count -eq 1) { $UnityPath = $candidates[0] }
        }
        if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
            throw '指定 -UnityPath <Unity.exe>，或设置 UNITY_EDITOR_PATH；也可自动复用当前唯一运行的 Unity 安装路径。'
        }
        $UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
        $summary.unityPath = $UnityPath
        Write-Host '准备隔离工程与仓库包副本'
        foreach ($directory in @('Assets', 'ProjectSettings', 'Packages', '.aibridge/cli')) {
            New-Item -ItemType Directory -Path (Join-Path $projectRoot $directory) -Force | Out-Null
        }
        $versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
        if (-not (Test-Path -LiteralPath $versionFile)) {
            $productVersion = (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion
            if ($productVersion -notmatch '^(\d+\.\d+\.\d+[abfp]\d+)') { throw '无法从 Unity.exe 读取工程版本。' }
            # 提前标明版本，避免首次启动额外安装默认广告、购买和 IDE 包。
            [IO.File]::WriteAllText($versionFile, "m_EditorVersion: $($Matches[1])`n")
        }
        $packageCopy = Join-Path $projectRoot 'Packages/AIBridge'
        Assert-TestPath $packageCopy
        New-Item -ItemType Directory -Path $packageCopy -Force | Out-Null
        if (Get-ChildItem -LiteralPath $packageCopy -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
            throw '隔离包副本包含链接，拒绝覆盖。'
        }
        foreach ($directory in @('Editor', 'Runtime', 'Shared', 'Skill~')) {
            $sourceDirectory = Join-Path $repoRoot $directory
            $targetDirectory = Join-Path $packageCopy $directory
            if (Test-Path -LiteralPath $targetDirectory) {
                foreach ($file in Get-ChildItem -LiteralPath $targetDirectory -Recurse -File) {
                    $relative = $file.FullName.Substring($targetDirectory.Length).TrimStart('\', '/')
                    if ($file.Extension -ne '.meta' -and -not (Test-Path -LiteralPath (Join-Path $sourceDirectory $relative))) {
                        Assert-TestPath $file.FullName
                        Remove-Item -LiteralPath $file.FullName -Force
                    }
                }
            }
            Copy-Item -LiteralPath $sourceDirectory -Destination $packageCopy -Recurse -Force
        }
        Copy-Item -LiteralPath (Join-Path $repoRoot 'package.json') -Destination $packageCopy -Force
        $dependencies = [ordered]@{ 'com.sh.aibridge' = 'file:AIBridge'; 'com.unity.ugui' = '1.0.0'; 'com.unity.test-framework' = '1.1.33' }
        # 与 Unity 2022.3 空工程一致的内置模块，不读取业务项目 manifest。
        foreach ($module in @('ai','androidjni','animation','assetbundle','audio','cloth','director','imageconversion','imgui','jsonserialize','particlesystem','physics','physics2d','screencapture','terrain','terrainphysics','tilemap','ui','uielements','umbra','unityanalytics','unitywebrequest','unitywebrequestassetbundle','unitywebrequestaudio','unitywebrequesttexture','unitywebrequestwww','vehicles','video','vr','wind','xr')) {
            $dependencies['com.unity.modules.' + $module] = '1.0.0'
        }
        [IO.File]::WriteAllText((Join-Path $projectRoot 'Packages/manifest.json'), (@{dependencies = $dependencies} | ConvertTo-Json -Depth 5))
        $cli = Join-Path $projectRoot '.aibridge/cli/AIBridgeCLI.exe'
        Invoke-TestStep 'publish-win-x64' 'dotnet' @('publish', 'Tools~/AIBridgeCLI/AIBridgeCLI/AIBridgeCLI.csproj', '-c', 'Release', '-r', 'win-x64', '-o', (Split-Path -Parent $cli), '--nologo')
        $summary.cliSha256 = (Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash
        $editorLog = Join-Path $runRoot 'editor.log'
        Write-Host '启动隔离 Unity，等待编译和 Bridge 就绪'
        $ownedUnity = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + $editorLog + '"')) -WindowStyle Hidden -PassThru
        $summary.editorPid = $ownedUnity.Id
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $ready = $false
        $firstHeartbeatUtc = $null
        while ($timer.Elapsed.TotalSeconds -lt $StartupTimeoutSeconds) {
            if ($ownedUnity.HasExited) { throw "隔离 Unity 提前退出（$($ownedUnity.ExitCode)），见 $editorLog" }
            $metadataPath = Join-Path $projectRoot '.aibridge/editor-instance.json'
            if (Test-Path -LiteralPath $metadataPath) {
                try {
                    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
                    if ($metadata.processId -eq $ownedUnity.Id -and $metadata.protocolVersion -eq 2) {
                        if ($firstHeartbeatUtc -and $metadata.lastUpdatedUtc -ne $firstHeartbeatUtc) { $ready = $true; break }
                        $firstHeartbeatUtc = $metadata.lastUpdatedUtc
                    }
                } catch { } # 启动期间只接受完整元数据，未就绪继续有界等待。
            }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) { throw "Unity 就绪超时，见 $editorLog" }
        $summary.phases += [ordered]@{ name = 'editor-startup'; exitCode = 0; durationMs = $timer.ElapsedMilliseconds; log = $editorLog }
        Invoke-TestStep 'editor-protocol' 'python' @('-X', 'utf8', '-B', 'Tools~/ValidateEditorProtocol.py', '--report', (Join-Path $runRoot 'editor-results.json'))
        $summary.editorExecuted = $true
    }
    $summary.passed = $true
} catch {
    $summary.error = $_.Exception.Message
    Write-Host $summary.error
} finally {
    if ($ownedUnity -and -not $ownedUnity.HasExited) {
        try {
            & $cli CodeExecuteCommand_Execute --code 'UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.Exit(0); return "exit requested";' --no-wait --raw *> (Join-Path $runRoot 'editor-shutdown.log')
            $summary.editorClosed = $ownedUnity.WaitForExit(10000)
        } catch { $summary.editorClosed = $false }
        if (-not $summary.editorClosed) {
            $summary.passed = $false
            $summary.cleanupError = "隔离 Unity 未正常关闭（PID $($ownedUnity.Id)）；保留日志，手动关闭该隔离实例。"
            Write-Host $summary.cleanupError
        }
    }
    if ($ownedUnity -and $ownedUnity.HasExited) { $summary.editorClosed = $true }
    [IO.File]::WriteAllText((Join-Path $runRoot 'summary.json'), ($summary | ConvertTo-Json -Depth 8))
    Pop-Location
}
Write-Host "测试报告：$runRoot"
if (-not $summary.passed) { exit 1 }
Write-Host '测试通过'
