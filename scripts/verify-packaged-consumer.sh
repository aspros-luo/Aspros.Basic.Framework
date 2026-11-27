#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/aspros-framework-packages.XXXXXX")"
NUGET_PACKAGES_DIR="$(mktemp -d "${TMPDIR:-/tmp}/aspros-framework-nuget.XXXXXX")"
BUILD_DIR="$(mktemp -d "${TMPDIR:-/tmp}/aspros-framework-consumer.XXXXXX")"
CONSUMER_BUILD_PROPS=(-p:BaseIntermediateOutputPath="$BUILD_DIR/obj/" -p:BaseOutputPath="$BUILD_DIR/bin/")
CONSUMER_PROJECT="$ROOT_DIR/samples/Framework.Core.Api/Framework.Core.Api.csproj"
CONSUMER_URL="${PACKAGED_CONSUMER_URL:-http://127.0.0.1:17210}"
CONSUMER_LOG="$BUILD_DIR/consumer.log"
CONSUMER_PID=""

cleanup() {
  if [[ -n "$CONSUMER_PID" ]]; then
    kill "$CONSUMER_PID" 2>/dev/null || true
    wait "$CONSUMER_PID" 2>/dev/null || true
  fi
  rm -rf "$PACKAGE_DIR" "$NUGET_PACKAGES_DIR" "$BUILD_DIR"
}
trap cleanup EXIT

command -v dotnet >/dev/null || { echo "dotnet SDK is required." >&2; exit 1; }
command -v curl >/dev/null || { echo "curl is required." >&2; exit 1; }

echo "== Pack the three release-candidate packages =="
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Abstractions/Aspros.Base.Framework.Abstractions.csproj" -c Release -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Domain/Aspros.Base.Framework.Domain.csproj" -c Release -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Infrastructure/Aspros.Base.Framework.Infrastructure.csproj" -c Release -o "$PACKAGE_DIR"

for package in \
  "Aspros.Base.Framework.Abstractions.2.0.0-rc.1.nupkg" \
  "Aspros.Base.Framework.Domain.2.0.0-rc.1.nupkg" \
  "Aspros.Base.Framework.Infrastructure.2.0.0-rc.1.nupkg"; do
  [[ -s "$PACKAGE_DIR/$package" ]] || {
    echo "Expected package was not produced: $package" >&2
    exit 1
  }
done

echo "== Restore the standalone consumer from the local package feed =="
if grep -q '<ProjectReference' "$CONSUMER_PROJECT"; then
  echo "The package-consumer project must not contain ProjectReference." >&2
  exit 1
fi
if ! grep -q 'Aspros.Base.Framework.Infrastructure" Version="2.0.0-rc.1"' "$CONSUMER_PROJECT"; then
  echo "The consumer must reference Infrastructure 2.0.0-rc.1 through PackageReference." >&2
  exit 1
fi

NUGET_PACKAGES="$NUGET_PACKAGES_DIR" dotnet restore "$CONSUMER_PROJECT" "${CONSUMER_BUILD_PROPS[@]}" \
  --source "$PACKAGE_DIR" \
  --source "https://api.nuget.org/v3/index.json"

echo "== Build the package-only consumer =="
NUGET_PACKAGES="$NUGET_PACKAGES_DIR" dotnet build "$CONSUMER_PROJECT" -c Release --no-restore "${CONSUMER_BUILD_PROPS[@]}"

echo "== Start the consumer and verify Framework DI =="
NUGET_PACKAGES="$NUGET_PACKAGES_DIR" dotnet run --project "$CONSUMER_PROJECT" \
  -c Release --no-build --no-restore "${CONSUMER_BUILD_PROPS[@]}" -- --urls "$CONSUMER_URL" >"$CONSUMER_LOG" 2>&1 &
CONSUMER_PID=$!

ready=false
for _ in {1..30}; do
  if curl --fail --silent "$CONSUMER_URL/health" >/dev/null; then
    ready=true
    break
  fi
  if ! kill -0 "$CONSUMER_PID" 2>/dev/null; then
    echo "Consumer exited before becoming healthy." >&2
    cat "$CONSUMER_LOG" >&2
    exit 1
  fi
  sleep 1
done

if [[ "$ready" != true ]]; then
  echo "Consumer did not become healthy within 30 seconds." >&2
  cat "$CONSUMER_LOG" >&2
  exit 1
fi

health_response="$(curl --fail --silent --show-error "$CONSUMER_URL/health")"
framework_response="$(curl --fail --silent --show-error "$CONSUMER_URL/framework")"
printf '%s\n' "$health_response"
printf '%s\n' "$framework_response"

grep -q '"status":"ok"' <<<"$health_response"
grep -q '"framework":"Aspros.Basic.Framework"' <<<"$health_response"
grep -q '"canResolveWorkContext":true' <<<"$framework_response"

echo "== PASS: all three RC packages restored by an isolated PackageReference consumer, built, started and resolved Framework DI =="
