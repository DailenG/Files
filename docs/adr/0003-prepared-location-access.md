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

### The preparing state in the content area

There is no in-content loading state today. The only progress affordance during navigation is a
spinner on the tab, set by `BaseShellPage.SetLoadingIndicatorForTabs`. The content area has exactly
three states, driven by `EmptyTextType`: `None`, `FolderEmpty` and `NoSearchResultsFound`.

That is a problem this design creates for itself. `ShellViewModel.UpdateEmptyTextType` sets
`FolderEmpty` whenever `FilesAndFolders.Count == 0 && !IsLocationUnavailable`. Today that is never
reached for an archive, because hydration completes inside `OpenDirectory` *before* the view
changes, so the user sits on the old folder watching a tab spinner. The moment we navigate first and
prepare afterwards, an empty list plus a long preparation means the user reads **"This folder is
empty"** while a 1 GB download runs. Replacing a silent wait with a confident wrong answer is worse
than the bug we set out to fix.

So the preparing state is required, not decorative: add a fourth `EmptyTextType` member, e.g.
`Preparing`, with a visual state carrying a progress ring and a message. `FolderEmptyIndicator` is
already a visual-state-driven control bound in all three layouts, so this is a small, contained
extension of an existing control rather than a new page.

Deliberately **not** a full-page loading splash. A distinct page creates a mode to enter and leave,
which flickers on fast operations and needs its own back-navigation behaviour. Extending the state
the layouts already have costs less and cannot flicker, because the grace period means it is never
shown for fast work. Skeleton placeholder rows are a possible refinement later; they are strictly
more work than a ring and would need fake rows in a virtualized list, so they are not in the first
cut.

### Status Center volume and card lifecycle

Flooding is a fair concern and the design has to answer it rather than hope. Four things keep the
volume down, in descending order of importance:

1. **The grace period.** Fast preparations never create a card at all. On local disks this feature is
   invisible, which is the common case by a wide margin.
2. **Deduplication.** One card per path, however many times the user double-clicks.
3. **Self-dismissal on seamless completion.** If preparation finishes while the user is still at the
   destination, the card has served no purpose and removes itself. The content filling in *is* the
   notification. A card that survives here is pure litter, and this is the case that would otherwise
   generate one card per archive open.
4. **Only cards the user left behind persist,** because those are the ones carrying the "go to the
   prepared location" action. A persisting card always corresponds to something the user walked away
   from and might want to return to.

Point 3 is the one that decides whether this floods. `StatusCenterViewModel.RemoveItem` already
exists, so self-dismissal is available; what is new is the policy of using it. Note this makes
preparation cards behave unlike file-operation cards, which persist until dismissed. That asymmetry
is intentional and worth stating in review: a completed copy is a record worth keeping, a completed
preparation the user already saw resolve is not.

This is also why the scope line in this ADR matters. Preparation covers operations the user
explicitly requested, which are bounded by how fast a person can double-click. Extending it to
passive operations such as thumbnails or enumeration would produce hundreds of cards, and that is
precisely what is ruled out below.

### Completed cards should be actionable, generally

The "go to the prepared location" action should not be special-cased to preparation. Today
`StatusCenterItem` exposes only `CancelCommand`, so a finished copy, move, extract or compress tells
you it succeeded and then offers nothing. Clicking it should take you to the result.

The data is already there: `StatusCenterItem` carries `Source` and `Destination` as
`IEnumerable<string>`, populated by every `StatusCenterHelper` call site. A generic completed-card
activation can navigate to the destination's parent and select the item, reusing
`NavigationHelpers.OpenPath` with `selectItems`, which already exists for exactly this shape of
request.

Cases that need explicit handling rather than a generic fallback:

- **Delete and recycle** have no meaningful destination. The card must not be activatable; a no-op
  click that looks clickable is worse than an inert card.
- **Multi-item operations** should select all results in a shared parent, and fall back to the parent
  alone when they span several.
- **Failed and cancelled cards** should not offer to navigate to a result that does not exist.
- **A destination that has since been deleted or unmounted** needs to fail gracefully, not throw.

This is genuinely useful independent of preparation, is upstream-shaped, and touches no Cloud Guard
code. Two consequences follow. It is a better first upstream contribution than the preparation
feature itself, since it is small and uncontroversial. And it should be built and merged
*before* preparation, so that preparation's completed card is simply an instance of a general
behaviour rather than the reason the behaviour exists. See
[upstream-candidates.md](../upstream-candidates.md).

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
- **Versioning.** A branch build advances the **patch** number; merging a feature to `main` advances
  the **minor** number and resets the patch. Pilot is on `v0.1.4-poc`, so the first test build of
  this branch is `v0.1.5-poc` and merging this feature produces `v0.2.0-poc`. Patch numbers on a
  branch are consumed in order across branches; they are build counters, not per-branch sequences.
- Do **not** mirror an experimental build to R2 and do **not** bump the Chocolatey package. Those are
  the pilot distribution channel. Install by hand per the pilot runbook.
- `app.version` is already a telemetry resource attribute and the `Mode per device` dashboard panel
  groups by it, so an experimental build is distinguishable from pilot data rather than polluting it.
- **No workflow change is needed for the tag suffix.** `cd-poc.yml` derives the MSIX version with
  `('<tag>' -replace '^v', '') -replace '-.*$', ''`, so any suffix is stripped: `v0.1.5-alpha1` and
  `v0.1.5-poc` both yield `0.1.5.0`. The suffix is therefore invisible to Windows, and the patch
  number is what actually distinguishes builds. Keep patch numbers unique per build.
- **A test machine cannot go back.** MSIX upgrades must increase the version, so a machine that
  installs `0.1.5.0` cannot return to pilot `0.1.4.0` without uninstalling first. Since uninstall
  deliberately keeps `user_settings.json` and the `InstallationId`, that is recoverable, but it is a
  manual step. This is the concrete reason to test on a non-pilot machine.

This scheme belongs in `docs/release-and-deployment.md` alongside the rest of the release process. It
is recorded here instead because that file currently has uncommitted local edits, and it should be
promoted once those land.
