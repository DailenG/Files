# Upstream Patch Candidates

Changes in this fork that are worth sending to
[files-community/Files](https://github.com/files-community/Files), tracked here so they are not
rediscovered one at a time. Companion to [upstream-sync.md](upstream-sync.md), which covers pulling
upstream changes *in*; this file covers pushing our changes *out*.

Tracking issue: [#9 Track Upstream Synchronization](https://github.com/DailenG/Files/issues/9).

## What qualifies

A change is a candidate only if all of these hold:

1. **It stands alone.** It compiles and makes sense without any Cloud Guard type
   (`ICloudLocationClassifier`, `ICloudInteractionTelemetry`, `CloudOptimizationMode`, ...). If a
   cherry-pick would drag in the fork's cloud layer, it is not a candidate in its current shape.
2. **It fixes upstream behaviour, or adds something provider-neutral.** Bug fixes first. Anything
   that only serves the Egnyte pilot belongs in the fork.
3. **Upstream still has the problem.** Verify before writing the PR; upstream moves fast:
   ```powershell
   git fetch upstream
   git show upstream/main:<path> | Select-String -Pattern '<the line>'
   ```
4. **We can describe the user-visible symptom.** Upstream reviewers need a repro, not a diff.

Everything else stays here. That is not a value judgement: the Cloud Guard layer is deliberately
built behind clean seams *so that* a future upstream conversation is possible, but it needs that
conversation first, not a surprise PR.

## Candidates

### 1. Autodesk Desktop Connector: one entry per connector, not per subfolder

| | |
|---|---|
| Status | Ready. Fixed and shipped in the fork, not yet sent upstream |
| Fork commit | `d82556136` |
| File | `src/Files.App/Utils/Cloud/CloudDrivesDetector.cs`, `DetectAutodeskDrive` |
| Upstream state | Byte-identical to our pre-fix code as of `upstream/main` on 2026-09-09 |
| Type | Fix |
| Depends on Cloud Guard | No. Self-contained; cherry-picks cleanly |

**Symptom.** On a machine with Autodesk Desktop Connector, the sidebar's Cloud Drives section fills
with dozens or hundreds of `Autodesk - <name>` entries and grows a vertical scrollbar. File Explorer
shows the same connection as a single **Autodesk Docs** node. The list includes obvious non-drives
such as `Autodesk - 01 - Architecture` and `Autodesk - 03 - Structural`, which are folders *inside* a
project.

**Cause.** Two defects on one line:

```csharp
Directory.GetDirectories(mainFolder, "", SearchOption.AllDirectories)
```

- `SearchOption.AllDirectories` walks the entire `%USERPROFILE%\DC` tree and adds every directory at
  every depth as its own `CloudProvider`.
- The empty search pattern does not match nothing, as one might assume; it behaves like `*`.
  Verified empirically: a two-connector probe tree returned all 6 directories.

Per Autodesk's documentation the workspace is `DC\<connector>\<hub>\<project>\...`, where the top
level is the connector type (`ACCDocs`, `Drive`, `Fusion`). Explorer's single "Autodesk Docs" node is
the `ACCDocs` connector, so top-level-only enumeration is the granularity that matches Explorer.

**Fix.** `SearchOption.TopDirectoryOnly` with pattern `*`, plus a small name map so the sidebar reads
`Autodesk Docs` / `Autodesk Drive` / `Autodesk Fusion`, falling back to `Autodesk - {folder}` for
unrecognised connectors. Also guards `Directory.Exists(mainFolder)`: Desktop Connector's **Change
Workspace** command relocates the workspace, and the unguarded call threw
`DirectoryNotFoundException` into `SafetyExtensions.IgnoreExceptions`, silently disabling *all*
Autodesk detection on those machines.

**Measured effect.** A simulated workspace of 3 connectors with 5 projects went from 17 sidebar
entries to 3. Real workspaces are far worse; the reporting user had enough to need a scrollbar.

**Review risk to disclose in the PR.** The `ACCDocs` / `Drive` / `Fusion` names come from Autodesk's
published workspace layout, not from a machine we control. Neither the fix author nor the reporting
user has verified the folder names on older Desktop Connector versions. The fallback keeps the
previous `Autodesk - {folder}` shape, so an unrecognised connector degrades to today's naming rather
than disappearing. Worth asking upstream reviewers with Desktop Connector installed to confirm.

**Not verified on a live machine.** No Desktop Connector on the development machine. The enumeration
change was proven against a simulated tree, and the reporting user is the real test.

## Identified, not built

Upstream-shaped work found while investigating something else. Recorded so the analysis is not lost;
none of it is written yet, so none of it is sendable. All three came out of
[adr/0003-prepared-location-access.md](adr/0003-prepared-location-access.md).

| Item | Where | Why it qualifies |
|---|---|---|
| Archive hydrated twice on first open | `ZipStorageFolder.FromPathAsync` -> `CheckAccess`, then `GetItemsAsync` -> `OpenZipFileAsync` | The access probe builds a `SevenZipExtractor` purely to answer "is this browsable", discards it, and the index read immediately reopens the archive. Invisible locally because the page cache absorbs it; on a cloud file it doubles a whole-file fetch. Provider-neutral |
| Archive index re-read on every in-archive navigation | `ZipStorageFolder.GetItemsAsync` | A fresh extractor per navigation. An index cache keyed on container path, invalidated on write, would make in-archive browsing instant after the first open. There is precedent in the same class, which already caches per-container encoding |
| Completed Status Center cards are not actionable | `StatusCenterItem`, `StatusCenter.xaml` | A finished copy, move, extract or compress reports success and offers nothing. `Source` and `Destination` are already populated by every `StatusCenterHelper` call site, so activation can navigate to the destination's parent and select the result through `NavigationHelpers.OpenPath` with `selectItems`. Needs explicit handling for delete and recycle, which have no destination, and for failed or cancelled cards |

The third is the best next upstream contribution on this list: small, self-contained, no Cloud Guard
involvement, and useful to every Files user rather than only to cloud ones.

## Deliberately not candidates

| Change | Why it stays in the fork |
|---|---|
| `Files.Shared/Cloud/*` (classifier, telemetry, modes) | The whole Cloud Guard layer. Provider-neutral by design and an explicit goal of the charter, but it needs an upstream design discussion about whether Files wants a telemetry surface at all, not a drive-by PR |
| Cached-only thumbnails, deferred preview, search guard | Behaviour changes gated on `CloudOptimizationMode`. Meaningless without the layer above |
| `CloudSearchLimitedInfoBar` in `Views/MainPage.xaml` | Presents a Cloud Guard policy decision. The surrounding `StackPanel` refactor of `NetworkDiscoveryInfoBar` is incidental and would be churn on its own |
| `AppEnvironment.SideloadPoc` | Fork packaging identity |
| `Services/Cloud/CloudTelemetryExportHost.cs`, `MappedDriveResolver.cs` | Fork-only files |
| POC workflow, packaging scripts, `ops/`, pilot docs | Fork infrastructure |

## Sending one

1. Re-verify upstream still has the problem (criterion 3 above).
2. Branch from `upstream/main`, not from `origin/main`:
   ```powershell
   git fetch upstream
   git checkout -b fix/autodesk-connector-enumeration upstream/main
   git cherry-pick d82556136
   ```
3. Build `src/Files.App/Files.App.csproj` for `x64` and confirm the cherry-pick carried no fork types.
4. Follow upstream's PR template and title convention, which matches ours: `Fix:`, `Feature:` or
   `Code Quality:` prefix.
5. Record the upstream PR number in the candidate's table above and note it on issue #9.
6. When upstream merges it, the change arrives back through the normal sync in
   [upstream-sync.md](upstream-sync.md) and the local commit becomes a no-op merge.

## Adding one

When a fix lands in this fork that meets the four criteria, add a section here in the same shape:
status, fork commit, file, upstream state with the date checked, symptom, cause, fix, measured
effect, and anything a reviewer would need to challenge. The point of the register is that the
evidence is written down while it is fresh, not reconstructed months later.
