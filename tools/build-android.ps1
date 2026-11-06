param(
    [string]$OutputPath = 'Builds\Android\projectBankai.apk',
    [switch]$Development,
    [switch]$Install,
    [string]$Serial
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-ProjectRoot
$apk = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $root $OutputPath }
$log = Join-Path $root 'Logs\build-android.log'

$arguments = @('-executeMethod', 'Game.Editor.BuildScript.BuildAndroid', '-outputPath', "`"$apk`"")
if ($Development) { $arguments += '-development' }

$exit = Invoke-Unity -Root $root -LogFile $log -Arguments $arguments
if ($exit -ne 0 -or -not (Test-Path $apk)) {
    Write-Host "Build failed (exit $exit). See $log"
    exit 1
}

Write-Host "Built $apk"

if ($Install) {
    $adb = (Get-Command adb -ErrorAction SilentlyContinue).Source
    if (-not $adb) {
        Write-Host 'adb not found on PATH.'
        exit 1
    }

    $target = if ($Serial) { @('-s', $Serial) } else { @() }
    & $adb @target install -r $apk
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'adb install failed. Check the phone is connected and USB debugging is allowed (adb devices).'
        exit 1
    }

    & $adb @target shell monkey -p com.projectbankai.game -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Host 'Installed and launched.'
}
