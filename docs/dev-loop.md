# Dev loop and test loop

Two loops, used for different jobs.

- Dev loop (daily, fast): run the game in the Unity editor and stream it to the phone with Unity Remote. No build or install.
- Test loop (per milestone and per merge): a packaged build on the phone, for performance, real touch feel, memory and platform quirks.

## Dev loop: Unity Remote (Android)

Source: Unity 6.3 manual, "Unity Remote for Android".

1. Install **Unity Remote** from Google Play (`com.unity3d.mobileremote`).
2. Turn on USB debugging on the phone and connect it by USB. Accept the "Allow USB debugging" prompt.
3. Check `adb devices` lists the phone as `device`.
4. Open the app on the phone.
5. In the editor: **Edit > Project Settings > Editor > Unity Remote**. Set **Device** to the phone.
6. Press Play. The Game view streams to the phone and touch input streams back.

If the phone does not appear in the Device list, reconnect it. If it still does not, run a build and install (below) to confirm the USB connection works.

### Limits
- The editor runs the game on the PC. Performance, memory and frame rate are the PC's, not the phone's.
- The Game view is streamed as a compressed image: lower quality, some latency. In the Unity Remote section, **Compression: PNG** and **Resolution: Normal** look better but use more bandwidth.
- Only one connected Android device is supported. Unity picks the first one it finds.
- Streamed inputs listed by Unity: touch and stylus, accelerometer, gyroscope, camera, compass, GPS, joystick.
- Not yet checked: whether on-screen stick and buttons built on the Input System work through Unity Remote. This is tested when the first on-screen controls land (issue #13). If they do not work well, the fallback is a quick Build and Run to the phone with `tools/build-android.ps1 -Development -Install`.
- iOS has its own requirements and is covered in `docs/ios-setup.md` later.

## Test loop: packaged Android build

```powershell
tools\build-android.ps1 -Development -Install
```

Builds a portrait ARM64 APK to `Builds\Android\projectBankai.apk` and runs `adb install -r`, then launches the app. Add `-Serial <id>` when more than one device is attached. The first build takes several minutes while Unity compiles shaders and Gradle resolves dependencies.

Wireless debugging works too: pair once with `adb pair <ip:port>`, then `adb connect <ip:port>`. After that the same command installs over Wi-Fi.

Requirements: the Unity version in `ProjectSettings/ProjectVersion.txt` installed with Android Build Support, and `adb` on PATH. Set `UNITY_PATH` to `Unity.exe` if Unity is installed elsewhere.

Builds use Unity's debug keystore. The release keystore never goes in the repo.

Every build shows its version in the bottom-left corner: `b<commit count>-<short hash>`. Quote it in bug reports. In the editor, the number is written when Play starts.

## Playing together on a local network

Nothing goes on the internet. One device hosts and others join over the same Wi-Fi.

1. On the host: tap **Session**, then **Host on Wi-Fi**. The menu shows the host's address, for example `192.168.68.78:47777`, and how many players are in.
2. On each joining device: tap **Session**. Hosts on the same Wi-Fi appear under "Sessions found"; tap one to join. If none appear, type the host's address into the box and tap **Join**.
3. Pick your class with **Class** and skin with **Skin** before joining; the host checks the class exists and sends everyone your skin id.
4. Up to four players. The host runs the game; a player who stays silent for 8 seconds is dropped.
5. **Solo (offline)** in the same menu goes back to playing alone.

Notes:
- A phone hosting works with no setup. A **Windows PC hosting** can be blocked by Windows Firewall when the Wi-Fi is set to a **Public** network: Unity's firewall rules block incoming connections on Public networks, so phones cannot find or join it. Set the Wi-Fi's network profile to **Private** in Windows network settings, or let a phone host.
- Everyone must be on the same Wi-Fi (or a private network such as Tailscale). Some routers isolate wireless devices from each other ("AP isolation"); turn that off or use a different network.
- Anyone on the same network can join; there is no password. Do not host on public Wi-Fi.
- Ports: UDP 47777.

## Tests

```powershell
tools\run-tests.ps1
tools\run-tests.ps1 -Platform PlayMode
```

Runs Unity in batch mode and prints totals. The results file is `Logs\test-results-<platform>.xml`.

## Reading the editor console and log file

- Editor log on Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log`. Previous session: `Editor-prev.log`.
- Batch runs write their own logs: `Logs\test-EditMode.log` and `Logs\build-android.log`.
- Live device log: `adb logcat -s Unity`.

## Triage loop

1. Test on the phone, see something wrong.
2. File an issue with a screenshot, what was expected and the build number.
3. Label it `claude-ready`.
4. Claude triages, asks questions if needed, and opens a PR into `dev`.
5. CI runs tests. A human approves and merges.
6. A new build lands on the phone. Test again.
