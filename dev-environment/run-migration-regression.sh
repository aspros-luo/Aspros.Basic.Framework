#!/usr/bin/env bash
set -euo pipefail

CONNECTION="${ConnectionStrings__MigrationDatabase:?ConnectionStrings__MigrationDatabase is required}"
FIXTURE="tests/Aspros.Basic.Framework.MigrationFixture"
MIGRATIONS="$FIXTURE/Migrations"
OUTPUT_DIR="$FIXTURE/.regression"

rm -rf "$MIGRATIONS" "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

echo "== Migration regression: add =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration add InitialCreate \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

echo "== Migration regression: script =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration script \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext \
  --idempotent \
  --output "$OUTPUT_DIR/initial.sql"

test -s "$OUTPUT_DIR/initial.sql"
grep -q "MigrationCustomers" "$OUTPUT_DIR/initial.sql"

echo "== Migration regression: update =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration update \
  --allow-update \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

echo "== Migration regression: repeat update =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration update \
  --allow-update \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

echo "Migration add/script/update regression passed."
