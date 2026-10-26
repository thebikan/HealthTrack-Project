# Phase 04 — SQL Server integration preparation

Date: 2026-10-21. Status: PARTIAL — offline preparation only. Live execution BLOCKED.
No server installation/provisioning, connection, database changes, commit or push.

## Actual preparation results and changed files

- .NET 8 build succeeded: zero warnings and errors. One xUnit analyzer finding
  in the new concurrency assertion was fixed before the final passing build.
- Domain: 23 passed; Application: 74 passed; IntegrationTests: 46 passed and
  **8 SQL tests skipped**. Total: **143 passed, 0 failed, 8 skipped**.
- Eight new offline safety-guard cases passed. Live SQL tests compile and are
  discovered but have NOT been executed; no database claim follows from this run.
- EF reports no pending model changes. Idempotent SQL generated offline.
- Local evidence: `artifacts/test-results/phase-04-preparation/` and
  `artifacts/phase-04-migrations.sql` (ignored artifacts).
- Added `SqlServer/SqlTestEnvironment.cs`, `SqlServerModuleTests.cs` and
  `SqlTestGuardTests.cs` under the existing integration-test project; updated that
  project's user-secret ID. No production service or migration was changed.
- Added this report and `docs/database/phase-04-preflight.sql`; linked the phase
  from `docs/database-setup.md` and `docs/implementation-plan.md`.

## Required environment

The application does not require Enterprise-only database features. Recommended
development/assessment baseline: **SQL Server 2022 Developer**, patched to an
institution-approved servicing release, on supported **x64 Windows or x86-64
Linux**. Both the ARM64 Mac and Windows Visual Studio can connect to this same
server via TCP/TLS over a private network/VPN. Developer is licensed for dev/test,
not a production server. A production deployment requires an appropriately
licensed edition (for example Standard); Express can be evaluated for a small
deployment but its limits/operations must be accepted. SQL Server 2022 Express
has a 10 GB database limit. No edition or license has been purchased/approved.
Sources: [Microsoft editions](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022?view=sql-server-ver16),
[2022 comparison](https://www.microsoft.com/en-us/sql-server/sql-server-2022-comparison).

Do not install an emulated SQL Server container on Apple Silicon. Microsoft
supports Linux containers on Intel/AMD x86-64 hosts, not Rosetta/QEMU/Prism.
[Microsoft container requirements](https://learn.microsoft.com/en-us/sql/linux/containers/deploy).
Windows LocalDB is an optional local Windows assessment environment only; it is
not a shared network database for this Mac. Prefer a normal TCP instance on the
approved Windows machine if both machines need access.

Set SQL Server 2022 database compatibility to **160** for the proposed baseline.
Current EF8 collection queries require **at least 130** for OPENJSON. Merely using
a newer server does not ensure the database has a compatible level. No compatibility
setting was changed. [EF8 breaking changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/breaking-changes#contains-in-linq-queries-may-stop-working-on-older-sql-server-versions).

Required configuration: named dedicated database, known collation, TCP port/DNS,
VPN/firewall access, trusted TLS certificate matching DNS, approved authentication
usable from both clients, and `Encrypt=True;TrustServerCertificate=False;Persist
Security Info=False`. Use a separate migration/deployment identity with the
reviewed DDL permissions; runtime/test identities need table DML, metadata visibility,
and permission to use transaction-owned `sys.sp_getapplock`. Do not grant sysadmin
or db_owner to runtime as a shortcut. No SQL Agent job is needed: reminders use
the application BackgroundService. At-rest/backups encryption and Data Protection
key management need separate deployment review; Developer testing is not production certification.

## Configuration discovery and preparation

Read the project instructions, database guide, Phase 03 report, mapping code,
migrations and transaction adapter; inspected Git status. Existing uncommitted work
was preserved. Checked only presence, never printed secret values:

- `ConnectionStrings__DefaultConnection`: absent.
- `ConnectionStrings__SqlIntegration`: absent.
- `HEALTHTRACK_SQL_TESTS` and `HEALTHTRACK_SQL_TEST_DATABASE`: absent.
- Known Web and SQL-integration development user-secret files: absent.

No approved connection was found in these sources or prior authorization. This
does not prove another terminal, IDE profile, keychain or remote server has none.
No network/database connection attempt was made.

The Web uses `ConnectionStrings:DefaultConnection`; tests deliberately use a
different `ConnectionStrings:SqlIntegration` and secret ID
`HealthTrack.SqlIntegration-development`. The test project is now configured for
that secret ID. No real value or dummy credential was written.
After the user supplies approved details privately, the following prompts without
putting credentials in source or command history (development secrets are not an
encrypted vault):

```sh
python3 -c 'import getpass,json; print(json.dumps({"ConnectionStrings:SqlIntegration":getpass.getpass("Approved SQL integration connection: ")}))' |
  dotnet user-secrets set --project tests/HealthTrack.IntegrationTests
```

Alternatively inject `ConnectionStrings__SqlIntegration` from the approved secret
store/process environment. Never use the app's production connection for tests.
The existing [database guide](../database-setup.md) contains the separate Web setup.

## Prepared live tests — not executed

`tests/HealthTrack.IntegrationTests/SqlServer/` contains eight opt-in SQL tests:

1. Identity registration: actual Identity gateway, persisted user hash/role/profile,
   re-read in a new dependency scope.
2. Trainer assignment/revocation: real rowversion and access loss.
3. Workout create/publish/exercise retrieval/logging and cross-owner rejection.
4. Diet assignment/planned meals and decimal meal-log round trip.
5. Health precision/UTC round trip and rejection by the SQL weight CHECK constraint.
6. Concurrent booking: synchronized requests with separate scopes/connections,
   exactly one reservation and a conflict for the other.
7. Reminder scheduling/delivery/read state across fresh scopes and no second delivery.
8. Reports: actual grouped date/decimal and correlated trainer SQL queries, client
   dashboard and admin/trainer report execution.

Only actor identity/time are controlled by the harness; data access and Identity
registration use the real SQL Server adapters. These are service integration tests,
not full browser/Identity-cookie acceptance. The concurrency test uses separate
connections in one process; cross-process concurrency and crash recovery remain
additional acceptance tasks. Raw SQL failure details in test artifacts may contain
environment identifiers; retain artifacts privately and redact before sharing.

Gates checked before connecting:

- `HEALTHTRACK_SQL_TESTS=I_APPROVE_SYNTHETIC_WRITES` must be set intentionally
  **after explicit user approval of the named test target and test writes**.
- `HEALTHTRACK_SQL_TEST_DATABASE` must exactly match the connection catalog, and
  the name must start `HealthTrack_Integration_` (not a security boundary by itself).
- Valid verified TLS; no attached database, failover partner, insecure encryption
  or persisted security info. Timeout bounded to 10 seconds.
- Once access is authorized, actual DB_NAME, compatibility level and exact applied
  migration set are checked before any seeding. Missing/mismatched schema fails;
  tests never invoke Migrate/EnsureCreated/EnsureDeleted or DDL.

Each test adds random synthetic users and feature rows. The initializer ensures
roles exist; no seed Admin secret is consumed. No cleanup/drop/reset is performed.
Rows remain for inspection, so reruns grow the dedicated test database. Reminder
polling processes any eligible schedules there: isolate this database from users
and other running workers. Data cleanup requires a separate reviewed approval.

After separate migration and synthetic-write approvals, activate the user-scoped
SDK and run (replace the non-secret database name with the approved one):

```sh
export HEALTHTRACK_SQL_TEST_DATABASE=HealthTrack_Integration_approved_name
export HEALTHTRACK_SQL_TESTS=I_APPROVE_SYNTHETIC_WRITES
dotnet test tests/HealthTrack.IntegrationTests --filter Category=SqlServer --logger trx
unset HEALTHTRACK_SQL_TESTS HEALTHTRACK_SQL_TEST_DATABASE
```

Do not set the approval variable merely to make skipped tests green. The ordinary
suite discovers these tests as SKIPPED, not passed, while offline safety-guard
tests validate rejection without opening a connection.

## Migration/transaction review

Reviewed the three Up/Down migrations and latest model; generated a fresh
idempotent SQL script at `artifacts/phase-04-migrations.sql`. No schema change
was needed for preparation. InitialCoreSchema creates 22 application/Identity
tables; AddAccountAudit creates the 23rd; AddReminderDeliveryState adds three
columns, an appointment FK and polling indexes. Migration history is additional.
Generated script inspection finds 24 CREATE TABLE statements including migration
history, 31 foreign keys, 22 CHECK constraints, 6 unique and 29 ordinary index
creation statements, and no cascading deletes. One old polling index is replaced;
these statement counts are not a claim about the final deployed index inventory.

- Identity/profile keys have bounded strings; role-specific profile alternate
  keys enforce FK typing. Every FK restricts deletion (SQL NO ACTION); no cascades.
- Workout logs reference the composite plan/client key, preventing cross-owner
  plan links at the database layer. SQL cannot enforce every dynamic role/assignment
  rule; application authorization remains required.
- Positive weight/height, nonnegative calories/attempts, valid enum states,
  increasing booking intervals and assignment lifecycle CHECK constraints exist.
  Server checks are narrower than application validation; they do not replace it.
- Decimal precision is explicit; dates use date and UTC events datetime2.
  datetime2 does not itself enforce UTC: application validation/converters do.
- Filtered unique active-assignment pairs and unique delivery keys prevent
  duplicates. Rowversion protects updates to appointments, assignments/schedules.
- Owner/date/history indexes and reminder polling indexes exist. Their usefulness,
  grouped queries and locking behavior must be checked with actual SQL query plans.
- Appointment overlap is not enforced by a simple index. Sorted application-lock
  resource names, SERIALIZABLE transactions and an in-transaction overlap query
  implement the strategy; all writers must follow it. sp_getapplock return failures
  become conflicts; real deadlock/timeout behavior still needs verification.
- Multi-row saves commit inside explicit transactions. Reminder attempt/backoff is
  committed before a separate atomic notification/delivered-state transaction.
  Real rollback, interruption and duplicate-race acceptance remains pending.

No execution success is inferred from SQL generation/model inspection.

## Safe execution and rollback checklist

1. Record approved server/version/edition, exact database, purpose, owner, TLS,
   compatibility/collation and deployment/test permissions without credentials.
2. Get **separate explicit migration approval** for that exact target and reviewed
   script. Provisioning a server/database is a separate approval if needed.
3. Take an appropriate backup and prove restore on a separate approved target for
   any populated database; record restore point, downtime and recovery owner.
4. Stop app writes/reminder workers for the planned window. Review script hash,
   baseline migration history, DDL locks, capacity and any pending changes.
5. An approved operator runs the script using a SQL tool that understands GO and
   aborts on errors. The script is idempotent, not permission to rerun blindly.
   Do not use the design-time factory to update a database: it intentionally has
   no connection string. Never pass real credentials on command lines.
6. Validate migration history, trusted/enabled constraints, FKs/indexes, TLS and
   permission boundaries. Read-only queries are prepared in
   [phase-04-preflight.sql](../database/phase-04-preflight.sql); access still needs approval.
7. Approve synthetic test writes separately, run opt-in tests, inspect retained rows
   and failures, then exercise real HTTP login/cookies, process restart, simultaneous
   workers, forced transaction failures and cross-process booking cases.
8. If deployment fails, stop writes and inspect actual committed history/schema.
   Prefer a reviewed forward fix or approved backup restore. Initial Down drops
   tables; Audit Down deletes audit history; Reminder Down discards retry/cancel
   state and linkage. **Do not automatically execute Down or delete test data.**
9. Record actual SQL results and Windows Visual Studio restore/build/launch evidence.
   Only then close the relevant Phase 03 acceptance gaps and consider P1 work.

## Approvals and remaining blockers

| Action | Current authorization/status |
|---|---|
| Source tests/docs, offline build/model/script generation | Authorized and prepared |
| Inspect local configuration presence without values | Completed; none found in checked sources |
| Install/provision server, VM or database | NOT authorized |
| Connect/query identified server | Await endpoint/access authorization |
| Apply migration script | Requires explicit permission for exact target/script |
| Seed/run integration tests | Requires explicit synthetic-write permission; opt-in flag unset |
| Delete rows/database or execute destructive rollback | NOT authorized; separate approval |
| Production deployment/licensing/encryption acceptance | Not established; outside preparation |
| Commit/push | NOT authorized |

Next: select an existing supported SQL host (or approve provisioning separately),
provide non-secret server/version/database/TLS/authentication details, configure the
connection privately, then approve connection preflight, migration and synthetic
test execution as distinct actions. No request for credentials in chat is needed.
