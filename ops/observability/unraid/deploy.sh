#!/bin/bash
# Deploys signoz-aio and the Caddy ingest proxy on an Unraid host. Idempotent: re-run after
# editing env or Caddyfile. Run as root on the Unraid box from this directory.
#
#   ./deploy.sh            create/refresh both containers
#   ./deploy.sh status     show containers, cert state, and last proxy log lines
set -euo pipefail

APPDATA=/mnt/user/appdata
SIGNOZ_DIR="$APPDATA/signoz-aio"
INGRESS_DIR="$APPDATA/otel-ingress"
NET=otel-net
SIGNOZ_IMAGE=jsonbored/signoz-aio:latest
CADDY_IMAGE=ghcr.io/caddybuilds/caddy-cloudflare:latest
TEMPLATES=/boot/config/plugins/dockerMan/templates-user
HERE="$(cd "$(dirname "$0")" && pwd)"

status() {
	docker ps -a --filter name=signoz-aio --filter name=otel-ingress --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
	echo "--- certificates"
	docker exec otel-ingress sh -c 'ls /data/caddy/certificates/*/ 2>/dev/null || echo "(none yet)"'
	echo "--- otel-ingress log tail"
	docker logs --tail 15 otel-ingress 2>&1
}

if [[ "${1:-}" == "status" ]]; then status; exit 0; fi

mkdir -p "$SIGNOZ_DIR" "$INGRESS_DIR/data" "$INGRESS_DIR/config"

if [[ ! -f "$INGRESS_DIR/.env" ]]; then
	cat > "$INGRESS_DIR/.env" <<EOF
# Read by the otel-ingress container only. Never copy this file into the repository.
INGEST_HOST=otel.pesengineers.dev
ACME_EMAIL=CHANGE_ME@pesengineers.dev
# Cloudflare API token scoped to Zone:DNS:Edit on the pesengineers.dev zone only.
CF_API_TOKEN=CHANGE_ME
# Bearer token pilots store in the Windows credential vault (Files.CloudGuard.Telemetry).
INGEST_TOKEN=$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')
EOF
	chmod 600 "$INGRESS_DIR/.env"
	echo "Created $INGRESS_DIR/.env with a fresh INGEST_TOKEN; fill in CF_API_TOKEN and ACME_EMAIL."
fi

cp "$HERE/Caddyfile" "$INGRESS_DIR/Caddyfile"
cp "$HERE/signoz-aio.xml" "$HERE/otel-ingress.xml" "$TEMPLATES/" 2>/dev/null || true

docker network inspect "$NET" >/dev/null 2>&1 || docker network create "$NET"

docker pull "$SIGNOZ_IMAGE"
docker pull "$CADDY_IMAGE"

docker rm -f signoz-aio otel-ingress >/dev/null 2>&1 || true

# UI on 8080 for the LAN; OTLP ports stay on the docker network so only the proxy reaches them.
docker run -d --name signoz-aio \
	--network "$NET" \
	--restart unless-stopped \
	-p 8080:8080 \
	-v "$SIGNOZ_DIR:/appdata" \
	-v "$SIGNOZ_DIR/clickhouse:/var/lib/clickhouse" \
	-e TZ=America/New_York \
	-e SIGNOZ_GLOBAL_INGESTION__URL="https://$(grep ^INGEST_HOST= "$INGRESS_DIR/.env" | cut -d= -f2):8443" \
	-e SIGNOZ_ANALYTICS_ENABLED=false \
	-e SIGNOZ_STATSREPORTER_ENABLED=false \
	--label net.unraid.docker.managed=dockerman \
	--label net.unraid.docker.webui='http://[IP]:[PORT:8080]' \
	--label net.unraid.docker.icon='https://raw.githubusercontent.com/JSONbored/awesome-unraid/main/icons/signoz.png' \
	"$SIGNOZ_IMAGE"

# The image ships the histogramQuantile UDF binary and an XML descriptor, but names the descriptor
# 'custom-function.xml' while ClickHouse only loads files matching '*_function.*ml', and wraps it in
# a <clickhouse> root the executable-function loader rejects. Without this, every percentile query
# fails with "Function with name `histogramQuantile` does not exist".
install_histogram_udf() {
	for _ in $(seq 1 30); do
		if docker exec signoz-aio clickhouse-client -q 'SELECT 1' >/dev/null 2>&1; then
			docker exec signoz-aio sh -c "sed -e '/<clickhouse>/d' -e '/<\/clickhouse>/d' \
				/opt/signoz-aio/config/clickhouse/custom-function.xml > /etc/clickhouse-server/signoz_function.xml"
			sleep 8
			docker exec signoz-aio clickhouse-client -q \
				"SELECT if(count() = 1, 'histogramQuantile registered', 'histogramQuantile MISSING') FROM system.functions WHERE name = 'histogramQuantile'"
			return
		fi
		sleep 10
	done
	echo "ClickHouse never became reachable; histogramQuantile UDF not installed."
}
install_histogram_udf

if grep -q '^CF_API_TOKEN=CHANGE_ME' "$INGRESS_DIR/.env"; then
	echo "CF_API_TOKEN is still CHANGE_ME in $INGRESS_DIR/.env; otel-ingress not started. Set it and re-run."
	sleep 5
	status
	exit 0
fi

docker run -d --name otel-ingress \
	--network "$NET" \
	--restart unless-stopped \
	-p 8443:8443 \
	--env-file "$INGRESS_DIR/.env" \
	-v "$INGRESS_DIR/Caddyfile:/etc/caddy/Caddyfile:ro" \
	-v "$INGRESS_DIR/data:/data" \
	-v "$INGRESS_DIR/config:/config" \
	--label net.unraid.docker.managed=dockerman \
	--label net.unraid.docker.icon='https://raw.githubusercontent.com/caddyserver/website/master/src/resources/images/caddy-circle-lock.svg' \
	"$CADDY_IMAGE"

sleep 5
status
