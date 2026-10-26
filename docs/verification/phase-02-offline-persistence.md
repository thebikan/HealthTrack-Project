# .NET 8 installation and offline persistence verification

Date: 2026-10-21 (Asia/Colombo). Commit: none.

Status: foundation COMPLETE; step 02 PARTIAL, live database verification BLOCKED.
Lecturer approval remains pending. No database connection, provisioning,
migration application, commit, or push occurred.

## Installation evidence

Before installation: macOS ARM64, `sw_vers` 27.0.1; SDK 10.0.203,
Microsoft.NETCore.App 10.0.7 and Microsoft.AspNetCore.App 10.0.7.

Microsoft's [official .NET 8 release manifest](https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json)
reported latest SDK **8.0.426**, release **8.0.32**, dated **2026-10-13**.
Downloaded the official `dotnet-sdk-8.0.426-osx-arm64.tar.gz` archive and matched
its SHA-512 against that manifest before extraction:

```text
17cd408c92eb6ceedc06d41fa726c783b9be020c6ae5f3624ad3a0b2c26a78aa7d09f38338003163a72bb14d437731debf533c0451a5c6a05de3b1e37a3aa50a
```

The shared root directory requires administrator credentials (`sudo -n` reported
that a password is required). Used Microsoft's manual archive installation
method in `~/.local/share/dotnet8` instead. No administrator password was
requested, no .NET 10 files replaced, and no shell profile changed.

Verified `dotnet --info`, `dotnet --list-sdks`, and `dotnet --list-runtimes` using
the new installation:

| Component | Verified value |
|---|---|
| SDK | 8.0.426, ARM64, MSBuild 17.11.69 |
| Microsoft.NETCore.App | 8.0.32 |
| Microsoft.AspNetCore.App | 8.0.32 |
| Installation root | `/Users/theby/.local/share/dotnet8` |
| Existing system SDK, checked afterward | 10.0.203 |
| Existing system runtimes, checked afterward | Both 10.0.7 |

The official [macOS support table](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
currently lists versions through macOS 26, while the local machine reports
27.0.1. Local compilation/test execution succeeds; official support for this
OS version and compatibility with the eventual Windows assessment installation
are not claimed. .NET 8's published end of support remains 2026-11-10.

## Implemented offline scope

- 15 core domain entity types and seven Identity tables, with explicit SQL
  Server mappings (22 tables total).
- Profile/user uniqueness, typed client/trainer profile foreign keys, and a
  composite workout-log/plan/client FK.
- Restricted deletion on every FK, bounded strings, decimal precision,
  SQL checks, history indexes, filtered uniqueness for active assignments and
  notification delivery keys, and rowversion for concurrent updates.
- UTC timestamp validation before both sync/async SaveChanges, plus UTC kind
  restoration when reading SQL datetime2 values.
- Identity EF stores, password/lockout options, secure cookie configuration,
  token providers, MVC antiforgery registration, and authentication middleware.
- Environment/user-secrets configuration with no connection fallback.
  Sensitive EF logging is disabled; no health values are logged by this code.
- Initial migration `20261020183553_InitialCoreSchema`, model snapshot, and
  offline idempotent SQL generation. Its timestamp is UTC; verification dates
  here use Asia/Colombo.

The first persistence compilation failed because the Infrastructure project
needed an explicit Microsoft.AspNetCore.App framework reference for its Identity
registration. Added the reference without changing project dependency direction.
Subsequent compilation and tests passed.

## Actual validation results

| Validation | Result |
|---|---|
| Foundation restore/build using installed .NET 8 | PASS, zero warnings/errors |
| Original foundation tests | PASS: Domain 1, Application 1, Integration 1 |
| EF/Identity package restore and local EF tool restore | PASS; EF Core/Identity/tool version 8.0.32 |
| Final solution build | PASS, seven projects, zero warnings/errors |
| HealthTrack.Domain.Tests | 1 passed, 0 failed, 0 skipped |
| HealthTrack.Application.Tests | 1 passed, 0 failed, 0 skipped |
| HealthTrack.IntegrationTests | 19 passed, 0 failed, 0 skipped |
| Final total | **21 passed, 0 failed, 0 skipped** |
| EF `has-pending-model-changes` | PASS: no changes since last migration |
| Idempotent SQL generation | PASS; `artifacts/initial-core-schema.sql` |
| Actual SQL Server connection/migration execution | NOT RUN; no approved server |

Tests verify architecture, host startup, SQL Server model metadata/DDL,
cross-client relationship keys, restrictive deletion, UTC validation,
rowversion mappings, filtered unique indexes, missing configuration, Identity
options/password hashing, snapshot drift, and migration operations. The
IntegrationTests project currently tests local framework integration, not
live SQL Server behavior. No fake provider substitutes for SQL Server.

TRX files:

- `artifacts/test-results/foundation-net8/` — original three passing tests.
- `artifacts/test-results/persistence-final/` — final 21 passing tests.

These artifacts are ignored by Git. Earlier aborted runs remain as historical
evidence and should not be mistaken for the final results.

## Commands executed

Commands ran after setting `DOTNET_ROOT` to the user installation and prepending
it to PATH. Process-local `DOTNET_CLI_HOME=/tmp/healthtrack-dotnet-cli`,
`DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`, and
`NUGET_PACKAGES="$PWD/artifacts/packages"` isolated the tooling cache.

```sh
dotnet restore HealthTrack.sln --source https://api.nuget.org/v3/index.json --disable-parallel
dotnet tool restore
dotnet build HealthTrack.sln --no-restore --disable-build-servers -m:1
dotnet test HealthTrack.sln --no-build --no-restore --disable-build-servers -m:1 --logger trx --results-directory artifacts/test-results/persistence-final
dotnet ef migrations add InitialCoreSchema --project src/HealthTrack.Infrastructure --output-dir Persistence/Migrations --no-build
dotnet ef migrations has-pending-model-changes --project src/HealthTrack.Infrastructure --no-build
dotnet ef migrations script --idempotent --project src/HealthTrack.Infrastructure --no-build --output artifacts/initial-core-schema.sql
```

Migration creation preceded the final build/tests so the generated migration
and snapshot were included in validation.

## Remaining limits

SQL Server environment, connection credentials, and permission to apply
migrations are still pending. No roles/Admin account were seeded. No login,
registration, ownership/assignment services, business endpoints, dashboards,
reminder worker, booking locks, or advanced features are complete. Entity
definitions do not implement those use cases. Rowversion is not an overlap
constraint. The initial Down migration drops tables and must not be executed
against populated data without an explicit reviewed recovery plan.

See [database setup](../database-setup.md) for remote/Windows server options,
safe configuration input, and outstanding live-server checks.

## Exact files changed in this task

Updated:

- `README.md`
- `docs/trd.md`
- `docs/schema.md`
- `docs/implementation-plan.md`
- `docs/decisions/0002-provisional-backend-authorization.md`
- `docs/verification/phase-01-foundation.md`
- `src/HealthTrack.Infrastructure/HealthTrack.Infrastructure.csproj`
- `src/HealthTrack.Web/Program.cs`

Added:

- `global.json`
- `.config/dotnet-tools.json`
- `docs/database-setup.md`
- `docs/decisions/0003-offline-persistence-foundation.md`
- `docs/verification/phase-02-offline-persistence.md`
- `src/HealthTrack.Domain/Profiles/Profiles.cs`
- `src/HealthTrack.Domain/Workouts/Workouts.cs`
- `src/HealthTrack.Domain/Nutrition/Nutrition.cs`
- `src/HealthTrack.Domain/Health/HealthRecords.cs`
- `src/HealthTrack.Domain/Appointments/Appointments.cs`
- `src/HealthTrack.Domain/Notifications/Notifications.cs`
- `src/HealthTrack.Infrastructure/Identity/ApplicationUser.cs`
- `src/HealthTrack.Infrastructure/DependencyInjection.cs`
- `src/HealthTrack.Infrastructure/Persistence/HealthTrackDbContext.cs`
- `src/HealthTrack.Infrastructure/Persistence/CoreModelConfiguration.cs`
- `src/HealthTrack.Infrastructure/Persistence/HealthTrackDbContextFactory.cs`
- `src/HealthTrack.Infrastructure/Persistence/Migrations/20261020183553_InitialCoreSchema.cs`
- `src/HealthTrack.Infrastructure/Persistence/Migrations/20261020183553_InitialCoreSchema.Designer.cs`
- `src/HealthTrack.Infrastructure/Persistence/Migrations/HealthTrackDbContextModelSnapshot.cs`
- `tests/HealthTrack.IntegrationTests/PersistenceModelTests.cs`
- `tests/HealthTrack.IntegrationTests/InfrastructureRegistrationTests.cs`
- `tests/HealthTrack.IntegrationTests/MigrationTests.cs`

Outside the repository: installed official SDK files under
`~/.local/share/dotnet8`; downloaded archive/manifest under `/tmp`; .NET tooling
cache under `/tmp/healthtrack-dotnet-cli`. No application database secrets were set.
