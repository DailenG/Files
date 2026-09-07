# Privacy and Telemetry Specification

## Purpose and Scope

Files Cloud Guard introduces cloud-interaction telemetry solely to support evidence-based engineering, observe-mode baselining, and validation of passive hydration protections during development and controlled pilots.

**This is not general user tracking or analytics.**

## Strict Exclusion Rules

The following data categories are **strictly prohibited** from ever being collected, logged, or exported by Files Cloud Guard:

- **Raw file system paths** (e.g. `E:\Projects\ClientA\Secret.dwg`).
- **File names and directory names**.
- **File contents or metadata payloads** (e.g. document titles, authors, comments).
- **Tenant names, tenant domains, or customer identifiers**.
- **Usernames, domain accounts, and Windows SIDs**.
- **Hostnames and computer names**.
- **IP addresses**.
- **Search terms or filter queries**.
- **Full exception messages** that might contain file paths or user data.

## Permitted Attributes (Bounded Cardinality)

All exported telemetry uses normalized, coarse-grained buckets or predefined enums:

| Dimension | Allowed Values / Format |
|---|---|
| `provider.kind` | `egnyte`, `local`, `standard_network`, `other_cloud`, `unknown` |
| `optimization.mode` | `off`, `observe`, `protect` |
| `operation.name` | `navigate`, `enumerate_directory`, `request_thumbnail`, `request_preview`, `read_basic_metadata`, `open_file` |
| `access.origin` | `navigation`, `folder_display`, `visible_item`, `selection_changed`, `hover`, `restored_tab`, `explicit_button` |
| `access.is_explicit` | `true`, `false` |
| `policy.decision` | `allowed`, `observed`, `cached_only`, `deferred`, `generic_fallback`, `blocked`, `not_applicable` |
| `operation.outcome` | `success`, `failure`, `cancelled` |
| `duration_ms` | Numeric duration in milliseconds |
| `thumbnail.result` | `cache_hit`, `cache_miss`, `generic_fallback`, `not_attempted` |
| `preview.result` | `deferred`, `explicit_load`, `cancelled`, `direct_load` |
| `item_count_bucket` | `0`, `1-10`, `11-100`, `101-1000`, `1001+` |
| `file_size_bucket` | `unknown`, `<1MB`, `1-10MB`, `10-100MB`, `100MB-1GB`, `1GB+` |
| `extension_group` | `office`, `pdf`, `image`, `video`, `audio`, `cad`, `bim`, `archive`, `text`, `code`, `shortcut`, `other` |
| `app.version` | Semantic version string (e.g. `0.1.0-poc`) |
| `session.id` | Ephemeral UUID generated on app startup |
| `installation.id` | Random GUID generated once per installation and stored in `user_settings.json`; not derived from hardware or user identity |

## Control and Configuration

Settings live in the Files `user_settings.json` file under `%LOCALAPPDATA%\Packages\<package>\LocalState\settings\` and are read by `ICloudOptimizationSettingsService`:

| Key | Default | Meaning |
|---|---|---|
| `Mode` | `Off` | `Off`, `Observe`, or `Protect` |
| `TelemetryEnabled` | `false` | Export to the collector. Opt-in. No effect when `Mode` is `Off` |
| `TelemetryEndpoint` | `http://localhost:4318` | OTLP/HTTP base URL; `/v1/traces` and `/v1/metrics` are appended. Plaintext `http` is accepted only for loopback; any other host must be `https`. Redirects are never followed |
| `EgnyteConfiguredRoots` | `[]` | Administrator override roots treated as Egnyte; empty means automatic detection |
| `InstallationId` | generated | Random identifier; delete the key to rotate it |
| (credential vault) `Files.CloudGuard.Telemetry` | none | Bearer token sent as `Authorization` on every export. Set via `ICloudOptimizationSettingsService.TelemetryAuthToken` or the `FILES_CLOUD_GUARD_TOKEN` environment variable; never written to `user_settings.json` |

The environment variable `FILES_CLOUD_GUARD_MODE` (`Off`/`Observe`/`Protect`) overrides `Mode` for the process lifetime. This is intended for development and controlled tests.

1. **Operating Modes**:
   - `Off`: No spans or measurements are created. The exporter is not started.
   - `Observe`: Stock Files behavior; cloud-backed interactions are recorded.
   - `Protect`: Interactions and policy decisions are recorded. Enforcement is not implemented yet; behavior currently matches `Observe`.
2. **What is recorded**: only operations whose location classifies as cloud-backed (`IsCloudBacked`). Local disks and standard network shares produce no telemetry.
3. **Collector Unavailability**: recording uses bounded in-memory batching (queue 2048 spans, 2 s export timeout, 10 s metric interval). An unreachable collector drops data silently; nothing is spooled to disk, so a machine that cannot reach the internal collector (for example, off the corporate network) exports nothing. The UI and file operations are never blocked.
4. **Failure isolation**: telemetry recording errors are swallowed and logged at most five times per process as a warning that contains only the exception type name.

## Retention and Deletion

- Files itself retains no telemetry. Data exists only in the internal SigNoz instance described in `ops/observability/README.md` (see #5); retention is enforced there (traces 1 month, metrics 3 months) and deleting `/mnt/user/appdata/signoz-aio/clickhouse` deletes all telemetry.
- Application logs (`debug.log`) may contain the warning described above but never telemetry payloads.
- To disable completely: set `Mode` to `Off` (or `TelemetryEnabled` to `false`) and restart Files. To rotate the installation identifier, remove `InstallationId` from `user_settings.json`.

## Known Limitations

- Only Files-owned activity is observed. Windows Search, Defender, EDR, DLP, backup agents, Office recent-file handlers, Explorer, and other applications are invisible to this telemetry.
- Mode changes apply to recording immediately, but export start/stop is evaluated at launch; restart Files after changing `TelemetryEnabled` or `TelemetryEndpoint`.
- Path-correlation tokens (HMAC of normalized paths) are not implemented in the POC.
- Transport security ends at the internal collector. Loopback export (development) is plaintext by design; the pilot endpoint is HTTPS with a bearer token checked by the reverse proxy in front of SigNoz. Any process running as the same Windows user can read the token from the credential vault, which is the same trust boundary as the user's own files.
