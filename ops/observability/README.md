# Cloud Guard Observability: SigNoz on Unraid

Files exports OTLP/HTTP directly from `CloudTelemetryExportHost` to an internal SigNoz instance. There is no local collector agent on the Windows side; see ADR 0002 item 6 for why.

```text
Files.exe  --HTTPS + bearer-->  Nginx Proxy Manager (Unraid)  --docker bridge-->  signoz-aio :4318
                                                                                   signoz-aio :8080 (UI, VPN only)
```

Line of sight is the access control: `signoz.<domain>` resolves to the Unraid LAN address, so machines outside the network fail DNS or TCP, and the exporter drops its 2 s batch. Nothing is queued to disk.

## 1. SigNoz container

Community Apps: install **signoz-aio** (JSONbored). It bundles SigNoz UI/API, the signoz-otel-collector, ClickHouse, and ZooKeeper in one container.

| Setting | Value | Why |
|---|---|---|
| Network | `bridge` (the same custom bridge as Nginx Proxy Manager) | Ingest ports stay off the LAN |
| Port `8080` | do not publish to the host | UI reached through NPM on a VPN-only hostname, or via the Unraid host on the docker network |
| Port `4317` | do not publish | gRPC ingest is unused |
| Port `4318` | do not publish | Reached only from NPM on the bridge |
| ClickHouse data path | cache/SSD share, e.g. `/mnt/cache/appdata/signoz/clickhouse` | ClickHouse on the array is slow |
| Image tag | pin a specific version, not `latest` | Reproducible pilot |

After first start, create the admin account in the UI, then set retention under **Settings -> General**: traces 7 days, metrics 30 days. These values are quoted in `docs/privacy-and-telemetry.md`; change both together.

## 2. DNS and certificate

Use the Cloudflare-hosted `.dev` zone.

1. Create `A signoz.<domain>` pointing at the Unraid LAN IP. Proxy status: DNS only (grey cloud). A public record with a private address is fine; it simply does not resolve to anything reachable from outside the LAN. If the network already has an internal DNS override for the domain, that works equally well.
2. Create a Cloudflare API token scoped to `Zone:DNS:Edit` for that zone only.
3. In Nginx Proxy Manager: **SSL Certificates -> Add -> Let's Encrypt**, domain `signoz.<domain>`, **Use a DNS Challenge**, provider Cloudflare, paste the token. Windows trusts Let's Encrypt out of the box, so no CA is shipped with Files.

`.dev` is on the HSTS preload list; browsers refuse plaintext for it, which is a feature here.

## 3. Reverse proxy (Nginx Proxy Manager)

Create one Proxy Host:

- Domain: `signoz.<domain>`
- Forward: `http://signoz-aio:4318`
- SSL: the certificate above, Force SSL on, HTTP/2 on
- Advanced tab: paste the contents of `npm-ingest.conf` from this directory.

`npm-ingest.conf` does three things: requires `Authorization: Bearer <token>` on every request, exposes only `/v1/traces` and `/v1/metrics`, and answers `404` to everything else. Generate the token once:

```powershell
-join ((1..48) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) })
```

Put it in both `if` comparisons of `npm-ingest.conf`. One token per pilot cohort; rotating it means editing the proxy host and pushing the new value to pilots.

Do not put the UI on this hostname. If the UI needs a name, create a second Proxy Host (`signoz-ui.<domain>` -> `http://signoz-aio:8080`) on an NPM Access List restricted to the VPN subnet.

## 4. Windows pilot configuration

In `user_settings.json` (`%LOCALAPPDATA%\Packages\<package>\LocalState\settings\`):

```json
"Mode": "Observe",
"TelemetryEnabled": true,
"TelemetryEndpoint": "https://signoz.<domain>"
```

The token is not in that file. Set it once per user through either:

- environment variable `FILES_CLOUD_GUARD_TOKEN` (development), or
- `ICloudOptimizationSettingsService.TelemetryAuthToken`, which stores it in the Windows credential vault under `Files.CloudGuard.Telemetry`.

Restart Files. `debug.log` shows `Cloud telemetry export started in Observe mode.`

For local development without SigNoz, leave `TelemetryEndpoint` at `http://localhost:4318` and run any OTLP receiver on loopback; plaintext is accepted there only.

## 5. Verification

From a pilot machine:

```powershell
# TLS chain and token check; expect 401 then 200 (empty protobuf body is a valid, empty export)
curl.exe -s -o NUL -w "%{http_code}`n" https://signoz.<domain>/v1/traces -X POST -H "Content-Type: application/x-protobuf" --data-binary ""
curl.exe -s -o NUL -w "%{http_code}`n" https://signoz.<domain>/v1/traces -X POST -H "Content-Type: application/x-protobuf" -H "Authorization: Bearer <token>" --data-binary ""
# anything else is hidden
curl.exe -s -o NUL -w "%{http_code}`n" https://signoz.<domain>/api/v1/version
```

Then browse a cloud-backed location in Files (Observe mode) and open SigNoz **Services**: `files-cloud-guard` appears within 15 s. Metrics are under **Dashboards -> New -> Query builder**, metric names `files.cloud.*`.

## 6. Dashboards

`dashboards/` holds SigNoz dashboard exports. Import through **Dashboards -> New Dashboard -> Import JSON**. Panels compare Observe vs Protect through a `mode` variable:

- operations per minute by `provider` and `operation`
- p50 / p95 of `files.cloud.operation.duration`
- `files.cloud.policy.decisions` by `decision`
- thumbnail `cache_hits` / `cache_misses` / `generic_fallbacks` ratio
- preview `deferred` vs `explicit_loads`
- `files.cloud.failures` by `error_category`

Export the JSON from the UI after building a panel set; the schema is version specific, so exports are checked in rather than hand-written.

## Troubleshooting

| Symptom | Check |
|---|---|
| `Cloud telemetry export skipped: endpoint is missing, or is neither loopback nor https.` | `TelemetryEndpoint` must be `https://` for any non-localhost host |
| Nothing in SigNoz, no warnings in `debug.log` | `curl` tests above; a `401` means the token in the vault differs from `npm-ingest.conf` |
| `401` from curl with the right token | NPM Advanced config not saved, or a stray space in the `map` value |
| Certificate errors | DNS challenge failed: token scope, or the zone is not on Cloudflare; NPM shows the certbot log |
| Data appears from an off-site machine | it resolved `signoz.<domain>`: check for a public override or a split-DNS leak; the record must point at a LAN address only |
| ClickHouse disk growth | retention in **Settings -> General**; the AIO container has no separate TTL knob |
