param(
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode'
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-ProjectRoot
$results = Join-Path $root "Logs\test-results-$Platform.xml"
$log = Join-Path $root "Logs\test-$Platform.log"
Remove-Item $results -ErrorAction SilentlyContinue

$exit = Invoke-Unity -Root $root -LogFile $log -Arguments @('-runTests', '-testPlatform', $Platform, '-testResults', "`"$results`"")

if (Test-Path $results) {
    $run = ([xml](Get-Content $results)).'test-run'
    Write-Host "$Platform tests: total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped)"
}
else {
    Write-Host "No results file. See $log"
}

exit $exit
