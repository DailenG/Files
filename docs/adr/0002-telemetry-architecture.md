# ADR 0002: Telemetry Architecture

## Status
Accepted

## Context
The POC needs evidence about Files-owned interactions with cloud-backed locations (Observe vs Protect) without slowing the UI, leaking identifying data, or coupling application code to an exporter vendor. Files targets Native AOT and trimming (`IsAotCompatible=true`), so any dependency must be AOT-safe.

## Decision

1. **Two layers, one seam.**
   - `Files.Shared/Cloud`: `ICloudInteractionTelemetry` and `CloudInteractionTelemetry` built on `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter`. No third-party dependency. Unit-testable with `ActivityListener`/`MeterListener`.
   - `Files.App/Services/Cloud/CloudTelemetryExportHost`: the only type that references the OpenTelemetry SDK. It subscribes to the `Files.Cloud` source/meter and exports over OTLP/HTTP. Swapping or removing the exporter touches one file.

2. **Mode gate.** `ICloudOptimizationSettingsService.Mode` (Off / Observe / Protect) is read on every record call through a delegate, so mode changes apply without restart for recording; export start is evaluated once at launch. `Off` is a strict no-op: no spans, no measurements. The `FILES_CLOUD_GUARD_MODE` environment variable overrides the persisted setting for development without recompiling.

3. **Record only cloud-backed locations.** `CloudLocationContext.IsCloudBacked == false` (local, standard SMB) is never recorded, so local traffic cannot appear on the cloud dashboard and the app pays nothing for local navigation beyond a boolean check.

4. **Bounded attributes only.** Every attribute value comes from `CloudTelemetryAttributes`: enum-to-string maps and buckets (`item_count_bucket`, `file_size_bucket`, `extension_group`). Raw paths, names, and exception messages have no code path into a tag. Exceptions are reduced to `CloudErrorCategory`.

5. **Never block, never throw.** Recording is wrapped in try/catch with a rate-limited warning (max 5 per process). Export uses the SDK batch processor (bounded queue 2048, 2 s export timeout, 10 s metric interval). An unreachable collector drops batches silently.

6. **Direct export, TLS off-box.** The export host accepts plaintext only for loopback endpoints (development against a local collector); any remote endpoint must be `https`, and an optional bearer token from the credential vault is attached. No local collector agent is shipped: it would only relocate the same HTTPS client into a second signed process, add an install/uninstall surface, and (with a disk queue) carry data out of the building to flush later. Retry is in-memory only, so unreachable-network periods export nothing. See #5 for the collector side.

7. **Identity.** Resource attributes are `service.name=files-cloud-guard`, `app.version`, `session.id` (per process), and `installation.id` (random GUID persisted in settings). No machine name, user, or SID.

8. **Dependency.** `OpenTelemetry` and `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.18.0, which are marked trim/AOT compatible (formal support landed in 1.17.0). Added only to `Files.App`.

## Alternatives considered
- **Reuse Sentry.** Already present, but it is an error-reporting pipeline with its own sampling and a third-party backend; not suitable for local metrics dashboards or Observe/Protect comparison.
- **OpenTelemetry types throughout the app.** Rejected: spreads vendor API into view models and complicates upstream contribution.
- **Custom file/CSV logger.** Rejected: no traces, no dashboards, reinvents batching and rotation.

## Consequences
- Pure telemetry logic is testable without a collector (see `tests/Files.Shared.Tests/CloudInteractionTelemetryTests.cs`).
- `Files.App` binary grows by the OTLP exporter and its protobuf dependency.
- Mode/endpoint/roots are currently editable only via `user_settings.json` or the environment variable; a settings UI is out of POC scope.
- `build.commit` is not yet emitted; packaging (#8) will decide how the commit is stamped into the build.

## Related
- Issue #4, ADR 0001, `docs/privacy-and-telemetry.md`
