# Phase 05 — Offline backend completion

Date: 2026-10-22. Offline implementation completed; live SQL verification pending. User explicitly authorizes advanced backend
implementation before live SQL acceptance; prior deferral is superseded.

## Initial audit checklist

| Module | Initial status | Unblocked work |
|---|---|---|
| Accounts/auth | PARTIAL | Identity password recovery, security audit events |
| Resource authorization | PARTIAL | Extend ownership regressions for new features |
| Workouts | PARTIAL | Filters, templates/cloning, version lineage, adherence denominator |
| Diet | PARTIAL | Author-only editing, planned times, filters |
| Health/goals | PARTIAL | Goal types/calculations/lifecycle and reporting |
| Trainer | PARTIAL | Extend existing scoped progress/feedback services |
| Admin | PARTIAL | Account/report filters and additional audit events |
| Appointments | PARTIAL | Extend audit events and regression cases |
| Notifications | PARTIAL | Preferences and delivery-time opt-out |
| Reporting | PARTIAL | Goal/adherence metrics and safe CSV exports |
| Security/performance | PARTIAL | Review new surfaces, bounded exports, regression tests |
| Database | BLOCKED | Prepare migrations/tests/checklist offline; no connection |
| Assessment approval | BLOCKED | Original brief, lecturer and Windows environment external |

All existing source/tests/mappings and planning/decision/verification documents
were inspected. Existing uncommitted work is preserved; global.json remains
8.0.426 with latestFeature roll-forward. No SQL tests will be enabled in this task.

## Final module status

There are **12 backend module areas** below. Eleven have COMPLETE offline
implementation/available verification; accounts are PARTIAL only for external
private recovery delivery. Every data-backed module remains PARTIAL against full
SQL/end-to-end acceptance. These labels do not certify production readiness.

| Module | Offline implementation | Full acceptance | Result |
|---|---|---|---|
| Accounts/auth | PARTIAL | PARTIAL | Registration/login/logout/password/profile/roles/status, stamp refresh, recovery tokens/workflow and audits exist; private delivery external |
| Resource authorization/assignments | COMPLETE | PARTIAL | Role + owner/assignment checks, revocation, Admin restrictions and regressions |
| Workouts | COMPLETE | PARTIAL | Draft/exercises/publish/archive/filter/log/history, explicit sessions, templates/clones and immutable revisions |
| Diet/nutrition | COMPLETE | PARTIAL | Author-only editing, date filters and optional daily UTC meal times; consumed logs independent |
| Health/goals | COMPLETE | PARTIAL | Measurements/height/BMI history, four goal types, direction/date/count validation, updates/cancel/completion snapshots |
| Trainer | COMPLETE | PARTIAL | Assigned clients/progress, plans, feedback history, appointments and scoped performance |
| Administrator | COMPLETE | PARTIAL | Aggregate dashboard, user filters/activation, assignments, audit date/action/actor filtering |
| Appointments | COMPLETE | PARTIAL | Lifecycle/adjacent/both-participant overlap logic, serialized adapter, history/notifications and transition audits |
| Notifications | COMPLETE | PARTIAL | Durable schema/processor, retry/cancel/UTC/preferences and worker failure/shutdown checks |
| Analytics/exports | COMPLETE | PARTIAL | Dated trends, separate adherence/coverage, goals, role reports and safe UTF-8 CSV |
| Audit metadata | COMPLETE | PARTIAL | Registration, successful login/logout, password change/reset, initial Admin provisioning, account/assignment and appointment events |
| Security/performance code review | COMPLETE | PARTIAL | Regression checks and safe optimizations below; live load/encryption/SQL verification pending |

BLOCKED: remote SQL setup/migration/execution, private recovery delivery integration,
production hosting/keys/encryption confirmation and external assessment decisions.
NOT STARTED: no authorized offline feature remains in that category. Frontend,
AI/payment/wearable integrations and unrelated infrastructure remain outside this task.

## Actual validation

Independent baseline reproduced **143 passed, 0 failed, 8 SQL tests skipped**.
Subsequent executed stages: recovery 148 passed; workout Application suite 81
passed; feature suite 182 passed; HTTP/security suite 185 passed. Final run:

| Check | Actual result |
|---|---|
| `dotnet --version` | 8.0.426, user-scoped SDK; target still net8.0 |
| Restore | Successful, all projects up to date |
| Clean/build | Successful, 0 warnings/errors |
| Domain tests | 31 passed |
| Application tests | 106 passed |
| IntegrationTests | 53 passed, 13 SQL tests skipped |
| Total | **190 passed, 0 failed, 13 skipped** |
| EF model consistency | No model changes pending; snapshot test passes |
| SQL script | `artifacts/final-backend-schema.sql`, offline idempotent generation only |
| Database/network to SQL | Not performed |
| External dependency installs / commits / pushes | None |

The final model check used the documented Infrastructure project as its startup
project. An initial invocation with Web as startup could not load the private
EF Design dependency; the documented command succeeded without code changes.

TRX evidence: `artifacts/test-results/phase-05-final-booking/`. SQL tests were explicitly
disabled with an unset approval variable. Tests include real Identity token-provider
and UserManager behavior against a test store, HTTP/CSRF/role pipelines with
synthetic authentication/data, application/domain cases and offline model/DDL checks.
These are not evidence of SQL translation/execution, isolation, real login cookies,
durable storage, cross-process concurrency or Windows assessment compatibility.
An intermediate build overlapped an unfinished source edit and failed to resolve a
new DTO; the completed source subsequently passed clean builds. No tests were weakened.

## Added behavior and acceptance evidence

- Reset tokens: real Identity tests verify expiry, tampering, purpose/user binding,
  password replacement and security-stamp/token invalidation. Request/reset endpoints
  enforce CSRF/rate limiting; APIs never return tokens. Default delivery explicitly
  reports unavailable. Delivery is not simulated as successful in production code.
- Revisions/templates: new draft IDs and lineage preserve old logs/exercises;
  owner/current-assignment checks, independent clones, archived templates and draft
  revision rejection are tested. Sessions are bounded, UTC, plan-dated, immutable
  after publish, and complete only once. Drafts cannot enter client-visible archives.
- Diet editing: only the assigned creating trainer can replace planned meals;
  independent consumption history and time fields remain intact. Revocation blocks edits.
- Goals: measured baseline, gain/loss direction, missing data, no zero denominator,
  integer counts, distinct UTC days, deadline boundaries, stale rowversion, ownership
  and immutable completion result are tested. No diagnoses or health recommendations.
- Preferences: per-user isolation, disabled scheduling, pending cancellation,
  delivery-time recheck and no resurrection on re-enable; worker cancellation/retry
  and duplicate tests remain passing.
- Reports/CSV: real elapsed scheduled-session denominator, distinct linked
  completions, null without schedule, separate coverage; export ownership/Admin
  restriction, formula protection, quoting, UTF-8 BOM and CRLF are tested.
- Audits: appointment lifecycle generates metadata alongside status history;
  Admin-only filtered audit reads tested. Identity adapter audits require live SQL
  atomicity verification; their existence is not a successful persistence claim.
- HTTP regressions cover diet edits, goals, preferences, exports, role denial,
  CSRF, invalid targets, recovery privacy, error-detail redaction and no-store headers.

## Security findings and fixes

1. Archive visibility: allowing an unpublished draft to be archived would expose
   it through the new history filter. Archive now requires Published; regression
   verifies draft archive rejection and no client history disclosure.
2. Current-user refresh: changed current-actor reads to no-tracking queries so
   repeated authorization in a request does not reuse an EF-cached activation value.
3. Recovery: trusted Identity token protection, one-hour lifespan, stamp rotation,
   generic API errors, bounded input, CSRF and rate limiting; no token/log delivery.
4. Exports: fixed filenames, same resource authorization, bounded output, invariant
   numeric formatting, full quoting and formula/control-character prefix protection.
5. Data history: immutable workout versions, session uniqueness/owner FKs and
   completed-goal snapshots prevent later edits/backfills from changing recorded results.
6. Error handling: API/worker redaction remains; identity initializer CLI now exits
   with a sanitized diagnostic rather than emitting raw database exceptions.
7. Request bodies limited to 256 KiB in MVC; list/exercise/session/export bounds
   remain. Cookie/CSRF/security-stamp tests continue to pass; no authorization relaxed.

8. Appointment ownership of time: overlap checks now cover both the trainer and
   the client, including a client booking different trainers. Request, approval and
   reschedule share this check and retain sorted participant transaction locks.
   The offline regression passes; the separate-connection SQL race test is gated.

Review found no new raw SQL user interpolation, token/credential logging, unguarded
new controller, public Admin role assignment, or raw HTML output. This is a code
review/regression result, not a penetration-test or deployed encryption certificate.
Failed unknown-user login attempts intentionally do not create per-account audit
rows; lockout/rate limiting and sanitized operational diagnostics remain the controls.

## Performance changes and limits

- Batched plan child reads now use lookups instead of scanning every child list for
  each parent. No extra database call per plan/template.
- Goal list progress uses at most two aggregate queries for a page, rather than
  querying once per goal; a regression checks the bounded query count.
- Completed goals use stored results without recalculating history; projections,
  no-tracking reads, page limits and cancellation propagation remain.
- New owner/status/date, session uniqueness and revision indexes support the features.
- CSV output is bounded to 11,000 rows/2,000 characters per cell. Due-work polling
  remains batches of 50. Actual SQL plans, memory/load and latency are unmeasured.
- A gated 1,000-synthetic-meal report observation is prepared, NOT RUN. It records
  elapsed time when authorized without asserting an invented latency threshold.

## Changed files and migration review

New Application files: `Accounts/PasswordRecovery.cs`, `Workouts/WorkoutExtensions.cs`,
`Health/GoalService.cs`, `Reporting/ReportExports.cs`. Updated account/workout/diet/
report/notification/appointment services; no architecture or database provider change.

New Domain file: `Health/FitnessGoal.cs`. Extended WorkoutPlan/WorkoutLog plus
WorkoutTemplate/WorkoutTemplateExercise/WorkoutSession, planned meal UTC time and
NotificationPreference. New Infrastructure file: `Identity/IdentityPasswordRecovery.cs`;
updated registration/DI, current actor, Identity account gateway/initializer, context
and model configuration. New Web controllers: `PasswordRecoveryController.cs`,
`GoalsController.cs`; extended workout/diet/report/notification/account controllers
and Program safeguards.

New tests: PasswordRecoveryTests, WorkoutExtensionTests, GoalTests,
GoalCalculationTests, ExportAndAdherenceTests, PasswordResetTokenTests and
SqlServerPerformanceTests. Extended HTTP journeys, account/appointment/notification/
nutrition tests, data double query counting, model count and real SQL module tests.

Added four migrations (plus generated designers and updated snapshot):

- `20261022052102_AddWorkoutLibraryAndSessions`
- `20261022052435_AddGoalsPreferencesAndMealTimes`
- `20261022053124_AddGoalCompletionSnapshot`
- `20261022053338_AddGoalAndSessionIntegrityChecks`

Total seven unapplied migrations, 28 mapped tables. Composite session/plan/client
FK and session-plan CHECK protect ownership links; goal completion-value check
guards stored completion. Initial migrations remain intact. Down paths drop new
tables/columns/history and require explicit approval; no automatic rollback is safe
for populated data. A future deployment containing preexisting completed goals must
review/backfill completion values before enforcing the new constraint.

Documentation: updated README, CLAUDE (still concise), PRD, TRD, schema, implementation
plan and database guide; added decision 0005, backend installation guide, this report
and `final-sql-server-integration-checklist.md`. Historical phase reports remain historical.

## Exact next actions

1. Configure the approved supported remote SQL Server privately as the final stage.
2. Approve connection preflight, the reviewed seven-migration script and synthetic
   test writes separately for the named dedicated target.
3. Run the 13 gated SQL tests and the [final checklist](final-sql-server-integration-checklist.md),
   including real Identity HTTP sessions, cross-process races, restart/recovery and
   measured query/performance behavior; fix actual failures before full completion.
4. Configure approved recovery delivery/production protected keys and confirm
   lecturer/coursework/Windows assessment requirements. No additional unapproved
   backend integration, system install, database mutation or commit was performed.
