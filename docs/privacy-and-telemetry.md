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

## Control and Configuration

1. **Operating Modes**:
   - `Off`: Telemetry is completely disabled. No spans, metrics, or logs are created.
   - `Observe`: Default Files behavior; telemetry recorded.
   - `Protect`: Protective behaviors active; telemetry recorded.
2. **Collector Unavailability**:
   - The telemetry pipeline uses non-blocking, bounded in-memory buffers.
   - If the collector is unreachable or missing, batches are dropped silently without impacting UI or file operations.
3. **Local Pilot Privacy**:
   - In distributed pilot builds, telemetry is strictly opt-in.
   - All telemetry endpoints point to local development collectors (`http://localhost:4318` or `4317`) unless explicitly configured.
