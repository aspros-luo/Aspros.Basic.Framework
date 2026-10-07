#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_DIR="$ROOT_DIR/dev-environment/.packages"
CORE_PROJECT="$ROOT_DIR/samples/Framework.Core.Api/Framework.Core.Api.csproj"
CORE_URL="http://127.0.0.1:7210"

rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR"

echo "== Restore framework =="
dotnet restore "$ROOT_DIR/Aspros.Base.Framework.sln"

echo "== Pack framework =="
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Abstractions/Aspros.Base.Framework.Abstractions.csproj" -c Release --no-restore -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Domain/Aspros.Base.Framework.Domain.csproj" -c Release --no-restore -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Infrastructure/Aspros.Base.Framework.Infrastructure.csproj" -c Release --no-restore -o "$PACKAGE_DIR"

echo "== Restore Core API from local framework packages =="
dotnet restore "$CORE_PROJECT" --source "$PACKAGE_DIR" --source "https://api.nuget.org/v3/index.json"

echo "== Build Core API =="
dotnet build "$CORE_PROJECT" -c Release --no-restore

echo "== Start Core API =="
dotnet run --project "$CORE_PROJECT" -c Release --no-build --urls "$CORE_URL" > "$PACKAGE_DIR/core-api.log" 2>&1 &
CORE_PID=$!
trap 'kill "$CORE_PID" 2>/dev/null || true' EXIT

for _ in {1..30}; do
  if (echo > /dev/tcp/127.0.0.1/7210) 2>/dev/null; then
    break
  fi
  sleep 1
done

echo "== Smoke GET /health =="
response="$(exec 3<>/dev/tcp/127.0.0.1/7210; printf 'GET /health HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n' >&3; cat <&3; exec 3>&-)"
printf '%s\n' "$response"

if ! grep -q "HTTP/1.1 200" <<<"$response"; then
  echo "Core API health check failed."
  cat "$PACKAGE_DIR/core-api.log"
  exit 1
fi

echo "== Package consumer regression passed =="