#!/usr/bin/env bash
set -euo pipefail

: "${ConnectionStrings__TestDatabase:?ConnectionStrings__TestDatabase is required}"
: "${TestRabbitMq__Host:?TestRabbitMq__Host is required}"
: "${TestNacos__Address:?TestNacos__Address is required}"
: "${ConnectionStrings__MigrationDatabase:?ConnectionStrings__MigrationDatabase is required}"

wait_for() {
  local host="$1"
  local port="$2"

  until (echo >"/dev/tcp/$host/$port") 2>/dev/null; do
    sleep 1
  done
}

wait_for mysql 3306
wait_for redis 6379
wait_for rabbitmq 5672
wait_for nacos 8848

echo "== Restore =="
dotnet restore Aspros.Base.Framework.sln

echo "== EF CLI =="
dotnet ef --version

echo "== Solution build =="
dotnet build Aspros.Base.Framework.sln --no-restore

echo "== Full xUnit regression =="
dotnet test   tests/Aspros.Basic.Framework.IntegrationTests/Aspros.Basic.Framework.IntegrationTests.csproj   --no-restore   --no-build

echo "== MySQL migration regression =="
bash dev-environment/run-migration-regression.sh

echo "== Full Framework regression passed =="
