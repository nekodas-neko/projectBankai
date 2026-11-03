param(
    [string]$File = 'tuning.txt',
    [switch]$Clear,
    [switch]$Show
)

# Pushes a tuning file to a phone running a development build, or removes it, then restarts the game.
# The file format and examples are in docs/tuning.md.

$package = 'com.projectbankai.game'
$destination = "/sdcard/Android/data/$package/files/tuning.txt"

if ($Show) {
    adb shell cat $destination
    exit $LASTEXITCODE
}

if ($Clear) {
    adb shell rm -f $destination
    Write-Host 'Tuning file removed from the phone.'
}
else {
    if (-not (Test-Path $File)) {
        throw "No file at $File. Copy docs/tuning.example.txt and edit it."
    }

    adb push $File $destination
    if ($LASTEXITCODE -ne 0) {
        throw 'adb push failed. Is the phone connected, and has the game been run once so its folder exists?'
    }

    Write-Host "Pushed $File. The build label in the corner shows 'tuning: N overrides' when it is applied."
}

adb shell am force-stop $package
adb shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Null
