# iOS setup (placeholder)

Not started. The teammate with the Apple Developer Program account owns signing and App Store Connect. This lists what that person needs to do so iOS work can begin; nothing here has been tried yet.

## What iOS needs that Android does not
- A Mac with Xcode. Unity cannot produce a signed iOS app from Windows. Unity builds an Xcode project; Xcode signs and uploads it.
- The Unity **iOS Build Support** module for the editor version in `ProjectSettings/ProjectVersion.txt`.
- An Apple Developer Program membership (paid, yearly) on the account that owns the app id.
- TestFlight for distribution to testers. Unity Remote is not the iOS dev loop, and a TestFlight build also exercises real touch and performance.

## Steps for the Apple account owner
1. Register the app id (placeholder `com.projectbankai.game`, matching `ProjectSetup.cs`; change both together if the name changes).
2. Create the app in App Store Connect and a TestFlight internal testing group.
3. Create a **private** repo for signing material and set up fastlane `match` against it. The certificates and profiles never go in this public repo or in CI logs.
4. Create an App Store Connect API key for CI uploads. It goes in GitHub Actions secrets, never in the repo.
5. On the Mac: install Unity Hub, the editor version above and the iOS module, open the project, **File > Build Profiles > iOS > Build**, open the generated Xcode project, set the team, and run on a device.

## What Claude will do once that exists
- Add an iOS build method alongside `Game.Editor.BuildScript.BuildAndroid`.
- A fastlane lane that builds, signs through `match` and uploads to TestFlight.
- An iOS section in `docs/dev-loop.md`.
- Check safe-area handling on notched iPhones (the HUD already follows `Screen.safeArea`).

## Open questions
- Does the account owner have a Mac, or is a cloud Mac build service needed?
- Which iPhone is the baseline test device?
- Public repo reminder: builds and workflow artifacts can be downloaded by anyone, so TestFlight (private) is the right channel for iOS from the start.
