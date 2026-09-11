# Archive Navigation on Cloud Storage: UX Proposals

Four options to put in front of pilot users, with the tradeoffs they should be asked to weigh.
Nothing here is implemented.

## The actual mechanism

The common description of this problem is "Files has to extract the ZIP to traverse into it". That is
not what happens, and the difference matters for which fixes are worth building.

Files browses archives in place. `ZipStorageFolder` opens a read stream over the `.zip` and reads the
archive index through `SevenZipExtractor`; nothing is written to disk and nothing is unpacked
(`src/Files.App/Utils/Storage/StorageItems/ZipStorageFolder.cs`). So there is no "extracting locally"
stage to report, and a message saying so would be inventing a stage that does not exist.

What the user is actually waiting on, in order:

1. **Whole-file hydration.** On an Egnyte placeholder, opening a read stream makes the provider fetch
   the file. A ZIP's central directory lives at the *end* of the file, and the extractor seeks, so
   the cost is the whole archive regardless of how little of it the user wants. A 1 GB archive is a
   1 GB download to show a folder listing. This is the dominant cost and it is not reducible by
   being cleverer about the read pattern.
2. **Index read.** Parsing the archive index once the bytes are local. Fast, and invisible next to
   stage 1.

Two findings worth knowing before choosing an option:

- **The cost is paid twice on the first open.** `FromPathAsync` calls `CheckAccess(containerPath)`,
  which constructs a `SevenZipExtractor` purely to decide whether the path is browsable, then throws
  it away. `GetItemsAsync` then calls `OpenZipFileAsync()` and opens the archive again. The access
  probe alone is enough to force full hydration.
- **Every level of traversal re-opens the archive.** `GetItemsAsync` opens a fresh extractor per
  navigation, so moving between folders *inside* the archive re-reads the index each time. Locally
  the OS page cache hides this; on a cold cloud file it does not.

Neither of those is a UX question, and both are worth fixing whichever option wins. They are listed
at the end.

## What to ask users

The options differ on one axis that engineering cannot decide: **should Files ever download a large
cloud archive without asking?** Everything else follows from the answer. Present the options as a
group, not one at a time, and drive the session with these questions:

1. When you double-click a large archive on Egnyte, do you want it to just start, or do you want to
   be told the cost first and choose?
2. Is being able to cancel mid-download important, or would you rather it never blocked you at all?
3. At what size does this stop being "barely noticeable" and become "I need to know what is going
   on"? Ask for a number, then sanity-check it against their real project archives.
4. Does it matter whether the archive is on Egnyte versus a local drive, or should the behaviour be
   the same everywhere?
5. Have you ever opened an archive by accident and wanted to back out?

Question 3 is the one that decides the threshold, and the answer will differ between an engineer
opening a 30 MB drawing set and someone opening a 2 GB point-cloud archive. Collect numbers, not
adjectives.

## Option 1: Pre-flight consent card

**What the user sees.** Double-clicking a large archive on a cloud location does not navigate.
Instead the view shows a card naming the cost and offering choices: "This archive is 1.2 GB and
stored on Egnyte. Opening it downloads the whole file." with **Download and open**, **Extract
to...**, and **Cancel**. Small archives, and archives already downloaded, open immediately with no
card.

**Why it addresses the real cost.** It is the only option that lets the user avoid the download
entirely. The other three make a 1 GB transfer more pleasant; this one makes it optional. It also
turns an accidental double-click into a no-op, which is the cheapest possible outcome.

**Cost.** Moderate. Needs a size and location check before navigation, a card, and a threshold
setting.

**Risks and objections.** It is the only option that changes what a double-click does, so it is the
most likely to annoy people who just want the file open. The threshold has to be right or it is
either useless or constant friction. Users who work in archives all day may find it insufferable;
users who browse occasionally may find it a relief. Expect this to split the room, and that split is
the most useful thing the session can produce.

**Feasibility.** Fits the existing Cloud Guard seam directly. It is the same shape as the deferred
preview already shipped (`UserControls/FilePreviews/DeferredPreview.xaml`): classify the location,
decline the passive operation, offer an explicit button. Location classification and the
explicit-versus-passive distinction already exist.

## Option 2: Staged progress with cancel

**What the user sees.** Navigation proceeds, and a progress surface names the honest stage and the
real numbers: "Downloading from Egnyte, 340 MB of 1.2 GB", then briefly "Reading archive contents",
with a **Cancel** button throughout. Cancelling returns to the previous folder.

**Why it addresses the real cost.** The complaint is not only the wait, it is not knowing whether
anything is happening or how long is left. A byte count converts an indefinite hang into a bounded,
escapable wait.

**Cost.** Moderate to high, and the reason is worth being explicit about: the cloud provider does not
report hydration progress to us. To show a real byte count, Files would have to hydrate the file
deliberately, reading it in chunks and reporting bytes as it goes, before handing the file to the
extractor. That is a genuine change to how the archive is opened, not a label on top of the existing
path. Without it the only honest UI is an indeterminate spinner with a label, which is Option 4 with
extra words.

**Risks and objections.** A deliberate chunked read must not end up slower than letting the provider
hydrate in its own way; that needs measuring on real Egnyte files before committing. Cancel
semantics need thought: cancelling the read does not un-download what Egnyte already cached, so the
next attempt may be much faster, which is good but needs to not look like a bug.

**Feasibility.** The progress surface itself is routine. `IsLoadingItems` and `ItemLoadStatusChanged`
in `ShellViewModel` already gate a progress indicator during enumeration, and the info-bar pattern is
established. The hard part is the chunked hydration, not the UI.

## Option 3: Non-blocking background open

**What the user sees.** Double-clicking a large cloud archive keeps you where you are. A job appears
in the Status Center, "Opening project-archive.zip", and you carry on browsing. When it is ready you
get an affordance to enter it, or it opens in a new tab.

**Why it addresses the real cost.** It takes the wait off the critical path. The user is never
blocked, never watching a bar, and can queue several archives.

**Cost.** Moderate. The job plumbing largely exists.

**Risks and objections.** The weakest feedback loop of the four: a user who double-clicks and sees
nothing happen in the file list may well double-click again, or conclude it is broken. Needs a clear
immediate acknowledgement at the click site, which starts pulling Option 4 in. Also raises a question
users should answer: when it finishes, should it navigate for you, open a tab, or wait quietly? Doing
it automatically means the view can change under someone who has moved on.

**Feasibility.** `StatusCenterViewModel.AddItem` already provides progress-reporting background jobs
with taskbar integration, which is exactly the surface this needs.

## Option 4: Ambient in-place feedback

**What the user sees.** No policy change and no dialog. The pointer switches to a busy cursor, the
file list shows skeleton placeholder rows where the archive contents will appear, and the details
pane shows a shimmer instead of blank space. Everything else behaves exactly as today.

**Why it addresses the real cost.** It does not. It makes the existing wait legible rather than
shorter or avoidable. That is still worth something: much of the frustration is ambiguity about
whether the click registered.

**Cost.** Low. The cheapest of the four by a wide margin, and the only one that ships without a
behaviour decision.

**Risks and objections.** On a 1 GB archive over a slow link, a shimmer for ninety seconds with no
byte count and no cancel may read as a hang regardless. It is a floor, not a fix. Worth proposing
honestly as such rather than overselling it.

**Feasibility.** Cursor changes are established via `ChangeCursor(InputSystemCursor.Create(...))`,
used in `MainPage.xaml.cs` and the layout pages. One caveat on the animated-cursor idea specifically:
`InputSystemCursor` exposes only the built-in system shapes, so a custom "box opening" animation
would need a resource cursor loaded from a module rather than the API already in use here. The busy
and wait shapes are free; a bespoke animation is a separate, larger piece of work, and Windows
convention would argue for the standard busy cursor anyway.

## How these combine

They are not mutually exclusive, and the session should say so.

- **Option 4 is compatible with all three others** and arguably belongs in whichever wins.
- **Options 1 and 2 pair naturally**: consent first, then honest progress once consent is given.
- **Options 1 and 3 conflict in spirit.** One says "ask before spending", the other says "spend, but
  do not block". A user who wants both is really asking for a consent card whose confirm button
  starts a background job, which is a fine answer but should be a deliberate choice rather than an
  accident.
- **Options 2 and 3 overlap.** Both need progress reporting; they differ only on whether the user
  watches it.

## Worth fixing regardless

These need no user input and no design decision. They are engineering defects that make every option
above perform worse than it should.

1. **Do not pay for the archive twice on first open.** The `CheckAccess` probe in `FromPathAsync`
   forces full hydration just to answer "is this browsable", then discards the extractor that
   `GetItemsAsync` immediately recreates. Reusing that work, or deciding browsability without a full
   read, removes one whole-file cost from the first open.
2. **Cache the archive index per container.** `GetItemsAsync` opens a fresh extractor for every
   navigation, so moving around inside an archive re-reads the index repeatedly. An index cache keyed
   on container path with invalidation on write would make in-archive navigation feel instant after
   the first open. Note the fork already caches per-container encoding in the same class, so there is
   a precedent for keying state this way.

Both are upstream-shaped, not Egnyte-specific: they are simply invisible on local disks because the
page cache absorbs them. If either is pursued, check it against
[upstream-candidates.md](upstream-candidates.md).

## Measuring the outcome

Whichever option is chosen, the Cloud Guard telemetry already in the build can tell whether it
worked, and the instrumentation for it is mostly in place. `files.cloud.operations` records
`enumerate_directory` with duration and an item-count bucket, so archive navigation on a classified
location is already measurable today. A baseline should be collected before building any of this, so
that "it feels better" can be checked against how long these operations actually take and how often
they are cancelled.
