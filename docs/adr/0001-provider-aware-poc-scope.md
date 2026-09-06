# ADR 0001: Provider-Aware Proof-of-Concept Scope

## Status
Accepted

## Context
Files interacts with the Windows filesystem using standard Win32, WinRT, and shell APIs. When users navigate virtual cloud filesystems like the Egnyte Desktop App, passive operations—such as listing items, rendering thumbnail views, and updating the Preview Pane—can cause passive hydration of remote files.

A complete multi-cloud provider integration, kernel minifilter, or full cloud API implementation would require months of development and substantial architectural disruption to Files, making upstream contribution difficult or impossible.

We need an incremental, measurable proof of concept achievable in 1–2 development iterations that delivers immediate value on Egnyte while proving the generic seams for future upstream adoption.

## Decision

1. **Focus initially on Egnyte Desktop App**:
   - Egnyte presents well-defined UNC (`\\EgnyteDrive\`) and mapped drive interfaces.
   - It is widely used in AEC and enterprise environments where file sizes (PDFs, CAD, BIM) make unintended hydration particularly painful.

2. **Narrow the POC to two key passive hydration vectors**:
   - **Thumbnail Generation**: In Protect mode on Egnyte, request only cached thumbnails from the Windows shell cache. If uncached, fall back immediately to high-quality generic extension/type icons without hydrating content.
   - **Preview Pane**: In Protect mode on Egnyte, replace automatic content-reading preview dispatch with a lightweight deferred metadata card displaying safe directory metadata and an explicit **[Load Preview]** action.

3. **Adopt a Tri-State Operating Mode**:
   - **Off**: Standard Files behavior, no telemetry overhead.
   - **Observe**: Standard Files behavior, collects privacy-safe telemetry on cloud interactions to establish baseline metrics.
   - **Protect**: Applies Egnyte thumbnail and preview protections alongside telemetry.

4. **Isolate behind Clean Seams**:
   - Create `ICloudLocationClassifier` to classify paths without opening file contents or hardcoding drive letters.
   - Create `ICloudInteractionTelemetry` using standard .NET `ActivitySource` and `Meter` primitives.
   - Keep provider-specific logic in concrete implementations; do not pollute general UI/ViewModel code with `if (path.Contains("Egnyte"))`.

5. **Defer non-critical capabilities to Stage 2**:
   - Recursive search guards.
   - Automatic folder sizing suppression.
   - Tree expansion watchers.
   - AEC-specific custom format handling.
   - Egnyte Cloud API / OAuth integrations.

## Alternatives Considered

- **Complete Cloud Provider Framework**: High upfront complexity, high risk of rejection by upstream Files maintainers, slows time-to-value.
- **Filesystem Minifilter Driver**: Out of scope; requires kernel-mode signing and driver installation, inappropriate for a WinUI user-mode application fork.
- **Disabling Thumbnails Globally**: Degrades user experience on local drives where thumbnails are fast and free.

## Consequences

- **Positive**:
  - Immediate relief from passive hydration on Egnyte drives.
  - Safe, zero-risk fallback to generic behavior if classification is uncertain.
  - Clean, minimal diffs suitable for upstream submission.
  - Objective before-and-after comparison via Observe vs. Protect telemetry.
- **Negative / Limitations**:
  - Uncached thumbnails on Egnyte display generic icons instead of visual previews until opened or explicitly retrieved.
  - Users must click **Load Preview** to inspect file content on Egnyte when the Preview Pane is open.
