# Files Cloud Guard: proof of concept

**Experimental. For controlled pilot use only.** This is a fork of Files, not an official Files release, and it is not supported by the Files community.

It installs as **Files Cloud Guard (POC)** with its own package identity, protocol (`files-cloudguard:`) and execution alias, so it runs alongside an existing Files installation and File Explorer without touching either one or their settings.

## What this build does

Navigating a virtual cloud filesystem such as the Egnyte Desktop App can download file content without the user asking for it: the shell reads whole files to make thumbnails, and the Preview pane reads whatever is selected. On a folder of drawings or PDFs that is tens or hundreds of megabytes of background traffic.

Three modes:

- **Off** — stock Files. Nothing is classified, measured or changed.
- **Observe** — stock behaviour, plus measurement of how much hydration is happening.
- **Protect** — on cloud-backed locations: thumbnails come from the shell cache only and uncached items keep their type icon; the Preview pane shows a metadata card with a **Load full preview** button instead of reading the file on selection; search covers the current folder only, with a **Search all subfolders** button for the deep search.

Release builds ship in **Observe** by default, with the collector endpoint and ingest token compiled in. Change the mode in `user_settings.json`, or per process with `FILES_CLOUD_GUARD_MODE`.

Local disks and ordinary SMB shares behave exactly as stock Files in all three modes.

## Measured effect

On a real Egnyte drive, opening a folder in grid view with the Preview pane open:

| Mode | Folder | Egnyte local cache |
|---|---|---|
| Observe | 33 files, 19.0 MB | 0.03 MB to 25.07 MB |
| Protect | 35 files, 14.4 MB | 25.07 MB, unchanged |

Protect moved zero bytes for equivalent browsing.

## Privacy

Telemetry is opt-in, off by default, and exported only to an internal collector reachable on the corporate network or VPN. No paths, file or folder names, tenant or user names, host names, IP addresses, search terms or file contents are recorded or exported; values are bounded buckets and enumerations only. See `docs/privacy-and-telemetry.md`.

## Install, configure, roll back

See `pilot-runbook.md`, included as a release asset.

## Known limitations

- Uncached thumbnails on protected cloud locations show type icons instead of image previews.
- Cloud files need one click in the Preview pane in Protect mode, and search on them stays in the current folder until **Search all subfolders** is clicked.
- Only Egnyte is classified as a hydration-risk provider.
- Only Files' own activity is measured; other processes on the machine are invisible to it.
- No self-update. New builds are installed from a new release.
- If the release includes `FilesCloudGuard.cer`, the package is self-signed and the certificate must be trusted before installing.
