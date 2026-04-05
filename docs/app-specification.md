# Khepri — App Specification

## Overview

**Khepri** is a mobile application built with .NET MAUI (C#).
The app is structured around independent features, each self-contained with no coupling between them.

---

## Platform

| Property | Value |
|---|---|
| Framework | .NET MAUI (.NET 10) |
| Language | C# |
| Target Platforms | Android, iOS (primary); Windows, macOS (optional) |
| Primary test device | Pixel 10 Pro (physical, USB) |

---

## Design Language

**Swiss Modernist / International Typographic Style.**

- **Palette**: pure black and white as primary tones; a single mid-grey for secondary text. No decorative colour. Light/dark mode both follow the same rule — invert the palette.
- **Typography**: uppercase labels with letter-spacing for titles and categories; mixed-case for body text. No custom fonts — system default (Roboto / San Francisco).
- **Layout**: strict grid, generous whitespace, left-aligned. No shadows, no gradients, no rounded corners on containers.
- **Imagery**: photos fill their cells with `AspectFill` and are the sole source of visual richness.
- **Interaction**: no animations beyond standard navigation. Feedback is structural (a spinner, a label) not decorative.
- **Accent**: one 1 px black (light) / white (dark) rule line is used as a structural separator below thumbnails in cards. No other decorative lines.

---

## Feature 1 — Timelapse

### Purpose

Allow the user to build a personal timelapse over days, weeks, or months by capturing one photo per session. Designed primarily for face timelapses where consistent framing is critical.

### User Stories

| ID | As a user, I want to… | So that… |
|---|---|---|
| TL-01 | See all my timelapse projects in a grid when I open the app | I can quickly pick which project to work on |
| TL-02 | See a thumbnail of the most recent image, project name, and date of last capture on each card | I can recognise projects at a glance |
| TL-03 | Filter projects by name using a search bar | I can find a specific project quickly |
| TL-04 | Open a project and browse all its images with the most recent first | I can review my progress easily |
| TL-05 | Tap the camera icon to capture today's photo with a ghost overlay of the last frame | I can align my face accurately |
| TL-06 | Adjust the opacity of the ghost overlay | I can fine-tune how visible the reference image is |
| TL-07 | Retake the last captured image | I can replace a bad shot without breaking the sequence |
| TL-08 | Tap the play button to watch the timelapse animation in-app | I can see my progress up to the current date |
| TL-09 | Control playback speed of the timelapse | I can watch it faster or slower |
| TL-10 | Tap the share button to export and send or download the timelapse as a video | I can share my timelapse with others |
| TL-11 | Create and manage multiple timelapse projects | I can run more than one timelapse simultaneously (e.g. face + plant) |
| TL-12 | Clone an existing project into a new one | I can experiment with alignment algorithms without touching my original frames |
| TL-13 | Run an automatic face-alignment pass on a cloned project | I can see whether the algorithm improves the timelapse before committing |

### Screens

#### 1. Home — Project Grid
The app opens directly to this screen. No system navigation bar is shown — the app manages its own header.

- **Header**: `KHEPRI` wordmark (uppercase, letter-spacing 5) left-aligned; `+` button right-aligned to create a new project (name prompt).
- **Search bar**: uppercase placeholder `SEARCH PROJECTS`, letter-spacing 1; filters the grid reactively as the user types. Case-insensitive substring match.
- **Grid**: 2 columns, 20 px horizontal margin, 12 px column gap, 20 px row gap.
- **Card layout** (top to bottom):
  1. Thumbnail — `AspectFill`, 160 px height. Shows `LatestFrame` image; grey placeholder when no frames exist.
  2. 1 px accent rule (black / white per theme).
  3. Project name — 13 pt bold, truncated.
  4. Last capture date — `d MMM yyyy`, 10 pt, grey.
- **Empty state**: uppercase `NO PROJECTS` label with 3 pt letter-spacing, secondary grey.
- Long-press on a card: context menu with **Delete** (confirmation required) and **Clone** actions.
- **Clone**: prompts for a new project name. Cloned projects are visually tagged with a small badge.

#### 2. Project Detail
Opened by tapping a project card.

- **Image grid**: all captured frames displayed in **reverse chronological order** (most recent first), 2-column `AspectFill` grid.
  - Tapping a frame opens a full-screen preview.
- **Toolbar** with four actions:
  | Label | Action |
  |---|---|
  | Capture | Open Capture Screen to take today's photo |
  | Play | Open Playback Screen to watch the timelapse |
  | Share | Export and share/download the timelapse as a video |
  | Align | Open the Alignment Screen (clone projects only) |
- The **Align** action is only shown on projects that are clones, preventing accidental modification of original data.
- Back navigation returns to the Project Grid.

#### 3. Capture Screen
Reached from the Capture button in the Project Detail toolbar.

- Full-screen live camera preview (`CommunityToolkit.Maui.Camera`).
- **Ghost overlay**: most recently captured frame rendered on top at adjustable opacity (default 50 %).
- Opacity slider to adjust overlay transparency; hidden when no previous frame exists.
- **Capture button** — large, thumb-reachable; saves the photo and returns to Project Detail.
- **Cancel button** — dismisses without saving.
- Frame counter (e.g. "Frame 48") shown as a subtitle.
- All camera operations (start/stop preview, capture) are dispatched on the main thread to satisfy `ProcessCameraProvider` threading requirements.

#### 4. Playback Screen
Reached from the Play button in the Project Detail toolbar.

- Plays all frames as an in-app animation in chronological order.
- Controls: play/pause, speed selector (4 fps / 12 fps / 24 fps), scrub bar.
- Frame indicator (e.g. "12 / 48").
- Close/back returns to Project Detail.

#### 5. Share / Export
Triggered from the Share button in the Project Detail toolbar.

- Renders frames into a video file (MP4) asynchronously with a progress indicator.
- On completion: invokes the native share sheet so the user can save to gallery, send via messaging app, etc.

#### 6. Alignment Screen
Reached from the Align button in the Project Detail toolbar (clone projects only).

- Lets the user choose an **alignment algorithm** from a list of available options (see below).
- Shows a **before / after preview** on a sample frame pair before committing.
- **Run** button: processes all frames in sequence with a progress bar (frame N / total).
- Results overwrite the clone's frames in-place; the original project is never touched.
- If the run fails mid-way the already-processed frames are kept and the user can retry.

**Alignment algorithms:**

| Algorithm | Technique | Notes |
|---|---|---|
| **Facial landmark warp** ✓ | Detect face landmarks (eyes, nose, mouth) and apply affine/thin-plate-spline warp to align them across frames | **Confirmed default.** Uses MediaPipe Face Mesh — runs fully on-device, offline, no cloud calls. |
| Phase correlation | Frequency-domain translation estimation | Fallback if no face is detected in a frame |
| ORB feature matching | Detect and match ORB keypoints, compute homography | General-purpose fallback for non-face projects |
| Laplacian sharpness crop | Uses Laplacian variance to detect the sharpest region and centre-crop | Simplest fallback; corrects framing drift only |

### Data Model

```
TimelapseProject
├── Id                  : Guid
├── Name                : string
├── CreatedAt           : DateTimeOffset
├── ClonedFromId        : Guid?         // null for originals; set when cloned
├── IsClone             : bool          // derived from ClonedFromId
├── LatestFrame         : TimelapseFrame?  // derived: last element of Frames
└── Frames[]
    ├── Id              : Guid
    ├── Index           : int           // 0-based, defines playback order
    ├── CapturedAt      : DateTimeOffset
    ├── FilePath        : string        // local file path to stored image
    ├── AlignedFilePath : string?       // path to aligned version; null until alignment is run
    └── ActiveFilePath  : string        // derived: AlignedFilePath ?? FilePath
```

Frames are stored as full-resolution JPEG files in app-private local storage.
Metadata is persisted as JSON (`project.json`) in each project's folder.

**Storage layout:**
```
AppDataDirectory/
  projects/
    {projectId}/
      project.json
      (frame images referenced by absolute FilePath in project.json)
  frames/
    {guid}.jpg   ← written by CameraPage at capture time
```

### Rules & Constraints

- "Retake" replaces the **last frame only** — replacing older frames is not supported in v1.
- The ghost overlay uses the **most recently captured frame** (`LatestFrame`), not today's frame (since today's may not exist yet).
- Playback is rendered in-app; export is a separate async operation.
- **Clone** copies image files on disk into a new folder; it does not share file references with the source project.
- **Alignment** only modifies clone projects. The Align button is hidden on original projects.
- Aligned frames are stored alongside originals (`AlignedFilePath`); playback and export use `ActiveFilePath` (aligned when available).
- All file I/O is serialised through a `SemaphoreSlim` to prevent concurrent read/write conflicts on Android.
- All camera lifecycle operations (`StartCameraPreview`, `StopCameraPreview`) must be called on the main thread.

---

## Future Features (Planned, Not Scoped)

These are independent of Timelapse and will be designed separately.

| Feature | Description |
|---|---|
| Image Filter — Logarithm | Apply a logarithmic tone mapping / curve to a selected image and save the result. |
| (more tbd) | Additional standalone image tools or utilities. |

These features share no code, no data model, and no navigation flow with Timelapse. They will be accessed from a top-level feature menu.

---

## Architecture Notes

- **Clean Architecture**: Domain → Application → Infrastructure → Presentation layers with strict dependency direction.
- **Feature-based folder structure**: each feature lives in its own folder/namespace and does not reference other features directly.
- Navigation via MAUI Shell with `[QueryProperty]` on ViewModels and `Routing.RegisterRoute`.
- Local storage only in v1; no cloud sync.
- Camera access abstracted behind `ICameraService`; implemented by `MauiCameraService` which pushes `CameraPage` modally.
- **Central Package Management** (`Directory.Packages.props`); repo-wide analyzers enforced via `Directory.Build.targets`.
- **Key packages**: `CommunityToolkit.Mvvm 8.4.2`, `CommunityToolkit.Maui.Camera 6.0.0`, `Microsoft.Maui.Controls 10.0.30` (pinned).

---

## Open Questions

1. ~~Single active timelapse or multiple?~~ → **Multiple projects confirmed.**
2. ~~Framework choice?~~ → **.NET MAUI confirmed.**
3. Should "one frame per day" be a hard limit or a soft warning?
4. Export format: ~~MP4 assumed as primary~~ → **MP4 confirmed**; GIF or image ZIP as secondary options?
5. ~~Alignment algorithm default?~~ → **Facial landmark warp confirmed.** MediaPipe Face Mesh, on-device, offline.


### Purpose

Allow the user to build a personal timelapse over days, weeks, or months by capturing one photo per session. Designed primarily for face timelapses where consistent framing is critical.

### User Stories

| ID | As a user, I want to… | So that… |
|---|---|---|
| TL-01 | See all my timelapse projects in a grid when I open the app | I can quickly pick which project to work on |
| TL-02 | See a thumbnail of the most recent image and the project name on each grid card | I can recognise projects at a glance |
| TL-03 | Open a project and browse all its images with the most recent first | I can review my progress easily |
| TL-04 | Tap the camera icon to capture today's photo with a ghost overlay of the last frame | I can align my face accurately |
| TL-05 | Adjust the opacity of the ghost overlay | I can fine-tune how visible the reference image is |
| TL-06 | Retake the last captured image | I can replace a bad shot without breaking the sequence |
| TL-07 | Tap the play button to watch the timelapse animation in-app | I can see my progress up to the current date |
| TL-08 | Control playback speed of the timelapse | I can watch it faster or slower |
| TL-09 | Tap the share button to export and send or download the timelapse as a video | I can share my timelapse with others |
| TL-10 | Create and manage multiple timelapse projects | I can run more than one timelapse simultaneously (e.g. face + plant) |
| TL-11 | Clone an existing project into a new one | I can experiment with alignment algorithms without touching my original frames |
| TL-12 | Run an automatic face-alignment pass on a cloned project | I can see whether the algorithm improves the timelapse before committing |

### Screens

#### 1. Home — Project Grid
The app opens directly to this screen.

- Responsive grid of project cards (2 columns on mobile, more on wider screens).
- Each card shows:
  - Thumbnail of the **most recent frame**.
  - **Project name** below the thumbnail.
- Floating action button (or top-right icon): **Create new project** (name prompt).
- Long-press or swipe on a card: context menu with **Delete** (confirmation required) and **Clone** actions.
- **Clone**: prompts for a new project name, then copies all frames into a new project. The clone is visually tagged (e.g. a small "clone" badge) so it is easy to distinguish from the original.

#### 2. Project Detail
Opened by tapping a project card.

- **Image grid**: all captured frames displayed in **reverse chronological order** (most recent first).
  - Tapping a frame opens a full-screen preview.
- **Toolbar** (bottom bar or top app bar) with four actions:
  | Icon | Action |
  |---|---|
  | Camera | Open Capture Screen to take today's photo |
  | Play | Open Playback Screen to watch the timelapse |
  | Share | Export and share/download the timelapse as a video |
  | Align | Open the Alignment Screen to run automatic face alignment |
- The **Align** action is only shown on projects that are clones, preventing accidental modification of original data.
- Back navigation returns to the Project Grid.

#### 3. Capture Screen
Reached from the camera icon in the Project Detail toolbar.

- Live camera preview fills the screen.
- **Ghost overlay**: most recently captured frame rendered on top at adjustable opacity (default ~40 %).
- Opacity slider (or gesture) to adjust overlay transparency.
- **Capture button** — large, thumb-reachable; saves the photo and returns to Project Detail.
- **Retake button** — visible only when today's frame already exists; prompts confirmation before replacing it.
- Frame counter shown as a subtitle (e.g. "Frame 48").

#### 4. Playback Screen
Reached from the play icon in the Project Detail toolbar.

- Plays all frames as an in-app animation in chronological order.
- Controls: play/pause, speed selector (e.g. 4 fps / 12 fps / 24 fps), scrub bar.
- Frame indicator (e.g. "12 / 48").
- Close/back returns to Project Detail.

#### 5. Share / Export
Triggered from the share icon in the Project Detail toolbar.

- Renders frames into a video file (MP4 preferred) asynchronously with a progress indicator.
- On completion: invokes the native share sheet so the user can save to gallery, send via messaging app, etc.

#### 6. Alignment Screen
Reached from the align icon in the Project Detail toolbar (clone projects only).

- Lets the user choose an **alignment algorithm** from a list of available options (see below).
- Shows a **before / after preview** on a sample frame pair before committing.
- **Run** button: processes all frames in sequence with a progress bar (frame N / total).
- Results overwrite the clone's frames in-place; the original project is never touched.
- If the run fails mid-way the already-processed frames are kept and the user can retry.

**Alignment algorithms (candidates):**

| Algorithm | Technique | Notes |
|---|---|---|
| **Facial landmark warp** ✓ | Detect face landmarks (eyes, nose, mouth) and apply affine/thin-plate-spline warp to align them across frames | **Confirmed default.** Uses MediaPipe Face Mesh — runs fully on-device, offline, no cloud calls. |
| Phase correlation | Frequency-domain translation estimation | Fallback if no face is detected in a frame |
| ORB feature matching | Detect and match ORB keypoints, compute homography | General-purpose fallback for non-face projects |
| Laplacian sharpness crop | Uses Laplacian variance to detect the sharpest region and centre-crop | Simplest fallback; corrects framing drift only |

### Data Model

```
TimelapseProject
├── Id                  : Guid
├── Name                : string
├── CreatedAt           : DateTimeOffset
├── ClonedFromId        : Guid?         // null for originals; set when cloned
├── Frames[]
│   ├── Id              : Guid
│   ├── Index           : int           // 0-based, defines playback order
│   ├── CapturedAt      : DateTimeOffset
│   ├── FilePath        : string        // local file path to stored image
│   └── AlignedFilePath : string?       // path to aligned version; null until alignment is run
```

Frames are stored as full-resolution image files in app-private local storage.
Metadata is persisted in a local SQLite database or JSON file.

### Rules & Constraints

- Only **one frame per day** is enforced by default (configurable).
- "Retake" replaces the **last frame only** — replacing older frames is not supported in v1.
- Retake requires explicit confirmation to prevent accidental data loss.
- The ghost overlay uses the **most recently captured frame**, not today's frame (since today's may not exist yet).
- Playback is rendered in-app; export is a separate async operation.
- **Clone** copies image files on disk into a new folder; it does not share file references with the source project.
- **Alignment** only modifies clone projects. The align icon is hidden on original projects.
- Aligned frames are stored alongside originals (`AlignedFilePath`); playback and export use the aligned version when available.

---

## Future Features (Planned, Not Scoped)

These are independent of Timelapse and will be designed separately.

| Feature | Description |
|---|---|
| Image Filter — Logarithm | Apply a logarithmic tone mapping / curve to a selected image and save the result. |
| (more tbd) | Additional standalone image tools or utilities. |

These features share no code, no data model, and no navigation flow with Timelapse. They will be accessed from a top-level feature menu.

---

## Architecture Notes

- **Feature-based folder structure**: each feature lives in its own folder/namespace and does not reference other features directly.
- Navigation between features goes via a shared shell/app-level router.
- Local storage only in v1; no cloud sync.
- Camera access abstracted behind an interface to support MAUI implementations.

---

## Open Questions

1. ~~Single active timelapse or multiple?~~ → **Multiple projects confirmed** (project grid is the home screen).
2. .NET MAUI vs Uno Platform — final choice affects camera API and platform targets.
3. Should "one frame per day" be a hard limit or a soft warning?
4. Export format: MP4 assumed as primary; GIF or image ZIP as secondary options?
5. ~~Alignment algorithm default?~~ → **Facial landmark warp confirmed.** Must run fully on-device, offline (no cloud calls). Recommended implementation: MediaPipe Face Mesh (on-device, ~5 MB model, works offline).
