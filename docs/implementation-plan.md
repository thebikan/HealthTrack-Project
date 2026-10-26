# HealthTrack — Stepwise Implementation Plan

## Workflow
The user authorizes sequential backend development under [decision 0002](decisions/0002-provisional-backend-authorization.md).
For each numbered step, read its required docs, implement its scope, add relevant
tests, execute validation, fix failures, and report evidence before advancing.
Proceed without repeated approval when the next phase is not blocked. Do not
advance past failed required acceptance checks or claim unavailable checks passed.
The user's continuation authorization permits offline backend services and HTTP
regression work after available tests pass, while deferring live SQL acceptance.
See [decision 0004](decisions/0004-offline-backend-continuation.md).
Mark status COMPLETE, PARTIAL, BLOCKED, or NOT STARTED with evidence and a date.
Commit reference remains "none" until explicit commit approval.
Decision 0005 authorizes remaining P1 backend work before live SQL acceptance. Frontend UI and
browser E2E work are separate; step 20 covers backend HTTP/service journeys.
Steps 04–05 include password change/reset (delivery-dependent), current profile,
and disabled-session enforcement; step 17 includes account management.
Trainer feedback required by the PRD belongs to step 16, not only a P1 extension.

| # | Goal | Main files / areas | Done when (verification) |
|---|---|---|---|
| 00 | Confirm MVC 5 vs ASP.NET Core and assessment environment | docs/trd.md, decision log | Lecturer/framework/version decision recorded; no incompatible scaffolding |
| 01 | Scaffold solution and references | src/* projects, solution, tests | Build passes; dependency direction correct |
| 02 | Configure SQL Server, EF/Identity | Infrastructure/Persistence, Web config | Connection works; migration cleanly applies to blank DB |
| 03 | Add Identity roles and secure Admin seed | Infrastructure/Identity, Web startup | Client/Trainer/Admin roles exist; no public Admin creation |
| 04 | Registration/login/logout | Web/Account, Application/Auth | Login/logout and invalid-account tests pass |
| 05 | Client/Trainer profiles and authorization | Domain/Profiles, Application/Identity | Client A cannot access Client B records by ID |
| 06 | Trainer-client assignment | Assignments feature & EF config | Admin assigns/unassigns; revoked Trainer loses access immediately |
| 07 | Workout plan drafts and exercises | WorkoutPlans feature, migrations | Assigned Trainer can create/edit draft with validated exercise fields |
| 08 | Publish/assign workout plan | WorkoutPlans services | Client sees only authorized published plans |
| 09 | Workout logs/history | WorkoutLogs feature | Client logs workout; refresh preserves data; other client denied |
| 10 | Diet plan and planned meals | DietPlans feature | Trainer assigns diet plan; intended client sees meals |
| 11 | Meal logging and calories | MealLogs feature | Valid totals shown; negative calories rejected |
| 12 | Health measurements and BMI | HealthMetrics feature | Weight history and BMI unit tests pass; zero height fails |
| 13 | Appointment request and overlap protection | Appointments feature | Parallel requests cannot create overlapping approved bookings |
| 14 | Appointment transitions and history | Appointments services | Approve/cancel/reschedule valid; invalid transitions fail |
| 15 | Client dashboard query model | Client/Dashboard | Plans, metrics, next appointment accurate, empty state works |
| 16 | Trainer dashboard query model | Trainer/Dashboard | Only assigned client data is visible |
| 17 | Admin dashboard & reports | Admin/Dashboard, Analytics | Aggregate statistics correct; no unauthorized PHI in response |
| 18 | Progress charts/report query endpoints | Analytics feature | Date filters and charts use real seeded records |
| 19 | In-app notifications/reminder store | Notifications, background worker | Reminder state persists; retries don't duplicate sends |
| 20 | P0 full journey regression tests | Integration, E2E | All role journeys pass in clean seeded environment |
| 21 | P1 fitness goals | FitnessGoals feature | Goal direction/formulas tested; scope guarded |
| 22 | P1 plan templates/versioning | WorkoutTemplates/Versions | Old completed workout history unchanged after new version |
| 23 | P1 audit logging and CSV export | Audit/Reporting | Role-safe export; administrative actions have audit metadata |
| 24 | Hardening & profiling | All modules/tests | Authorization, CSRF, XSS, invalid input, concurrency, load tested |
| 25 | Submission evidence and Viva | docs/, README, screenshots | Clean installation verified, architecture/ER/UML and individual report evidence prepared |

## Definition of done for every step
- Required acceptance tests actually executed and results documented.
- No unrelated files or modules modified without stated justification.
- Data access authorized at service/query level as well as controller level.
- New schema changes have migration and rollback consideration.
- Code review and commit happen after explicit approval.

## Current status — 2026-10-22

The user's decision 0005 supersedes earlier advanced-feature deferral. All listed
backend use cases now have implementation; live SQL acceptance is still separate.

| Steps | Status against full acceptance | Current evidence / remaining work |
|---|---|---|
| 00 | PARTIAL | User provisional stack approval; lecturer/assessment details pending |
| 01 | COMPLETE | .NET 8.0.426 restore/build, layer guards and offline tests pass |
| 02 | PARTIAL | 28 mapped tables, 7 consistent unapplied migrations; live SQL BLOCKED |
| 03–05 | PARTIAL | Account/auth/profile/roles/ownership and reset workflow implemented; real recovery delivery and SQL sessions pending |
| 06 | PARTIAL | Assignment/revocation/audit implemented and offline tested; SQL races pending |
| 07–09 | PARTIAL | Draft/exercises/publish/archive/revisions/sessions/logs/filtering implemented; SQL durability pending |
| 10–12 | PARTIAL | Diet create/edit/time/filter, meal logs, health/height/BMI history implemented; SQL round trips pending |
| 13–14 | PARTIAL | Lifecycle/history/notifications/audit and locking code; live concurrency pending |
| 15–18 | PARTIAL | All role dashboards, scoped feedback, dated reports/adherence/coverage, audit filters and CSV; SQL execution pending |
| 19 | PARTIAL | Persistent reminder engine, retries/cancel/preferences implemented; SQL restart/race tests pending |
| 20 | PARTIAL | Offline HTTP role journeys pass; real Identity/SQL end-to-end execution BLOCKED |
| 21 | PARTIAL | Four goal types, updates/completion snapshots and progress implemented/offline tested; live SQL pending |
| 22 | PARTIAL | Private templates/cloning and immutable version lineage implemented/offline tested; live SQL pending |
| 23 | PARTIAL | Expanded audit events and safe role-scoped CSV implemented/offline tested; live SQL pending |
| 24 | PARTIAL | Code-level security/performance fixes and regressions pass; deployment/load/SQL checks pending |
| 25 | BLOCKED | Lecturer/original brief, Windows assessment and final SQL evidence unavailable |

Final available suite: **190 passed, 0 failed, 13 live-SQL tests skipped**.
Build: zero warnings/errors. Model/snapshot consistent; SQL generated offline.
No remote connection, migration execution, production deployment or commit occurred.

See [Phase 05 completion evidence](verification/phase-05-offline-completion.md),
[decision 0005](decisions/0005-complete-offline-backend.md),
[backend installation](backend-installation.md) and the
[final SQL checklist](verification/final-sql-server-integration-checklist.md).
Historical Phase 01–04 reports describe their original scope/results, not current gaps.

Next: configure the approved remote SQL endpoint as the final stage, approve
migration and synthetic test writes explicitly, execute the checklist and fix actual
SQL findings. Configure private recovery delivery and confirm assessment decisions.
Do not mark full backend verification COMPLETE based on offline tests alone.

## Frontend implementation — 2026-10-24

The user authorized the full MVC frontend redesign. Shared Razor shell, role
workflows, local Chart.js and browser modules are implemented. Offline suite:
194 passed, 13 SQL skips. Responsive fixture-backed browser review is recorded in
[frontend UI review](verification/frontend-ui-review.md). This supersedes the
earlier statement that frontend work is not started; it does not complete the
SQL-dependent acceptance items above. No commit or deployment was made.
