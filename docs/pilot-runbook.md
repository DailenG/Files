# Files Cloud Guard: pilot runbook

Experimental build. It installs as **Files Cloud Guard (POC)** with its own identity, tile, protocol (`files-cloudguard:`) and alias (`files-cloudguard.exe`), so it does not touch an existing Files installation, its settings, or File Explorer. Uninstalling it leaves nothing behind.

## What it does

| Mode | Behaviour |
|---|---|
| `Off` (default) | Stock Files. No classification, no telemetry, no policy |
| `Observe` | Stock Files behaviour, plus measurement of cloud-backed interactions |
| `Protect` | On cloud-backed locations with hydration risk: thumbnails are served from the shell cache only (uncached items keep their type icon), the Preview pane shows a metadata card with a **Load full preview** button instead of reading the file, and search stays in the current folder instead of walking every subfolder, with a **Search all subfolders** button to run the deep search anyway |

Local disks and ordinary SMB shares are untouched in every mode.

## 1. Install

On a managed PES machine, install it from Chocolatey and skip the rest of this section:

```powershell
choco install PESFilesCloudGuard -y
```

That package provisions the bundle and the Windows App Runtime for every user, registers it for the
current user, and needs no further configuration. Its source lives in the PES `chocolatey` repository
under `packages/PESFilesCloudGuard`. To install by hand instead:

1. Download the `.msixbundle` and `Dependencies.zip` from the release, then `cd` to wherever they landed. Every command below is relative to that folder; nothing is written outside it. Builds are x64 only.
2. If the release includes `FilesCloudGuard.cer`, the build was self-signed and the certificate must be trusted first, from an elevated PowerShell:

   ```powershell
   Import-Certificate -FilePath .\FilesCloudGuard.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   Releases signed with Azure Trusted Signing have no `.cer` and need no such step.
3. Expand `Dependencies.zip` and install. The archive carries exactly the framework packages the bundle declares, which today is only `Microsoft.WindowsAppRuntime.2`:

   ```powershell
   Expand-Archive .\Dependencies.zip -DestinationPath .\Dependencies -Force
   Add-AppxPackage -Path (Get-ChildItem .\*.msixbundle).FullName `
     -DependencyPath .\Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix
   ```

   If the Windows App Runtime 2 is already present on the machine, `-DependencyPath` can be omitted entirely. Supplying a package the manifest does not declare is a hard failure (`0x80073CF3 ... provided but not used`), which is why the archive is now limited to declared dependencies.

4. Launch **Files Cloud Guard (POC)** from Start once, then close it. This creates its settings folder.

## 2. Configure

Release builds ship with the pilot collector endpoint and ingest token compiled in, and telemetry is on by default, so a pilot machine needs no configuration beyond choosing a mode. Settings live in `%LOCALAPPDATA%\Packages\FilesCloudGuard_<hash>\LocalState\settings\user_settings.json`; with the app closed, set:

```json
"Mode": "Observe"
```

`Mode` accepts `Off`, `Observe`, `Protect`. `EgnyteConfiguredRoots` is optional: Egnyte UNC paths and mapped drives are detected automatically, so set it only to force a folder to be treated as Egnyte.

Both compiled-in values can be overridden per machine: `TelemetryEndpoint` in `user_settings.json`, and the token through the credential vault entry `Files.CloudGuard.Telemetry` or, for one session:

```powershell
$env:FILES_CLOUD_GUARD_TOKEN = '<token>'
```

Precedence is environment variable, then credential vault, then the compiled-in token. Locally built (non-release) packages have no token and default to `http://localhost:4318`.

`FILES_CLOUD_GUARD_MODE` overrides `Mode` for one process, which is the quickest way to compare modes back to back.

Restart the app. `%LOCALAPPDATA%\Packages\FilesCloudGuard_<hash>\LocalState\debug.log` should contain `Cloud telemetry export started in Observe mode.`

## 3. What to do during the pilot

1. Work normally for a few days in `Observe`. Browse the folders that feel slow, with the layout and Preview pane you would normally use.
2. Switch to `Protect` and repeat the same kind of work.
3. Report anything that looks wrong: a thumbnail that should have appeared, a preview that would not load after clicking the button, or a slowdown.

Telemetry only leaves the machine while it is on the corporate network or VPN; `otel.pesengineers.dev` resolves to a private address. Nothing is queued to disk, so off-network periods simply record nothing.

## 4. Rollback

Any of these, in increasing order of severity:

| Situation | Action |
|---|---|
| Protect mode is in the way | Set `"Mode": "Off"` (or `"Observe"`) and restart the app |
| Stop all measurement, keep the app | Set `"TelemetryEnabled": false` and restart |
| Remove the app entirely | `choco uninstall PESFilesCloudGuard -y`, or `Get-AppxPackage FilesCloudGuard* \| Remove-AppxPackage` for a manual install |

A manual uninstall removes the package and its `LocalState`, including the settings file and the log. The Chocolatey uninstall keeps each profile's `LocalState`, so a reinstall retains the pilot mode and the telemetry `InstallationId`; it also leaves the Windows App Runtime installed. Remove the credential vault entry with `cmdkey /list` and `cmdkey /delete` if it was persisted, and delete `FilesCloudGuard.cer` from `Cert:\LocalMachine\TrustedPeople` if it was imported. Nothing outside the package is modified, so production Files and File Explorer are unaffected by the uninstall.

## 5. Privacy

No file paths, file names, folder names, tenant names, user names, host names, IP addresses, search terms, or file contents are recorded or exported. Measurements are bounded values only: operation kind, provider kind, a coarse size bucket, an extension group such as `image` or `cad`, an outcome, and a duration. The full rules, the exported attribute list, retention and deletion are in [privacy-and-telemetry.md](privacy-and-telemetry.md).

## 6. Known limitations

- Uncached thumbnails on protected cloud locations show type icons rather than image previews until the file is opened.
- The Preview pane requires one click for cloud files in Protect mode.
- Search on a protected cloud location covers the current folder only until **Search all subfolders** is clicked.
- Only Egnyte is classified as a hydration-risk provider. Other cloud providers currently behave as stock Files.
- Only Files' own activity is measured. Search indexers, antivirus, backup agents and Explorer are invisible to it.
- The app does not update itself. New builds are installed from a new release.
- Installed builds carry the collector endpoint, ingest token and default mode compiled in. Changing any of them means publishing a new build; see [release-and-deployment.md](release-and-deployment.md).
