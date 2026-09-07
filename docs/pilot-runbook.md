# Files Cloud Guard: pilot runbook

Experimental build. It installs as **Files Cloud Guard (POC)** with its own identity, tile, protocol (`files-cloudguard:`) and alias (`files-cloudguard.exe`), so it does not touch an existing Files installation, its settings, or File Explorer. Uninstalling it leaves nothing behind.

## What it does

| Mode | Behaviour |
|---|---|
| `Off` (default) | Stock Files. No classification, no telemetry, no policy |
| `Observe` | Stock Files behaviour, plus measurement of cloud-backed interactions |
| `Protect` | On cloud-backed locations with hydration risk: thumbnails are served from the shell cache only (uncached items keep their type icon), and the Preview pane shows a metadata card with a **Load full preview** button instead of reading the file |

Local disks and ordinary SMB shares are untouched in every mode.

## 1. Install

1. Download the `.msixbundle` and `Dependencies.zip` from the release, then `cd` to wherever they landed. Every command below is relative to that folder; nothing is written outside it. Builds are x64 only.
2. If the release includes `FilesCloudGuard.cer`, the build was self-signed and the certificate must be trusted first, from an elevated PowerShell:

   ```powershell
   Import-Certificate -FilePath .\FilesCloudGuard.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   Releases signed with Azure Trusted Signing have no `.cer` and need no such step.
3. Expand `Dependencies.zip` and install. The package declares exactly one dependency, `Microsoft.WindowsAppRuntime.2`; the archive also carries VCLibs packages this build does not use, and passing those makes deployment fail with `0x80073CF3 ... provided but not used`:

   ```powershell
   Expand-Archive .\Dependencies.zip -DestinationPath .\Dependencies -Force
   Add-AppxPackage -Path (Get-ChildItem .\*.msixbundle).FullName `
     -DependencyPath .\Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix
   ```

   If the Windows App Runtime 2.4 is already present on the machine, `-DependencyPath` can be omitted entirely.

4. Launch **Files Cloud Guard (POC)** from Start once, then close it. This creates its settings folder.

## 2. Configure

Settings live in `%LOCALAPPDATA%\Packages\FilesCloudGuard_<hash>\LocalState\settings\user_settings.json`. With the app closed, add:

```json
"Mode": "Observe",
"TelemetryEnabled": true,
"TelemetryEndpoint": "https://otel.pesengineers.dev:8443"
```

`Mode` accepts `Off`, `Observe`, `Protect`. `EgnyteConfiguredRoots` is optional: Egnyte UNC paths and mapped drives are detected automatically, so set it only to force a folder to be treated as Egnyte.

The ingest token is never written to that file. Set it once per user:

```powershell
$env:FILES_CLOUD_GUARD_TOKEN = '<token from the observability owner>'
```

for a single session, or persist it through the app's credential vault entry `Files.CloudGuard.Telemetry`. Without a token the exporter is refused by the collector (HTTP 401) and Files carries on unaffected.

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
| Remove the app entirely | `Get-AppxPackage FilesCloudGuard* \| Remove-AppxPackage` |

Uninstalling removes the package and its `LocalState`, including the settings file and the log. Remove the credential vault entry with `cmdkey /list` and `cmdkey /delete` if it was persisted, and delete `FilesCloudGuard.cer` from `Cert:\LocalMachine\TrustedPeople` if it was imported. Nothing outside the package is modified, so production Files and File Explorer are unaffected by the uninstall.

## 5. Privacy

No file paths, file names, folder names, tenant names, user names, host names, IP addresses, search terms, or file contents are recorded or exported. Measurements are bounded values only: operation kind, provider kind, a coarse size bucket, an extension group such as `image` or `cad`, an outcome, and a duration. The full rules, the exported attribute list, retention and deletion are in [privacy-and-telemetry.md](privacy-and-telemetry.md).

## 6. Known limitations

- Uncached thumbnails on protected cloud locations show type icons rather than image previews until the file is opened.
- The Preview pane requires one click for cloud files in Protect mode.
- Only Egnyte is classified as a hydration-risk provider. Other cloud providers currently behave as stock Files.
- Only Files' own activity is measured. Search indexers, antivirus, backup agents and Explorer are invisible to it.
- The app does not update itself. New builds are installed from a new release.
