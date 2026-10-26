#!/bin/bash
# HealthTrack Database Setup Script
# Starts SQL Server via Docker and runs EF Core migrations

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WEB_PROJECT="$SCRIPT_DIR/src/HealthTrack.Web"
INFRA_PROJECT="$SCRIPT_DIR/src/HealthTrack.Infrastructure"

echo "=== HealthTrack Database Setup ==="

# Start SQL Server via docker-compose
echo ""
echo "▶ Starting SQL Server via Docker..."
docker compose up -d sqlserver

# Wait for SQL Server to be healthy
echo ""
echo "⏳ Waiting for SQL Server to be ready..."
RETRIES=30
INTERVAL=5
for i in $(seq 1 $RETRIES); do
  STATUS=$(docker inspect --format='{{.State.Health.Status}}' healthtrack-sqlserver 2>/dev/null || echo "not_found")
  if [ "$STATUS" = "healthy" ]; then
    echo "✅ SQL Server is ready!"
    break
  elif [ "$STATUS" = "not_found" ]; then
    echo "   Container not found yet, waiting... ($i/$RETRIES)"
  else
    echo "   Status: $STATUS, waiting... ($i/$RETRIES)"
  fi

  if [ "$i" = "$RETRIES" ]; then
    echo "❌ SQL Server did not become healthy in time."
    echo "   Check Docker logs: docker logs healthtrack-sqlserver"
    exit 1
  fi
  sleep $INTERVAL
done

# Run EF Core migrations
echo ""
echo "▶ Applying EF Core migrations..."
dotnet ef database update \
  --project "$INFRA_PROJECT" \
  --startup-project "$WEB_PROJECT"

echo ""
echo "✅ Database setup complete!"
echo ""
echo "Connection: Server=localhost,1433;Database=HealthTrackDb;User Id=sa"
echo ""
echo "To run the app:  cd src/HealthTrack.Web && dotnet run"
echo "To seed users:   cd src/HealthTrack.Web && dotnet run --initialize-identity"
