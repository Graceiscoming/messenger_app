#!/usr/bin/env bash
# Starts only PostgreSQL 16 for local development (dotnet run + npm run dev).
# Usage (from repo root):  bash scripts/dev-db.sh
# Password: DB_PASSWORD from .env if present, otherwise the dev default "messenger_dev".
# Exposed on 127.0.0.1:5432 only. Data lives in the named volume messenger-pgdata-dev.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
CONTAINER_NAME="messenger-db-dev"
VOLUME_NAME="messenger-pgdata-dev"
IMAGE="postgres:16"
DB_NAME="messenger"
DB_USER="messenger"
PASSWORD="messenger_dev"

if [[ -f "$ENV_FILE" ]]; then
    value="$(grep -E '^[[:space:]]*DB_PASSWORD[[:space:]]*=' "$ENV_FILE" | tail -n 1 | cut -d= -f2- | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^["'\'']//' -e 's/["'\'']$//' || true)"
    if [[ -n "$value" ]]; then
        PASSWORD="$value"
    fi
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "docker CLI not found. Install Docker Desktop and make sure it is running." >&2
    exit 1
fi

if [[ -n "$(docker ps -a --filter "name=^/${CONTAINER_NAME}$" --format '{{.Names}}')" ]]; then
    echo "Starting existing container $CONTAINER_NAME ..."
    docker start "$CONTAINER_NAME" >/dev/null
else
    echo "Creating container $CONTAINER_NAME ($IMAGE) ..."
    docker run -d \
        --name "$CONTAINER_NAME" \
        -e "POSTGRES_DB=$DB_NAME" \
        -e "POSTGRES_USER=$DB_USER" \
        -e "POSTGRES_PASSWORD=$PASSWORD" \
        -p 127.0.0.1:5432:5432 \
        -v "${VOLUME_NAME}:/var/lib/postgresql/data" \
        "$IMAGE" >/dev/null
fi

echo "Waiting for PostgreSQL to accept connections ..."
ready=0
for _ in $(seq 1 60); do
    if docker exec "$CONTAINER_NAME" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
        ready=1
        break
    fi
    sleep 1
done

if [[ "$ready" -ne 1 ]]; then
    echo "PostgreSQL did not become ready. Check: docker logs $CONTAINER_NAME" >&2
    exit 1
fi

echo
echo "PostgreSQL ready: localhost:5432  db=$DB_NAME user=$DB_USER"
echo "Connection string:"
echo "  Host=localhost;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=<DB_PASSWORD>"
echo "Stop with: docker stop $CONTAINER_NAME"
