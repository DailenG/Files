# Project Charter: Files Cloud Guard

## 1. Mission

The mission of Files Cloud Guard is to create an upstream-friendly, provider-aware optimization layer in Files that prevents passive operations from triggering unnecessary remote file hydration on virtual cloud filesystems (initially Egnyte Desktop App), while keeping Files fast, responsive, and completely unmodified on local and standard network drives.

## 2. Problem Statement

Virtual cloud filesystems such as the Egnyte Desktop App mount remote filesystems into Windows as local drives or UNC paths (e.g. `\\EgnyteDrive\`). While they present standard filesystem semantics, reading file contents has real bandwidth, storage, and latency consequences.

In standard file managers:
- Opening and scrolling a folder triggers shell thumbnail extraction. If an item is uncached, thumbnail extractors may read the file header or full content, causing Egnyte to download ("hydrate") the file into the local desktop cache.
- Selecting a file while the Preview Pane is visible immediately dispatches content-reading preview handlers (for PDFs, Office documents, images, video, CAD files).
- Users who merely browse or organize folders can inadvertently fill their local disk cache and consume network bandwidth without ever deliberately opening the files.

## 3. Core Principles

1. **Distinguish Passive vs. Explicit Access**:
   - *Passive Access* (hovering, selecting, scrolling, opening folders, preview pane activation) must never hydrate files on protected cloud locations. It consumes only directory metadata, cached thumbnails, and generic icons.
   - *Explicit Access* (double-clicking to open, clicking a deliberate "Load Preview" button) allows normal retrieval.
2. **Zero Regressions on Local & Standard Network Drives**:
   - Local disks (C:, D:) and standard SMB network shares must retain 100% stock Files functionality and performance.
3. **Upstream Compatibility & Clean Seams**:
   - Avoid scattering provider checks throughout the codebase. Use clean interfaces (`ICloudLocationClassifier`, `ICloudInteractionTelemetry`).
   - Keep diffs minimal, focused, and reviewable for upstream contribution.
4. **Evidence-Based Engineering**:
   - Support *Off*, *Observe*, and *Protect* operating modes.
   - Collect privacy-safe OpenTelemetry metrics and traces to measure the real-world impact before and after optimizations.
5. **Absolute Privacy in Telemetry**:
   - No file names, folder paths, usernames, tenant IDs, or document contents are ever recorded or exported.

## 4. Milestone Roadmap

- **v0.1.0-poc (Proof of Concept)**:
  - Establish clean upstream baseline and documentation.
  - Implement non-blocking Egnyte location classification.
  - Implement telemetry abstraction with Off, Observe, and Protect modes.
  - Implement cached-only thumbnail queries with generic icon fallback on Egnyte.
  - Implement deferred metadata preview card with explicit [Load Preview] button.
  - Local containerized OpenTelemetry collector & dashboard.
- **v0.2.0-pilot (Pilot Hardening)**:
  - Packaging and sideloading for pilot evaluation.
  - Real-world validation with Egnyte Desktop App and Process Monitor verification.
  - Pilot runbook and rollback procedures.
- **Future / Stage 2**:
  - Search guards (prevent deep recursive traversals).
  - Folder size calculation guards.
  - AEC-specific workflows (large CAD/BIM awareness: `.dwg`, `.rvt`, `.ifc`).
