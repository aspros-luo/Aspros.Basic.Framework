#!/usr/bin/env bash
set -euo pipefail

: "${ConnectionStrings__MigrationDatabase:?ConnectionStrings__MigrationDatabase is required}"

FIXTURE="tests/Aspros.Basic.Framework.MigrationFixture"
MIGRATIONS="$FIXTURE/Migrations"
OUTPUT_DIR="$FIXTURE/.regression"

rm -rf "$MIGRATIONS" "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

echo "== Reset migration fixture database =="
dotnet run --project "$FIXTURE" -- --reset

echo "== Migration regression: add InitialCreate =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration add InitialCreate \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

echo "== Migration regression: generate InitialCreate SQL =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration script \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext \
  --idempotent \
  --output "$OUTPUT_DIR/initial.sql"

test -s "$OUTPUT_DIR/initial.sql"
grep -q "MigrationCustomers" "$OUTPUT_DIR/initial.sql"

echo "== Migration regression: apply InitialCreate =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration update \
  --allow-update \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

echo "== Seed pre-upgrade data =="
dotnet run --project "$FIXTURE" -- --seed-v1

echo "== Switch fixture model to V2 =="
cp "$FIXTURE/MigrationCustomer.v2.template" "$FIXTURE/MigrationCustomer.cs"
sed -i 's/Property(x => x.Name)/Property(x => x.DisplayName)/' \
  "$FIXTURE/MigrationDbContext.cs"

echo "== Migration regression: add RenameName =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration add RenameName \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

RENAME_FILE="$(find "$MIGRATIONS" -maxdepth 1 -name '*_RenameName.cs' -print -quit)"
test -n "$RENAME_FILE"

if grep -q "DropColumn" "$RENAME_FILE"; then
  echo "== Replace destructive scaffold with reviewed RenameColumn migration =="
  MIGRATION_ID="$(basename "$RENAME_FILE" .cs)"
  cat > "$RENAME_FILE" <<EOF
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aspros.Basic.Framework.MigrationFixture.Migrations;

[Migration("$MIGRATION_ID")]
public partial class RenameName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Name",
            table: "MigrationCustomers",
            newName: "DisplayName");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "DisplayName",
            table: "MigrationCustomers",
            newName: "Name");
    }
}
EOF
else
  echo "== Provider emitted RenameColumn directly; retain generated migration =="
  grep -q "RenameColumn" "$RENAME_FILE"
fi

echo "== Migration regression: generate upgrade SQL =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration script \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext \
  --idempotent \
  --output "$OUTPUT_DIR/upgrade.sql"

test -s "$OUTPUT_DIR/upgrade.sql"
grep -q "DisplayName" "$OUTPUT_DIR/upgrade.sql"
! grep -qi "DROP COLUMN.*Name" "$OUTPUT_DIR/upgrade.sql"

echo "== Apply reviewed rename migration =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration update \
  --allow-update \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

echo "== Verify data survives rename =="
dotnet run --project "$FIXTURE" -- --verify-v2

echo "== Repeat update must be a no-op =="
dotnet run --project Aspros.Basic.Framework.Tools -- db migration update \
  --allow-update \
  --project "$FIXTURE" \
  --startup-project "$FIXTURE" \
  --context MigrationDbContext

echo "Migration add/script/update/rename/data-preservation regression passed."
