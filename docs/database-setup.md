# SQL Server configuration — not connected

Phase 04 now has [environment requirements, guarded live tests and approval/rollback
checklists](verification/phase-04-sql-preparation.md). Live tests are opt-in and
were skipped; no approved connection is configured in the checked sources.

No server has been installed, provisioned, or contacted. No migration has been
applied. SQL Server remains the only database provider. The model tests are
offline tests, not proof that the schema works on an actual server.

Latest handoff: [final SQL checklist](verification/final-sql-server-integration-checklist.md).
The current model has 28 mapped tables and seven unapplied migrations; use the
latest `artifacts/final-backend-schema.sql`, not earlier phase artifacts. The live
suite now includes 13 gated tests, all still pending, including a timing observation.

## Supported connection options

1. An existing university/team-managed SQL Server on a supported Windows or
   x86-64 Linux host, reachable from the Mac over an approved private network/VPN.
2. SQL Server on a supported Windows development/assessment machine. Ask its
   administrator to enable TCP/IP and supply a fixed port, DNS name, certificate,
   authentication method, and dedicated database.
3. A supported remote Windows/Linux VM managed by the user or institution.
   Provisioning a new VM or database requires separate approval.

Do not use a SQL Server container emulated on macOS ARM64. Windows LocalDB may
be suitable for local Windows assessment if approved; it is not the proposed
remote server for this Mac. See Microsoft's
[SQL Server container support](https://learn.microsoft.com/en-us/sql/linux/containers/deploy)
and [Windows TCP configuration](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-a-server-to-listen-on-a-specific-tcp-port).

Before connecting, confirm server version/edition, database name, host/port,
TLS certificate hostname/trust, VPN/firewall access, and authentication.
Windows integrated authentication depends on the client's configured identity;
do not assume it works automatically from a Mac. Use an administrator-approved
authentication method and a least-privilege development account. Schema changes
should use a separate authorized deployment identity. Prefer `Encrypt=True`,
`TrustServerCertificate=False`, and `Persist Security Info=False`.
See [SqlClient connection configuration](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax).

## Runtime configuration

The Web project already has `UserSecretsId` set to `HealthTrack.Web-development`.
`WebApplication.CreateBuilder` reads development user-secrets when the
environment is Development, and environment variables override those settings.

Configuration key: `ConnectionStrings:DefaultConnection`.
Environment variable: `ConnectionStrings__DefaultConnection`.
No default connection string exists in the repository. Resolving the DbContext
without this setting fails with a sanitized configuration message. The empty MVC
host can start without it, but this is not a database readiness signal.

For development user-secrets, this optional command prompts without placing the
value in source or shell history (do not run until you have approved details):

```sh
python3 -c 'import getpass,json; print(json.dumps({"ConnectionStrings:DefaultConnection":getpass.getpass("SQL Server connection string: ")}))' |
  dotnet user-secrets set --project src/HealthTrack.Web
```

Alternatively, set `ConnectionStrings__DefaultConnection` through your secure
process environment or IDE environment configuration outside source control.
Do not paste actual values into appsettings files, committed launch settings,
logs, or chat. No real or placeholder user-secret was written by this task.

User-secrets are development convenience storage, not an encrypted vault.
See [Microsoft's user-secrets documentation](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-8.0).

## Offline migration workflow

Activate the .NET 8 SDK as described in README, then:

```sh
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/HealthTrack.Infrastructure
dotnet ef migrations script --idempotent --project src/HealthTrack.Infrastructure --output artifacts/final-backend-schema.sql
```

The design-time factory configures SQL Server without a connection string.
It supports generating/reviewing model changes and SQL without any live server.
Neither the factory nor Web startup calls `Migrate`, `EnsureCreated`, or
`EnsureDeleted`. Do not run `database update` or execute generated SQL until the
server/environment is confirmed and migration execution is approved.

Review initial migration tables, foreign keys, check constraints, indexes, and
Identity lengths before application. The `Down` migration drops the new tables;
it is destructive once data exists and is not an approved rollback action.
Prefer backup/restore or a reviewed forward-fix plan for populated databases.
See [migration deployment guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

## Still requires a real SQL Server

- Apply the initial migration to an approved empty test database and verify schema.
- Round-trip Identity/profile/health data and verify SQL constraints and indexes.
- Verify implemented role/Admin provisioning, actual Identity login/password-change
  cookies, lockout and disabled-session enforcement with persisted users.
- Exercise transactions, rowversion update conflicts, concurrent appointment
  locking, reminder deduplication, and service-level ownership/assignment checks.
- Verify encrypted connections and clean Windows assessment setup.

No database-dependent functionality or concurrency acceptance criterion is
marked complete based on model/SQL-generation tests.

## Identity and reminder configuration after database approval

Do not execute the following until a disposable SQL database and its migration
application/seed have been explicitly approved. No command here was executed
against a database during offline development.

After an approved migration deployment, initialize Client/Trainer/Admin roles
using the development-only command:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HealthTrack.Web -- --initialize-identity
```

Optionally supply `SeedAdmin:Email` and `SeedAdmin:Password` through development
user-secrets (or environment names `SeedAdmin__Email`, `SeedAdmin__Password`).
Supply both or neither; never place a real password in commands, source or logs.
The initializer refuses to promote an existing non-Admin account. Remove seed
secrets after provisioning. No default account or password exists.

The reminder worker defaults off. Once the database is ready and verified,
`Notifications__WorkerEnabled=true` enables it;
`Notifications__PollIntervalSeconds` defaults to 60 (allowed 10–3600).
There is no email/SMS integration. Appointments create reminders on approval;
clients explicitly schedule workout/meal reminders. Persisted retries back off
from 60 seconds to at most one hour; failed cycles use a new dependency scope.

## SQL Server acceptance runbook (not executed)

1. Confirm endpoint/version/permissions/TLS and obtain separate permission for an
   empty test database's migrations and synthetic fixtures. Preserve existing data.
2. Review/apply all seven migrations and compare actual tables/constraints/indexes
   with the model. Test forward changes and a rollback plan only in that disposable
   environment. Never run destructive Down migrations on populated user data.
3. Provision roles and synthetic accounts. Register Client/Trainer; log in/out,
   change password, reject old cookies, lock out repeated failures, disable/enable
   accounts and verify assignment revocation. Reject public Admin elevation.
4. Execute all required role journeys through HTTP against real Identity and SQL.
   Reopen the context/app to confirm persisted profiles/plans/logs/measurements,
   appointment history, notifications, read timestamps and audit entries.
5. Run every service query against SQL Server (including correlated trainer counts,
   grouped date/decimal reports, Contains subqueries and pagination). Offline
   LINQ-to-objects tests do not verify SQL query translation or execution.
6. From separate processes/connections, submit overlapping booking requests and
   updates. Test stale rowversions, lock timeout/deadlock outcomes, adjacent slots,
   assignment revocation races and rollback of history/notifications on failures.
7. Run two reminder workers; crash between attempt commit and delivery commit,
   inject DB failures, restart, cancel/reschedule concurrently, and verify unique
   delivery keys, retry intervals and exactly one persisted notification per key.
8. Measure query plans/index usage and representative-load latency. Confirm SQL
   encryption in transit, database/backup encryption and access control; configure
   persistent protected Data Protection keys and HTTPS for the hosting environment.
9. Repeat clean restore/build/test/launch in the confirmed Windows Visual Studio
   assessment environment; record actual results before marking P0 COMPLETE.
