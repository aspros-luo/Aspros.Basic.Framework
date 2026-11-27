#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/aspros-framework-packages.XXXXXX")"
NUGET_PACKAGES_DIR="$(mktemp -d "${TMPDIR:-/tmp}/aspros-framework-nuget.XXXXXX")"
BUILD_DIR="$(mktemp -d "${TMPDIR:-/tmp}/aspros-framework-consumer.XXXXXX")"
CONSUMER_BUILD_PROPS=(-p:BaseIntermediateOutputPath="$BUILD_DIR/obj/" -p:BaseOutputPath="$BUILD_DIR/bin/")
CONSUMER_PROJECT="$ROOT_DIR/samples/Framework.Core.Api/Framework.Core.Api.csproj"
CONSUMER_DLL="$BUILD_DIR/bin/Release/net10.0/Framework.Core.Api.dll"
CONSUMER_URL="${PACKAGED_CONSUMER_URL:-http://127.0.0.1:17210}"
CONSUMER_LOG="$BUILD_DIR/consumer.log"
TOOL_PATH="$BUILD_DIR/tools"
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

echo "== Pack all six release-candidate NuGet artifacts =="
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Abstractions/Aspros.Base.Framework.Abstractions.csproj" -c Release -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Domain/Aspros.Base.Framework.Domain.csproj" -c Release -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Infrastructure/Aspros.Base.Framework.Infrastructure.csproj" -c Release -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.Algorithms/Aspros.Base.Framework.Algorithms.csproj" -c Release -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Base.Framework.ClickHouse/Aspros.Base.Framework.ClickHouse.csproj" -c Release -o "$PACKAGE_DIR"
dotnet pack "$ROOT_DIR/Aspros.Basic.Framework.Tools/Aspros.Basic.Framework.Tools.csproj" -c Release -o "$PACKAGE_DIR"

for package in \
  "Aspros.Base.Framework.Abstractions.2.0.0-rc.1.nupkg" \
  "Aspros.Base.Framework.Domain.2.0.0-rc.1.nupkg" \
  "Aspros.Base.Framework.Infrastructure.2.0.0-rc.1.nupkg" \
  "Aspros.Base.Framework.Algorithms.2.0.0-rc.1.nupkg" \
  "Aspros.Base.Framework.ClickHouse.2.0.0-rc.1.nupkg" \
  "Aspros.Basic.Framework.Tools.2.0.0-rc.1.nupkg"; do
  [[ -s "$PACKAGE_DIR/$package" ]] || {
    echo "Expected package was not produced: $package" >&2
    exit 1
  }
done

echo "== Verify the consumer references all five Framework libraries through NuGet =="
if grep -q '<ProjectReference' "$CONSUMER_PROJECT"; then
  echo "The package-consumer project must not contain ProjectReference." >&2
  exit 1
fi
for package in \
  Aspros.Base.Framework.Abstractions \
  Aspros.Base.Framework.Domain \
  Aspros.Base.Framework.Infrastructure \
  Aspros.Base.Framework.Algorithms \
  Aspros.Base.Framework.ClickHouse; do
  if ! grep -q "<PackageReference Include=\"$package\" Version=\"2.0.0-rc.1\"" "$CONSUMER_PROJECT"; then
    echo "Missing PackageReference for $package 2.0.0-rc.1." >&2
    exit 1
  fi
done

echo "== Restore and build the standalone library consumer =="
NUGET_PACKAGES="$NUGET_PACKAGES_DIR" dotnet restore "$CONSUMER_PROJECT" "${CONSUMER_BUILD_PROPS[@]}" \
  --source "$PACKAGE_DIR" \
  --source "https://api.nuget.org/v3/index.json"
NUGET_PACKAGES="$NUGET_PACKAGES_DIR" dotnet build "$CONSUMER_PROJECT" -c Release --no-restore "${CONSUMER_BUILD_PROPS[@]}"
[[ -s "$CONSUMER_DLL" ]] || { echo "Consumer assembly was not produced: $CONSUMER_DLL" >&2; exit 1; }

if curl --fail --silent "$CONSUMER_URL/health" >/dev/null; then
  echo "The verification URL is already serving an application: $CONSUMER_URL" >&2
  echo "Choose another port with PACKAGED_CONSUMER_URL." >&2
  exit 1
fi

echo "== Start the package-only consumer and verify Framework APIs =="
dotnet "$CONSUMER_DLL" --urls "$CONSUMER_URL" >"$CONSUMER_LOG" 2>&1 &
CONSUMER_PID=$!

ready=false
for _ in {1..30}; do
  if ! kill -0 "$CONSUMER_PID" 2>/dev/null; then
    echo "Consumer exited before becoming healthy." >&2
    cat "$CONSUMER_LOG" >&2
    exit 1
  fi
  if curl --fail --silent "$CONSUMER_URL/health" >/dev/null; then
    ready=true
    break
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
grep -q '"algorithmRank":1' <<<"$framework_response"
grep -q '"clickHouseAssembly":"Aspros.Base.Framework.ClickHouse"' <<<"$framework_response"

echo "== Install and smoke-test the Aspros CLI tool package =="
NUGET_PACKAGES="$NUGET_PACKAGES_DIR" dotnet tool install Aspros.Basic.Framework.Tools \
  --version 2.0.0-rc.1 \
  --prerelease \
  --tool-path "$TOOL_PATH" \
  --add-source "$PACKAGE_DIR" \
  --add-source "https://api.nuget.org/v3/index.json"

if ! tool_output="$("$TOOL_PATH/aspros" db migration --help 2>&1)"; then
  printf '%s\n' "$tool_output" >&2
  echo "The installed CLI did not handle 'aspros db migration --help' successfully." >&2
  exit 1
fi
printf '%s\n' "$tool_output"
grep -q "Aspros Basic Framework database migration tool" <<<"$tool_output"

echo "== PASS: all six RC packages packed; five libraries restored and exercised by a standalone consumer; CLI tool installed and launched =="
