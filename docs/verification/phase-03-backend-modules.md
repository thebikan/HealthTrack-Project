# Phase 03 — Mandatory backend modules and offline verification

Date: 2026-10-21. Commit: none. Lecturer approval: PENDING.
Stack unchanged: ASP.NET Core MVC/net8.0, EF Core 8, Identity, SQL Server,
four-layer modular monolith. Original coursework brief is unavailable.

## Repository inspection and baseline

Read CLAUDE, PRD, TRD, schema, app flow, implementation plan, existing decisions,
source and tests. Preserved the solution and initial migration. Git has a modified
tracked README and untracked source/tests/docs/configuration from prior work;
there is no committed implementation baseline from which to claim an exact
per-session diff. No files were staged, committed, pushed or reverted.

Initial continuation verification reproduced the 21-test foundation result.
Sequential implementation then produced 49 (accounts), 58 (assignments), 65
(workouts), 84 (nutrition/health), 101 (appointments), and 110 (reports) passing
tests. These stages are recorded under ignored `artifacts/test-results/`.
On the latest resume, a clean build independently reproduced **116 passing
tests** including the first reminder implementation. Expanded reminder checks
passed 126; final session/security/HTTP journey coverage passed **135**.

## Actual final verification

| Check | Result |
|---|---|
| User-scoped SDK | `dotnet --version`: 8.0.426 |
| Target/runtime | net8.0; installed .NET and ASP.NET Core 8.0.32; .NET 10 not removed |
| Restore | Successful: all projects up to date; network-enabled NuGet restore |
| Build | Successful, 0 warnings, 0 errors |
| HealthTrack.Domain.Tests | 23 passed, 0 failed, 0 skipped |
| HealthTrack.Application.Tests | 74 passed, 0 failed, 0 skipped |
| HealthTrack.IntegrationTests | 38 passed, 0 failed, 0 skipped |
| Total | **135 passed, 0 failed, 0 skipped** |
| EF migration/model consistency | No changes since last migration; snapshot comparison test passes |
| Offline idempotent script | Generated `artifacts/backend-schema.sql`; not executed |
| Live SQL Server tests | NOT RUN; no approved endpoint/database |
| Windows/Visual Studio, browser UI and load checks | NOT RUN |

Final TRX evidence: `artifacts/test-results/backend-final/` (ignored local artifacts).
Tests named IntegrationTests exercise MVC routing/CSRF/roles with synthetic
authentication and data doubles, Identity events with test stores, background
worker lifecycle, and EF metadata/DDL. They do **not** establish SQL persistence,
transaction isolation, SQL query translation/execution, cross-process concurrency,
or actual Identity database sessions. No alternate database provider was used.

During verification, global.json was externally changed to SDK 10.0.400 twice,
preventing repository-root commands from starting. The user explicitly approved
restoring 8.0.426 while preserving latestFeature/allowPrerelease false. After that
restoration, version/restore/build/tests/migration checks succeeded from the repo.
A test-helper compilation error (Options name shadowed by UserManager.Options)
was corrected before the final passing run. No failing tests remain in that run.

## Feature gap analysis

COMPLETE means the stated acceptance was executed. PARTIAL means implemented
with narrower evidence or missing behavior. BLOCKED identifies an unavailable
prerequisite. NOT STARTED means no implementation, not a placeholder success.
Database-backed modules stay PARTIAL until the SQL acceptance runbook is executed.

| Feature / plan step | Status | Implemented and available evidence | Remaining gaps |
|---|---|---|---|
| Framework/assessment decision 00 | PARTIAL | User provisional authorization recorded | Lecturer approval, original brief and Windows inventory unavailable |
| Four-layer foundation 01 | COMPLETE | Restore/build, architecture guards and .NET 8 execution | Windows assessment certification separate |
| SQL/EF/Identity model 02 | PARTIAL | 23 mapped tables, 3 migrations, UTC checks, constraints/index metadata, consistent snapshot | Migration apply/round trips/real SQL queries BLOCKED |
| Role/Admin setup 03 | PARTIAL | Explicit development initializer, public role allowlist, existing-account promotion refusal | Live idempotent provisioning/rollback tests |
| Authentication/account 04–05 | PARTIAL | Registration, login/logout, profile/password change, lockout configuration, active-user/stamp events; service/HTTP/framework tests | Actual Identity persistence/cookie journeys; password recovery/email confirmation not implemented pending delivery/scope |
| Resource authorization 05 | PARTIAL | Client self access, active trainer assignment, Admin limited to aggregate/account data; unit/HTTP IDOR denial | Full endpoint matrix against real users; race tests |
| Assignments 06 | PARTIAL | Admin assign/revoke, active-role checks, rowversion, audit, immediate service denial after revocation | Live SQL races, isolation and persistence |
| Workout drafts/publish/logs 07–09 | PARTIAL | Validated exercises, assigned author edits, immutable publish, archive, own logs/history; HTTP journey | Live SQL storage/query verification; no full version history |
| Diet/planned meals/logs 10–11 | PARTIAL | Validated trainer assignment, scoped plans, client meal log/history, calorie aggregates | Live SQL; no edit/delete historical diet or log UI/API beyond required create/read flows |
| Health/BMI 12 | PARTIAL | Validated measurements, height snapshot, BMI and dated history | SQL round trips and UI chart integration |
| Booking/overlap 13 | PARTIAL | Pending/Approved reserve time, sorted application locks and serializable transactions in adapter | Actual SQL concurrency, deadlocks, lock permissions and isolation BLOCKED |
| Appointment lifecycle 14 | PARTIAL | Approve/reject/cancel/complete/reschedule; rowversion, history and participant notifications | SQL atomicity/rollback and real-user journey |
| Client dashboard 15 | PARTIAL | Published/current plans, last 30-day workouts, latest measurement/BMI, next appointment and unread count | SQL data/empty-state execution, UI |
| Trainer dashboard/feedback 16 | PARTIAL | Current assignments, paged client activity, scoped progress, feedback create/history | SQL execution/profile; UI |
| Admin dashboard/accounts 17 | PARTIAL | Aggregate usage/status counts, account activation/deactivation, assignment/audit management | SQL totals and deployment authorization regression |
| Trends/performance 18 | PARTIAL | Date-filtered workout/calorie/weight series; BMI available per measurement; trainer performance and plan coverage | SQL grouped/decimal queries and chart/UI; expected session frequency undefined, no fabricated adherence |
| Notifications 19 | PARTIAL | Recipient list/read, one-time workout/meal schedules, automatic appointment reminders, cancel/retry/worker; unit/framework tests | Durable SQL delivery, restart/cross-process races BLOCKED; no email/SMS |
| P0 journey regression 20 | PARTIAL | Two actual HTTP journeys with doubles: assignment/publish/log/report/revoke and booking/approve/read/cancel | Clean seeded SQL + Identity regression BLOCKED; full diet/health HTTP journey still to cover there |
| Fitness goals 21 | NOT STARTED | Deferred | P0 acceptance first |
| Templates/version history 22 | NOT STARTED | Published immutability exists but is not version history | P0 acceptance first |
| Administrative audit / CSV 23 | PARTIAL | Account/assignment audit writes and Admin history reads | Extended event audit and CSV NOT STARTED |
| Trainer feedback history enhancement | PARTIAL | Required feedback history already implemented | Live SQL acceptance; richer optional editing/versioning not implemented |
| Security/performance 24 | PARTIAL | Review and tests below | SQL races, deployment encryption, query plans/load tests |
| Submission evidence 25 | BLOCKED | Project documentation and local test artifacts | Lecturer/original brief/Windows/live SQL/UI evidence |

No AI, payments, wearable, email, external-professional role or other integration
was added. P1 goals/templates/version history/CSV are deliberately deferred while
mandatory live-server acceptance is incomplete.

## Reminder verification detail

| Concern | Offline evidence | Required real SQL evidence |
|---|---|---|
| Notification persistence | Mappings/FKs, service state and SaveAsync paths | Context/app restart round trip |
| Appointment generation | Approval creates two unique schedules, 24-hour UTC due time; reschedule/cancel cancels old schedules | Multi-row transaction commit/rollback |
| Workout/meal reminders | Both kinds wait until due and deliver; invalid/past/non-UTC/beyond-one-year input rejected | Actual polling queries and storage |
| Read/unread | Own read is idempotent; unread filter and other-user 404 tested through services/HTTP | Persisted read timestamp after restart |
| Duplicate prevention | Request UUID hashed with owner; payload conflict; existing delivery reconciliation; unique model indexes | Concurrent workers, SQL unique violations and transaction atomicity |
| Retry | Simulated failed add preserves attempt/backoff; early retry excluded; later retry succeeds | Crash between transactions, unavailable DB and restart |
| Cancellation | Owned rowversion check; cancelled schedule/appointment suppressed; inactive recipients suppressed | Cancel/delivery races and actual rowversion |
| Worker isolation | Disabled worker resolves no DB; scope failure logged safely, worker retries/stops cleanly | DB failure recovery under load |
| Authorization | No owner IDs trusted for scheduling/read; Trainer cannot create personal workout/meal reminders; IDOR denied | Real cookie/account status and role regression |

These are one-time in-app reminders. Approval within 24 hours makes the reminder
immediately due. No recurring plan schedule is inferred. Default worker is OFF;
do not enable it until the approved database is configured and validated.

## Security and performance review

- Identity: password hashing, minimum length 12 and complexity, 5-attempt lockout,
  secure HttpOnly SameSite cookies, security-stamp check each request, inactive
  sign-in/session rejection. Framework/test-store checks pass; real persistence
  and security-stamp replacement across sessions still require SQL.
- Authorization/IDOR: controllers and use cases check role; client self ownership
  or current trainer assignment filters reads/writes. Admin cannot browse personal
  health history. Assignment revocation is checked again within guarded writes.
- CSRF: global AutoValidateAntiforgery, secure antiforgery cookie/header including
  login/register. Real HTTP tests exercise rejection and valid-token journeys.
- SQL injection: EF LINQ parameters and a parameterized sp_getapplock resource;
  no concatenation of user input into SQL text. Live SQL security tests pending.
- XSS/data leakage: JSON DTOs with default serializer escaping, no rendered HTML
  views; consumers must encode text when UI is added. Added nosniff and no-store
  API response headers to avoid caching sensitive API content.
- Validation: lengths, decimal precision/ranges, bounded page size, UTC/date
  intervals, named enums, role allowlists, rowversion and transition rules.
  Invalid ownership, impossible transitions, stale versions and invalid inputs
  are covered by service tests. No claim of exhaustive fuzz/security testing.
- Exceptions/logging: generic API errors; EF detailed/sensitive logging disabled;
  reminder failures omit raw exceptions, credentials and activity/health text.
- Transactions: explicit SQL transactions for multi-row operations and account
  setup; ordered application locks for assignment/booking/reminder writes.
  Code review is not evidence of real rollback/isolation or deadlock behavior.
- Performance: no-tracking reads, projections, bounded paging, batched plan child
  loads, grouped report queries, due-work/owner/date indexes. Server query plans,
  SQL translation/execution and throughput have not been measured. Security-stamp
  checks every request intentionally add DB work and require load testing.
- UTC: grouped report dates explicitly restore UTC Kind in response DTOs.
- Deployment: SQL TLS, at-rest/backup encryption, HTTPS certificates, persistent
  protected Data Protection keys and proxy/rate-limit configuration remain to be
  verified. Health data is not claimed encrypted at rest by this source code.

## Files changed / added during continuation

Because the implementation is untracked, this list describes work relative to
the inspected foundation, not a Git commit diff. All pre-existing files remain.

- Application: `Accounts/Accounts.cs`, `Security/Access.cs`, `Security/ClientAccess.cs`,
  `Persistence/IDataSession.cs`, `Assignments/AssignmentService.cs`,
  `Workouts/WorkoutService.cs`, `Nutrition/NutritionService.cs`, `Health/HealthService.cs`,
  `Appointments/AppointmentService.cs`, `Reporting/ReportingService.cs`,
  `Notifications/NotificationService.cs`, `Notifications/ReminderProcessor.cs`.
- Domain: `Auditing/AuditLog.cs`, `Appointments/AppointmentRules.cs`,
  `Health/HealthCalculations.cs`, `Health/ProgressCalculations.cs`,
  extended `Notifications/Notifications.cs`.
- Infrastructure: `DependencyInjection.cs`, Identity `AccountDirectory.cs`,
  `ActiveAccountCookieEvents.cs`, `ActiveSignInManager.cs`, `CurrentActor.cs`,
  `IdentityAccountGateway.cs`, `IdentityInitializer.cs`; `Notifications/ReminderWorker.cs`;
  Persistence `DataSession.cs`, `HealthTrackDbContext.cs`, `CoreModelConfiguration.cs`;
  `20261021070521_AddAccountAudit` and `20261021073229_AddReminderDeliveryState`
  migrations/designers plus `HealthTrackDbContextModelSnapshot.cs`. Initial migration preserved.
- Web: `Program.cs`, `SafeExceptionMiddleware.cs`, controllers `AccountController.cs`,
  `AssignmentsController.cs`, `WorkoutsController.cs`, `NutritionController.cs`,
  `HealthController.cs`, `AppointmentsController.cs`, `ReportsController.cs`,
  `NotificationsController.cs`.
- Domain tests: `AppointmentRulesTests.cs`, `HealthCalculationsTests.cs`,
  `ProgressCalculationsTests.cs`.
- Application tests: `TestSession.cs`, `AccountServiceTests.cs`,
  `AccessAndAssignmentTests.cs`, `WorkoutTests.cs`, `NutritionAndHealthTests.cs`,
  `AppointmentTests.cs`, `ReportingTests.cs`, `NotificationTests.cs`.
- Integration tests: `AccountHttpTests.cs`, `AccountSessionTests.cs`,
  `BackendJourneyTests.cs`, `ReminderWorkerTests.cs`, updated registration/model/
  migration tests and project file (links the existing data double for HTTP tests).
- Configuration/docs: `global.json`, `CLAUDE.md`, `README.md`, `docs/trd.md`,
  `docs/schema.md`, `docs/implementation-plan.md`, `docs/database-setup.md`,
  decision 0004 and this report. No credential or connection string was added.

## Next action and acceptance boundary

Obtain an approved SQL Server test endpoint, authentication/TLS details, and
explicit permission to apply the reviewed migrations/seed synthetic fixtures.
Execute the [SQL acceptance runbook](../database-setup.md#sql-server-acceptance-runbook-not-executed),
fix actual failures, then mark mandatory modules COMPLETE only with that evidence.
Confirm original coursework requirements, password-recovery delivery requirement,
external-professional scope and Windows assessment compatibility separately.
No deployment, database change, dependency installation, commit or push occurred.
