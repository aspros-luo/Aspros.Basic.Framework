#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_DIR="$ROOT_DIR/dev-environment/.packages"
CORE_PROJECT="$ROOT_DIR/samples/Framework.Core.Api/Framework.Core.Api.csproj"
CORE_URL="http://127.0.0.1:7210"
CORE_LOG="$PACKAGE_DIR/core-api.log"

rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR"

echo "== Restore framework solution =="
dotnet restore "$ROOT_DIR/Aspros.Base.Framework.sln"

echo "== Pack framework NuGet packages =="
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Abstractions/Aspros.Base.Framework.Abstractions.csproj" -c Release --no-restore -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Domain/Aspros.Base.Framework.Domain.csproj" -c Release --no-restore -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Infrastructure/Aspros.Base.Framework.Infrastructure.csproj" -c Release --no-restore -o "$PACKAGE_DIR"

for package in \
  "Aspros.Base.Framework.Abstractions.2.0.0.nupkg" \
  "Aspros.Base.Framework.Domain.2.0.0.nupkg" \
  "Aspros.Base.Framework.Infrastructure.2.0.0.nupkg"; do
  if [[ ! -s "$PACKAGE_DIR/$package" ]]; then
    echo "Expected NuGet package was not produced: $package" >&2
    exit 1
  fi
done
echo "Produced packages:"
ls -lh "$PACKAGE_DIR"/*.nupkg

echo "== Restore external-style Core API using only packed framework packages =="
dotnet restore "$CORE_PROJECT" --source "$PACKAGE_DIR" --source "https://api.nuget.org/v3/index.json"

echo "== Build Core API consumer =="
dotnet build "$CORE_PROJECT" -c Release --no-restore

echo "== Start Core API consumer =="
dotnet run --project "$CORE_PROJECT" -c Release --no-build --urls "$CORE_URL" > "$CORE_LOG" 2>&1 &
CORE_PID=$!
trap 'kill "$CORE_PID" 2>/dev/null || true' EXIT

ready=false
for _ in {1..30}; do
  if curl --fail --silent "$CORE_URL/health" >/dev/null; then
    ready=true
    break
  fi
  if ! kill -0 "$CORE_PID" 2>/dev/null; then
    echo "Core API exited before becoming healthy." >&2
    cat "$CORE_LOG" >&2
    exit 1
  fi
  sleep 1
done

if [[ "$ready" != true ]]; then
  echo "Core API did not become healthy within 30 seconds." >&2
  cat "$CORE_LOG" >&2
  exit 1
fi

echo "== Smoke GET /health =="
health_response="$(curl --fail --silent --show-error "$CORE_URL/health")"
echo "$health_response"
grep -q '"status":"ok"' <<<"$health_response"
grep -q '"framework":"Aspros.Basic.Framework"' <<<"$health_response"

echo "== Smoke GET /framework (resolves a framework service from DI) =="
framework_response="$(curl --fail --silent --show-error "$CORE_URL/framework")"
echo "$framework_response"
grep -q '"canResolveWorkContext":true' <<<"$framework_response"

echo "== PASS: packed NuGet packages restored by the API consumer, API built, and framework DI smoke test succeeded =="
