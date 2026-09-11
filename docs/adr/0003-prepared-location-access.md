# ADR 0003: Tracked preparation for slow location and file access

Status: Proposed. Explored on `feature/prepare-location-status-center`, not merged.

## Context

Double-clicking a large archive on an Egnyte location appears to hang. The cause is established in
[archive-navigation-ux-proposals.md](../archive-navigation-ux-proposals.md): Files browses archives
in place, but a ZIP's central directory sits at the end of the file, so opening the index forces the
provider to hydrate the whole archive. A 1 GB archive is a 1 GB download before a single row appears.

Of the four options proposed, the chosen direction is a combination of two of them: show an in-place
loading state at the destination, and simultaneously track the work as a Status Center job so that
navigating away does not abandon or hide it.

The wider observation driving this ADR is that the archive case is one instance of a general pattern.
Files has several operations whose latency is normally imperceptible and becomes significant on cloud
or network storage:

- opening an archive root (hydrate whole file, read index)
- launching a file in its default application (hydrate before the app sees it: the Revit case)
- enumerating a directory on a slow provider
- reading rich properties or generating a thumbnail for a large uncached file

Today each of these either blocks silently or is invisible. There is no shared vocabulary for
"something is being fetched so that you can do the thing you asked for", and no surface that reports
it. This ADR proposes that vocabulary, and uses archive opening as the first and only consumer.

## Decision

Introduce a **location preparation** step: a named, cancellable, progress-reporting operation that
runs between the user's request and the action that needs local bytes.

### Core abstraction

A single service owns preparation. Sketch, not final:

```csharp
public interface IPreparedAccessService
{
    bool MayRequirePreparation(string path, PreparedAccessKind kind);

    Task<PreparedAccessResult> PrepareAsync(
        PreparedAccessRequest request,
        CancellationToken cancellationToken);
}
```

Properties the service must have, in priority order:

1. **Deduplication by path.** A second double-click joins the in-flight preparation instead of
   starting a competing one. Without this, an impatient user doubles the network cost. This is the
   single most important behaviour in the design.
2. **Progress in bytes where obtainable.** See the honesty constraint below.
3. **Cancellation** that stops our read. Already-cached bytes stay cached, so a cancelled attempt
   makes the next attempt faster rather than wasting the transfer.
4. **Kind-specific completion.** For an archive, "prepared" means the index is readable. For a file
   launch, it means the file is fully local.

### Deferred promotion instead of a size threshold

Preparation starts **silently**. If it has not completed within a short grace period, it is promoted:
the destination view shows its loading state and a Status Center card appears.

This avoids inventing a size threshold. A size cutoff has to be tuned, is wrong for someone on a
fast link and wrong again for someone on VPN, and would still flash a card for a small archive on a
bad connection. A time-based trigger is self-calibrating: fast operations stay invisible exactly as
they are today, and only genuinely slow ones surface. Suggested grace period 750 ms to 1 s, settable,
to be validated with pilot users.

Consequence to accept deliberately: cancellation is only offered once promoted. A user cannot cancel
during the grace period. That is the right trade, because a sub-second operation does not need a
cancel button.

### Navigation semantics

- Double-clicking an archive **navigates immediately** to the destination, which renders a preparing
  state rather than an empty list. This is what makes the common case feel seamless: when preparation
  finishes and the user is still there, content simply fills in.
- If the user **navigates away**, preparation continues. The Status Center card remains, and on
  completion offers an action to go to the prepared location.
- Completion **never navigates on its own.** Moving someone's view after they have deliberately gone
  elsewhere is hostile. The card is the invitation; the user accepts it.
- Cancelling, or navigating back during preparation, leaves the archive unopened and the view
  restored. No partial archive state is ever shown.

### Honesty constraint on progress

The cloud provider does not report hydration progress. Byte-level progress therefore requires Files
to hydrate deliberately, reading the file in chunks and reporting as it goes, before handing it to
the extractor. This is a real change to how the archive is opened, not a label over the existing
path.

Two rules follow:

- Where we do the chunked read, report real bytes.
- Where we cannot, the card must be indeterminate. `StatusCenterItem` already supports this through
  `canProvideProgress: false`, which sets `IsIndeterminateProgress`. Never show a fabricated
  percentage.

Before committing to the chunked read, measure it against letting the provider hydrate in its own
way on real Egnyte files. If our read is slower, indeterminate progress is the correct answer and the
chunked read should be dropped.

## Seams

The investigation identified precisely where this lands.

| Concern | Location |
|---|---|
| Blocking call that triggers hydration | `NavigationHelpers.OpenDirectory`, the `GetFolderWithPathFromPathAsync` call |
| Archive open and access probe | `ZipStorageFolder.FromPathAsync` -> `CheckAccess`; index read in `GetItemsAsync` -> `OpenZipFileAsync` |
| Status Center job creation | `StatusCenterViewModel.AddItem`, wrapped by `StatusCenterHelper` |
| Existing operation type | `FileOperationType.Prepare` already exists, with `StatusCenter_Prepare_Header` localized in every language and `StatusCenterHelper.AddCard_Prepare()` already written. Nothing currently calls it |
| In-place loading state | `ShellViewModel.IsLoadingItems` and `ItemLoadStatusChanged` already gate a progress indicator during enumeration |
| Cloud classification | `ICloudLocationClassifier`, already used for thumbnails, previews, search, enumeration |
| Telemetry | `CloudOperationName.OpenFile` and `EnumerateDirectory` already defined and exported |

Two points worth noting. `FileOperationType.Prepare` is documented as "an item is being prepared for
copy/move/drag" and is dead code; either reuse it for this and widen the comment, or add a distinct
member rather than overloading a copy/move concept. Reuse is tempting because the localized string
already exists in 52 languages, but "Preparing the operation..." is vague for this purpose and a
purpose-built string is probably better.

Second, `StatusCenterItem` currently exposes only `CancelCommand`. The "go to the prepared location"
action on a completed card is new surface, in both the view model and `StatusCenter.xaml`.

## Prerequisites

Two defects make any version of this perform worse than it should, and both should be fixed first
because they change what preparation even has to do:

1. **The archive is hydrated twice on first open.** `FromPathAsync` builds a `SevenZipExtractor`
   purely to answer "is this browsable", discards it, and `GetItemsAsync` opens the archive again.
2. **Every in-archive navigation re-opens the archive.** `GetItemsAsync` creates a fresh extractor
   per navigation. An index cache keyed on container path, invalidated on write, would make
   in-archive navigation instant after the first open. `ZipStorageFolder` already caches per-container
   encoding, so there is precedent for keying state this way.

Both are upstream-shaped and Egnyte-independent; the OS page cache merely hides them locally. See
[upstream-candidates.md](../upstream-candidates.md).

## Scope

**In scope for the experiment:** archive root opening only. One consumer, enough to judge the idea.

**Explicitly deferred:** hydrate-before-launch for application files such as Revit. It is the most
compelling generalization and the strongest argument for the abstraction, but it carries its own
problem: we would read the file to hydrate it, then the application reads it again. The second read
is local and cheap, but it needs measuring, and shell-launch interception is a riskier seam than
navigation. Build it second, on evidence from the first.

**Out of scope:** thumbnails, previews and directory enumeration. These are already handled by Cloud
Guard policy, which suppresses rather than reports them. Preparation is for operations the user
explicitly asked for; policy is for passive ones. Keeping that line clear is what stops this feature
from becoming a second, competing cloud subsystem.

## Consequences

**Good.** The archive case stops looking like a hang. Slow work becomes visible, attributable and
cancellable. Files gains one vocabulary for "fetching so you can proceed", which the Revit case and
anything else can reuse. Deduplication removes a real source of doubled network cost. Most of the
infrastructure already exists.

**Bad.** New state to reason about: a view can be "navigated but not ready", and every path that
assumes a navigated location is enumerable must tolerate it. Cancellation and navigation racing each
other is the likely source of bugs. The chunked read may not be worth it, and that is unknown until
measured.

**Risk of scope creep.** "Communicate every cloud-guarded action in the Status Center" is a much
larger idea than this ADR. It is deliberately not proposed here. One consumer, judged on its merits,
then decide.

## Validation

This is a UX change, so evidence is behavioural, not a test suite:

- A large archive on Egnyte: navigate, see the preparing state, see the card, watch it fill in.
- Same archive, navigate away mid-preparation: card persists, action opens the prepared location.
- Same archive, cancel: view restored, nothing half-open.
- Double-click twice: one job, not two. Verifiable in the Status Center.
- A small local archive: no card, no skeleton, behaviour indistinguishable from today. This is the
  regression that matters most, because it is the common case.
- Existing telemetry gives the before and after: `files.cloud.operations` already records
  `enumerate_directory` duration and `open_file`. Collect a baseline before building.

## Test build

The pilot must not be disturbed by an experiment:

- The POC package uses a fixed MSIX `Identity/Name` of `FilesCloudGuard`, so an experimental build
  **replaces the pilot build** on whatever machine installs it. Install on a non-pilot machine, or
  accept that the device changes channel.
- Tag experiments distinctly, for example `v0.2.0-alpha1`, dispatching the workflow against this
  branch rather than `main`.
- Do **not** mirror an experimental build to R2 and do **not** bump the Chocolatey package. Those are
  the pilot distribution channel. Install by hand per the pilot runbook.
- `app.version` is already a telemetry resource attribute and the `Mode per device` dashboard panel
  groups by it, so an experimental build is distinguishable from pilot data rather than polluting it.
