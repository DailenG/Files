# Files Cloud Guard (Fork)

> **Non-Affiliation Notice:**  
> This repository is an independent, experimental fork of [Files](https://github.com/files-community/Files) (`files-community/Files`).  
> It is **not** endorsed, certified, sponsored, maintained, or officially supported by the Files Community or Egnyte, Inc.  
> All product names, logos, and brands are property of their respective owners.

---

## Project Overview

**Files Cloud Guard** is an experimental fork of Files evaluating provider-aware controls to reduce passive file access on virtual cloud filesystems, initially focused on **Egnyte Desktop App** drives.

When navigating virtual cloud drives, standard Windows Explorer or Files operations—such as opening a directory, scrolling through items, generating thumbnails, or displaying the Preview Pane—can inadvertently trigger remote file hydration (downloading content), exhausting local disk cache and bandwidth.

Files Cloud Guard introduces:
1. **Egnyte Location Classification**: Safe, non-blocking identification of Egnyte virtual paths and roots without reading file contents.
2. **Tri-State Operating Modes**:
   - **Off**: Standard Files behavior.
   - **Observe**: Standard Files behavior while collecting privacy-safe metrics and traces.
   - **Protect**: Egnyte-aware protections active (cached-only thumbnails, deferred preview cards).
3. **Cached-Only Thumbnails on Egnyte**: Only queries existing local/Windows thumbnail caches; falls back cleanly to generic icons on cache misses instead of hydrating files.
4. **Explicit Preview Loading**: Shows safe directory metadata in the Preview Pane with an explicit **[Load Preview]** action, avoiding passive background downloads.
5. **Local Observability**: Integrated OpenTelemetry metrics and traces exportable to a lightweight local collector/dashboard.

---

## Documentation

- [Project Charter](docs/project-charter.md)
- [Architecture Decision Records (ADR)](docs/adr/)
  - [ADR 0001: Provider-Aware POC Scope](docs/adr/0001-provider-aware-poc-scope.md)
- [Privacy and Telemetry Specification](docs/privacy-and-telemetry.md)
- [Upstream Synchronization Guide](docs/upstream-sync.md)

---

## Original Project & Upstream

For information on the upstream Files project, installation of official releases, and upstream documentation, visit [files.community](https://files.community) or the [upstream GitHub repository](https://github.com/files-community/Files).
