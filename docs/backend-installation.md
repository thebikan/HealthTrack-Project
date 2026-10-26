# Backend installation and operation

Provisional ASP.NET Core MVC/net8.0, EF Core 8, Identity and SQL Server. Lecturer
and Windows assessment approval remain pending. This guide does not authorize
database provisioning, connecting, migration execution or secret delivery.

## Build and offline verification

On the existing Mac installation:

```sh
export DOTNET_ROOT="$HOME/.local/share/dotnet8"
export PATH="$DOTNET_ROOT:$PATH"
dotnet --version
dotnet restore HealthTrack.sln
dotnet build HealthTrack.sln --no-restore
unset HEALTHTRACK_SQL_TESTS
dotnet test HealthTrack.sln --no-build --no-restore
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/HealthTrack.Infrastructure
dotnet ef migrations script --idempotent --project src/HealthTrack.Infrastructure --output artifacts/final-backend-schema.sql
```

global.json requests SDK 8.0.426 and preserves latestFeature roll-forward within
.NET 8, with prereleases disabled. No .NET 10 migration or system changes occurred.
The SQL tests deliberately skip without explicit opt-in. Tests with synthetic
Identity/data adapters are not substitutes for live SQL verification.

## Runtime configuration after approval

Use `ConnectionStrings:DefaultConnection` in Web development user-secrets, or
`ConnectionStrings__DefaultConnection` injected securely. Never write real values
into appsettings, launch files, source or chat. See [database setup](database-setup.md)
for supported server options, secure prompts and the separate integration-test secret.

After the authorized SQL deployment, the development-only `--initialize-identity`
command provisions roles and optionally a new Admin from `SeedAdmin:Email` and
`SeedAdmin:Password`. It refuses to promote an existing public account. Remove
seed secrets afterward. Startup never migrates, creates or deletes a database.

Launch with an existing trusted HTTPS certificate:

```sh
dotnet run --project src/HealthTrack.Web -- --urls https://localhost:7240
```

Certificate installation/trust, hosting/proxy configuration, production Data
Protection key persistence/encryption, database encryption/backups and recovery
delivery adapters are environment-specific; none were configured by this task.
The API may start without SQL, but data-backed calls cannot work until configured.

## API conventions and feature entry points

- Get `/api/account/csrf`, retain cookies and send the response token in
  `X-CSRF-TOKEN` for every mutation. Refresh the token after authentication changes.
  Secure cookies require HTTPS. Body limit is 256 KiB; collection limits also apply.
- Public registration permits `Client`/`Trainer` only; `Admin` is provisioned.
  JSON enums use their names, timestamps use UTC `Z`, DateOnly uses yyyy-MM-dd,
  TimeOnly uses HH:mm:ss and rowversions use base64. Read rowversions before updates.
- Lists use page/size (default 1/20, maximum size 100). Report/log intervals are
  start-inclusive/end-exclusive UTC, at most 3660 days. Goal/plan DateOnly ranges
  include both endpoint dates. No server-local timezone conversion is inferred.
- `/api/account`: register/login/logout/me/password/users, account activation;
  user listing supports `search`, `active`, `role` filters (Admin only).
- `/api/account/recovery` and `/reset`: request/reset workflow. Requests are
  unavailable until a private delivery adapter is configured. Never log a token
  or use a public/debug inbox as delivery. The API never returns reset tokens.
- `/api/assignments`: Admin assignment/revocation and participant-scoped listing.
- `/api/workouts`: draft/exercise management, publish/archive/revisions, session
  schedule, templates/create/update/archive/clone, owned logs and history. Plan
  lists support status/from/to; Clients may explicitly request Archived history.
- `/api/nutrition`: assign/edit plans, planned meals/time, consumed logs/history;
  plan lists support from/to. Consumed logs remain independent of plan editing.
- `/api/health`: measurements/BMI/height history and assigned-trainer feedback.
- `/api/goals`: create/update/finish, `/clients/{clientId}` progress list.
  Completion requires measured progress >=100%; cancellation is distinct.
- `/api/appointments`: requests, lifecycle, reschedule and history.
- `/api/notifications`: list/read, schedules/cancel, preferences. Workout/meal
  schedules are explicit one-time events. Appointment approval creates reminders.
- `/api/reports`: role dashboards, client progress/adherence, trainer performance,
  Admin audit (date/action/actor filters); `clients/{clientId}/progress.csv`,
  `trainer.csv`, `admin.csv`. CSV uses the same authorization as JSON queries.

The controllers define exact DTO signatures. No frontend was built in this backend
task. Consumers must render returned titles/notes as text, not raw HTML.

## Reminder operation and handoff

`Notifications__WorkerEnabled` defaults false. Enable only after SQL approval and
validation. Polling defaults 60 seconds, configurable 10–3600. The worker stores
attempt/backoff before delivery, checks preferences/eligibility, and saves the
notification/delivered state atomically. Failed cycles use fresh scopes and safe
logs. Cross-process guarantees still require the live tests/checklist.

See [Phase 05 evidence](verification/phase-05-offline-completion.md) and the
[final remote SQL checklist](verification/final-sql-server-integration-checklist.md).
