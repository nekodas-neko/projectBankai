# Plays every weapon against every boss with bots (stand still, stand and block, kite) on the shipped numbers,
# plus tuning.txt if there is one, and prints a table of who survives, how fast, and how much damage was taken and dealt.
# Needs Unity (batch mode), so close the editor first. See docs/tuning.md to change numbers and run it again.

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-ProjectRoot
$report = Join-Path $root 'Logs\balance-report.txt'
Remove-Item $report -ErrorAction SilentlyContinue

& (Join-Path $PSScriptRoot 'run-tests.ps1') -Filter 'Game.Client.Tests.ShippedBalanceTests.Report' | Out-Null

if (Test-Path $report) {
    Get-Content $report
}
else {
    Write-Host "No report was written. See Logs\test-EditMode.log"
    exit 1
}
