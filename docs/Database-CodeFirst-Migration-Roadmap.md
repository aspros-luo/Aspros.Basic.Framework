# Database Code First & Migration Roadmap

## Goal

Standardize the Framework around:

DDD Entity/Aggregate -> explicit EF mapping -> DbContext model -> EF Core Migration -> SQL -> Database

This is intentionally not a reflection-based Entity-to-SQL generator.

## Current status

- Mapping discovery: complete.
- Generic DbContext registration: complete.
- Code First architecture: designed.
- Migration CLI/tooling: not implemented yet.
- Automated migration integration tests: not implemented yet.
- Production SQL/bundle workflow: designed, not implemented.

## Phase 1 — Tooling foundation

Target: Aspros.Basic.Framework.Tools

Responsibilities:
- locate the application project;
- locate the DbContext;
- invoke EF Core design-time services;
- add a named migration;
- generate/update migration scripts;
- provide clear errors when multiple DbContexts or startup projects exist.

The tool must not silently modify or apply a production database.

Candidate commands:

    aspros db migration add InitialCreate
    aspros db migration script
    aspros db migration update

Update is intended primarily for development/test environments. Production deployment should prefer a reviewed SQL script or migration bundle.

## Phase 2 — Mapping test matrix

The test model must cover:
1. Single entity/table.
2. One-to-many.
3. Many-to-many with an implicit join table.
4. Many-to-many with an explicit join entity and payload.
5. Value object/owned type.
6. Composite key.
7. Foreign key and delete behavior.
8. Unique and non-unique indexes.
9. Backing fields/shadow properties.
10. Nullable/reference type behavior.
11. Column rename.
12. Column type/length/precision changes.

## Phase 3 — MySQL/Pomelo validation

The first provider target is the same MySQL/Pomelo stack already used by real Framework consumers.

Validation must include:
- migration generation;
- script generation;
- clean database creation;
- migration application;
- repeated migration invocation;
- upgrade from previous schema;
- rollback/compensation strategy where applicable;
- charset/collation and common MySQL column types.

## Phase 4 — Data-loss safety

The tooling must make destructive changes visible.

Examples:
- column rename;
- drop column;
- table rename;
- changing nullable to non-nullable;
- narrowing string/decimal types.

The tool should generate migration files/scripts, but it must not claim that EF can always infer business-safe data movement. Developers must review generated migrations and add explicit data migration code when required.

## Phase 5 — Production workflow

Recommended workflow:

    Developer changes Entity/Mapping
            |
            v
    migration add
            |
            v
    review migration
            |
            v
    generate SQL / bundle
            |
            v
    deployment pipeline / DBA review
            |
            v
    production database

Do not make Database.Migrate() a default Framework startup behavior for multi-instance microservices.

## Acceptance criteria

The database feature is complete only when:
- a new service can create a DbContext using Framework conventions;
- a migration can be generated from the service model;
- 1:N and N:N schemas are correct;
- value objects map correctly;
- indexes and constraints survive generation;
- MySQL/Pomelo scripts execute successfully;
- an upgrade migration preserves existing data in the tested rename scenario;
- destructive changes are visible for review;
- production SQL can be generated without starting the application;
- bilingual usage documentation exists;
- the workflow does not require GitHub Actions to be enabled.

## Current progress

| Phase | Status |
|---|---|
| Architecture/repository investigation | Done |
| EF mapping extraction | Done |
| DbContext registration | Done |
| Tool design | In progress |
| CLI implementation | Pending |
| Migration tests | Pending |
| MySQL/Pomelo tests | Pending |
| Data-loss tests | Pending |
| Production script/bundle workflow | Pending |
| Documentation | In progress |
