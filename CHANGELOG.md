# Changelog

All notable changes to Khepri are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions align with `ApplicationVersion` / `ApplicationDisplayVersion` in `Khepri.csproj`.

---

## [Unreleased]

_Nothing yet._

---

## [1.0] — 2026-04-05

### Added

**Core timelapse**
- Create, rename, and delete timelapse projects.
- Capture frames with a live camera preview; ghost overlay of the previous frame at adjustable opacity for consistent framing.
- Browse all frames in reverse-chronological order inside a project.
- Reorder frames via drag-and-drop.
- Delete individual frames (single or multi-select) with confirmation.
- Clone a project (copies all frames into a new folder; original is never modified).
- Rename projects.

**Playback & export**
- In-app timelapse animation with play/pause, variable speed (4 / 12 / 24 fps), scrub bar, and frame counter.
- Export timelapse as H.264 MP4 using `Android.Media.MediaCodec` + `Android.Media.MediaMuxer` (on-device, no cloud).
- Native share sheet after export.

**Storage**
- User-selectable external storage root (`StorageSetupPage`) shown on first launch.
- Projects and photos saved under `<user-root>/projects/` and `<user-root>/frames/` — survives app reinstall.
- Android 11+ (API 30+): All Files Access (`MANAGE_EXTERNAL_STORAGE`) via Settings deep-link.
- Android ≤10: `WRITE_EXTERNAL_STORAGE` runtime permission.
- Storage root path persisted in `Preferences`; user picks the same folder after reinstall to recover all data.
- SAF folder picker result routed through `MainActivity.OnActivityResult`.

**Branding**
- Animated splash screen: two rule lines expand from centre, "KHEPRI" fades in, holds, fades out.
- Geometric K app icon and Android splash image; pure black (`#0A0A0A`) background.

**CI/CD**
- GitHub Actions workflow: build + test on every push; signed AAB deployed to Google Play internal track on `main`.
- `scripts/build-aab.ps1` — local script for one-time manual AAB upload to activate Play Console API publishing.
- `scripts/deploy-usb.ps1` — USB deploy with `-NoBuild` and `-Cleanup` switches.

### Architecture

- .NET 10 / MAUI, Android-first (`net10.0-android`).
- Clean Architecture: Domain → Application → Infrastructure → Presentation.
- `CommunityToolkit.Mvvm 8.4.2` — `[ObservableProperty]` partial property syntax.
- `IStorageRootService` abstraction with `AndroidStorageRootService` and `DefaultStorageRootService`.
- Atomic JSON writes (write to `.tmp`, then `File.Move(..., overwrite: true)`).
- All file I/O serialised through `SemaphoreSlim(1,1)`.
- Package ID: `com.binerdy.khepri`.

---

[Unreleased]: https://github.com/binerdy/khepri/compare/v1.0...HEAD
[1.0]: https://github.com/binerdy/khepri/releases/tag/v1.0
