# Release and deployment

How a Cloud Guard POC build is produced, what it carries, and how it reaches a machine.

## Producing a release

```powershell
gh workflow run cd-poc.yml -R DailenG/Files -f tag=v0.1.1-poc -f prerelease=true -f default_mode=Observe
```

About ten minutes. Inputs:

| Input | Meaning |
|---|---|
| `tag` | Release tag to create. Also becomes the package version |
| `prerelease` | Marks the GitHub release as a prerelease |
| `default_mode` | Mode compiled into the build: `Observe` (default), `Protect` or `Off` |

## Package identity

`.github/scripts/Configure-AppxManifest.ps1 -Branch SideloadPoc` rewrites the manifest so the POC
installs beside production Files:

| Field | Value |
|---|---|
| `Identity/Name` | `FilesCloudGuard` |
| `Identity/Publisher` | The signing subject, from the `SIDELOAD_PUBLISHER_SECRET` secret. Must match the certificate exactly or the package will not install |
| `Identity/Version` | Derived from the tag: `v0.1.1-poc` -> `0.1.1.0`. Without this the build inherits the upstream Files version |
| `Properties/PublisherDisplayName` | The `CN` of the signing subject, so the name Windows shows always matches the certificate the package was signed with |
| `VisualElements/Description` | Credits Files by the Files Community (MIT and MPL-2.0) and links the fork |
| protocol / alias | `files-cloudguard:` / `files-cloudguard.exe` |

The upstream project is dual MIT and MPL-2.0. Both permit modified builds; neither grants trademark
rights, so the fork is named, described and published distinctly rather than under the upstream
Store publisher.

## Compiled-in values

Three constants in `CloudOptimizationSettingsService` are placeholders that
`Configure-AppxManifest.ps1` substitutes at packaging time, following the same mechanism upstream
uses for the Bing Maps, Sentry and GitHub OAuth secrets:

| Constant | Placeholder | Source |
|---|---|---|
| `BuiltInTelemetryToken` | `cloudguardtoken.secret` | `CLOUD_GUARD_INGEST_TOKEN` secret |
| `BuiltInTelemetryEndpoint` | `cloudguardendpoint.secret` | `CLOUD_GUARD_TELEMETRY_ENDPOINT` secret |
| `BuiltInDefaultMode` | `cloudguardmode.secret` | `default_mode` workflow input |

An unreplaced value still ends in `.secret` and is ignored, so a local developer build keeps the
loopback endpoint, no token, and `Off`. Runtime precedence for the token is
`FILES_CLOUD_GUARD_TOKEN`, then the credential vault, then the compiled-in value; `TelemetryEndpoint`
in `user_settings.json` overrides the compiled-in endpoint, and `FILES_CLOUD_GUARD_MODE` overrides
the mode for one process.

The ingest token is therefore extractable from any release binary. It is a pilot-scoped write
credential on an internal-only endpoint, not a secret. To rotate it, change
`/mnt/user/appdata/otel-ingress/.env`, update the `CLOUD_GUARD_INGEST_TOKEN` repository secret, and
publish a new build.

## Release assets

| Asset | Purpose |
|---|---|
| `Files.CloudGuard_<version>_x64.msixbundle` | The signed package |
| `Dependencies.zip` | Framework packages the bundle declares. `Create-MsixBundle.ps1` filters staged dependencies against the manifest's `PackageDependency` names, so unused packages such as VCLibs are no longer shipped and cannot cause `0x80073CF3` |
| `FilesCloudGuard.Deploy.zip` | The bundle plus those dependencies in one archive. This is what the Chocolatey package downloads: one URL and one checksum |
| `pilot-runbook.md` | Operator instructions |
| `FilesCloudGuard.cer` | Only when the build was self-signed |

## Chocolatey deployment

`PESFilesCloudGuard`, in the PES `chocolatey` repository under `packages/PESFilesCloudGuard`, is the
supported path for managed machines. It downloads `FilesCloudGuard.Deploy.zip`, enables trusted app
installs, provisions the Windows App Runtime 2 dependency when absent, provisions the bundle for all
users, and registers it per user through a logon scheduled task. Its `README.md` documents the
install and uninstall behaviour, including what uninstall deliberately keeps.

For a new release: publish the release, then bump `<version>` in the nuspec and the tag inside `$url`
in `tools\chocolateyinstall.ps1`. CI recalculates the checksum from that URL.
