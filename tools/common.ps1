function Get-ProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Get-UnityEditorPath {
    param([string]$Root)

    if ($env:UNITY_PATH -and (Test-Path $env:UNITY_PATH)) {
        return $env:UNITY_PATH
    }

    $match = Select-String -Path (Join-Path $Root 'ProjectSettings\ProjectVersion.txt') -Pattern 'm_EditorVersion:\s*(\S+)'
    $version = $match.Matches[0].Groups[1].Value
    $path = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (-not (Test-Path $path)) {
        throw "Unity $version not found at $path. Install it from Unity Hub or set UNITY_PATH to Unity.exe."
    }
    return $path
}

function Invoke-Unity {
    param([string]$Root, [string[]]$Arguments, [string]$LogFile)

    $unity = Get-UnityEditorPath -Root $Root
    New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
    $all = @('-batchmode', '-nographics', '-projectPath', "`"$Root`"", '-logFile', "`"$LogFile`"") + $Arguments
    $process = Start-Process -FilePath $unity -ArgumentList $all -Wait -PassThru
    return $process.ExitCode
}
