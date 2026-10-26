# Final Remote SQL Server Integration Checklist

Prepared 2026-10-22. All unchecked items are pending; no remote connection or
migration execution is authorized by preparation. Use only synthetic test data.
The backend remains PARTIAL against end-to-end acceptance until evidence exists.

## Approval and environment

- [ ] Confirm lecturer framework/version acceptance, original coursework scope,
  external healthcare-professional/email requirements and Windows assessment inventory.
- [ ] Select an existing supported SQL Server or separately approve provisioning.
  Record edition/version, host architecture, database purpose and owner. Developer
  is dev/test only; production licensing is a separate decision.
- [ ] Approve connection/read-only preflight for the exact server/database.
- [ ] Confirm private network/VPN, TCP port/DNS, authentication, certificate trust,
  verified TLS, compatibility >=130 (proposed SQL Server 2022 baseline 160), collation.
- [ ] Configure Web and dedicated SQL test secrets privately; do not reuse a
  production connection for tests. Record presence only, never credential values.
- [ ] Separate migration and runtime/test privileges; verify metadata visibility,
  required DML and transaction-owned sp_getapplock access without granting sysadmin.
- [ ] Confirm HTTPS, reverse-proxy trust/rate-limit behavior, protected persistent
  Data Protection keys, SQL/backup encryption, retention and backup recovery ownership.
- [ ] Configure an approved private password-recovery delivery adapter and stable
  secure client reset flow. Test delivery without logging tokens. No adapter or
  external credentials have been silently installed/configured.

## Migration execution (separate explicit approval)

- [ ] Review all **seven** migrations, their Down paths and the current snapshot.
  Original migrations remain intact; final model has 28 mapped tables plus migration history.
- [ ] Regenerate `artifacts/final-backend-schema.sql` from the final reviewed build;
  record SHA-256, commit/review identifier (currently no commit) and target database.
- [ ] Compare existing migration history, schema and data against expected baseline.
  If applying completion-value constraints to existing completed goals, backfill
  valid snapshots through a reviewed forward migration before adding constraints.
- [ ] Obtain explicit permission for this script/target/window. Approve fixture writes
  separately. No test fixture applies migrations or creates/drops a database.
- [ ] For populated data, back up and prove restore to a separately approved target;
  stop writers/reminder workers, confirm capacity/DDL lock window and abort procedure.
- [ ] An approved operator executes with a tool supporting GO and abort-on-error.
  Do not pass credentials as command arguments. The offline design factory has no
  connection and is intentionally not a runtime deployment mechanism.
- [ ] Verify exact migration history, 28 mapped tables, indexes, trusted/enabled
  CHECK constraints, FK types/lengths and restrictive deletes. Run the prepared
  read-only `docs/database/phase-04-preflight.sql` only with approved access.
- [ ] Inspect goal type/target/date/completion checks; session-plan guard; composite
  log/plan/client and session/plan/client FKs; unique active assignment, successor
  revision, schedule and delivery indexes; actual rowversions; decimal/date/time types.

## Automated SQL tests and durable behavior

- [ ] Approve synthetic writes to a named `HealthTrack_Integration_...` database.
  Match `HEALTHTRACK_SQL_TEST_DATABASE` exactly, set the approval flag only for this
  run and unset afterward. Verify wrong-target/TLS guards still reject unsafe inputs.
- [ ] Run `dotnet test tests/HealthTrack.IntegrationTests --filter Category=SqlServer
  --logger trx` using the approved private configuration. Record actual pass/fail,
  not offline skips. Tests leave rows for inspection; cleanup needs separate approval.
- [ ] Identity: registration/roles/profiles/hash round trips; reset token replacement
  and security stamps; verify no public Admin promotion or role/profile mismatch.
- [ ] Assignments: duplicate prevention, revocation, stale rowversion and immediate
  loss of trainer access through a new context and real HTTP request.
- [ ] Workouts: exercise/date precision, template ownership, independent clones,
  revision lineage, immutable prior plan/log content, archived history and one
  completion per owned scheduled session. Drafts must never leak into archives.
- [ ] Diet: editing by assigned author, daily planned UTC time, independent consumed
  meals, filtered reads and exact decimal calorie aggregation.
- [ ] Health/goals: weight/height/BMI history, actual constraints, all four goal
  formulas, inclusive deadline/future boundaries, completed-value snapshot,
  rowversion conflicts, batch goal query execution and ownership after revocation.
- [ ] Reports: execute every grouped/correlated/Contains/date query on SQL Server,
  verify decimal/date results against synthetic fixtures, empty states and pagination.
  Distinguish scheduled adherence from plan coverage and free workout counts.
- [ ] Transaction test: saved rows roll back after an injected failure; verify no
  orphan exercises, meals, audit events, history or notifications after failed writes.

## Concurrency, recovery and real HTTP journeys

- [ ] Run concurrent bookings using separate connections, then separate processes:
  exactly one overlapping Pending/Approved reservation for either participant,
  including the same client with different trainers and the same trainer with
  different clients; adjacent intervals allowed,
  safe reschedule/approval races, valid history/notifications and no leaked locks.
- [ ] Force lock timeouts/deadlocks; inspect response handling, rollback and retry
  decisions. Rowversion alone must not be treated as overlap protection.
- [ ] Race assignment revocation/deactivation against trainer writes and cookie
  validation. Verify the current-user refresh and active-assignment checks on SQL.
- [ ] Run concurrent reminder workers; fail/crash after attempt commit and before/
  during delivery commit. Restart and verify persisted backoff, one notification
  per delivery key, cancellation/opt-out/reschedule races and inactive recipients.
- [ ] Verify schedule eligibility and preference changes across processes and that
  re-enabling reminders does not revive cancelled work. Confirm worker graceful shutdown.
- [ ] Real HTTP login/logout/password change/reset, lockout, old-cookie rejection,
  disabled-account rejection, Admin provisioning refusal for existing public users.
  Offline synthetic authentication tests do not cover this acceptance.
- [ ] Execute complete Client/Trainer/Admin journeys with real Identity and SQL;
  include diet, goals, templates, feedback, exports and audit filters. Tamper IDs,
  roles/body fields, rowversions and CSRF tokens; verify 401/403/404/409 boundaries.
- [ ] Verify cookie Secure/HttpOnly/SameSite, no-store/nosniff responses, recovery
  response privacy, redacted errors/logs, export filenames/encoding/formula safety,
  request-size limit and no unauthorized detailed health data in Admin output.

## Performance and assessment

- [ ] Execute the gated 1,000-row grouped report timing observation; record actual
  elapsed time/environment. It asserts correctness, not an invented latency SLA.
- [ ] Inspect SQL execution plans/index usage on representative volume; profile
  dashboards, batch goal queries, paging, exports and reminder due-work reads.
- [ ] Measure multiple simultaneous users/workers, memory, cancellation, security-
  stamp lookup overhead and request latency. Set acceptance targets with the user;
  do not infer throughput from unit tests or a single timing observation.
- [ ] Verify restart durability, approved backup restore and deployment rollback
  rehearsal. Prefer forward fixes/backup restore; never automatically execute
  destructive Down migrations, drop tables or delete existing data.
- [ ] Re-run clean restore/build/tests and application launch in the confirmed
  supported Windows Visual Studio environment; record exact SDK/software versions.
- [ ] Update progress/evidence with actual outcomes. Mark only executed acceptance
  COMPLETE, retain external decisions as BLOCKED, and obtain commit/deployment approval.
