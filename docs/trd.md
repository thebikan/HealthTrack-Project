# HealthTrack — Technical Requirements (TRD)

## Provisional development decision
The working coursework interpretation is **ASP.NET MVC + C# in Visual Studio**,
evaluated on VS 2017 or newer. Lecturer acceptance of Core MVC and the exact
assessment environment remain unverified. The user explicitly authorizes
provisional development on the stack below despite those risks; this is not
lecturer approval. See [decision 0002](decisions/0002-provisional-backend-authorization.md).

Step 00's academic confirmation remains pending. See [decision 0001](decisions/0001-framework-and-assessment-environment.md) for outstanding lecturer questions. Decision 0002 supersedes its prohibition on provisional implementation. A local SDK installation does not establish assessment compatibility.

## Provisionally authorized stack (ASP.NET Core route only)
User preference recorded on 2026-10-20: C#, ASP.NET Core MVC on .NET 8,
Entity Framework Core 8, ASP.NET Core Identity, and Microsoft SQL Server.
Development is planned on macOS using VS Code and the .NET SDK, followed by
verification in supported Windows Visual Studio. Lecturer approval, assessment
software versions, and the SQL Server environment remain **pending**.

| Layer | Choice |
|---|---|
| UI | MVC Razor views, Bootstrap 5, small JavaScript enhancements, Chart.js |
| App | C#, ASP.NET Core MVC, .NET 8 (subject to approval/runtime support) |
| Data | Microsoft SQL Server, EF Core 8; LocalDB only as a possible Windows option |
| Auth | ASP.NET Core Identity (cookie auth), authorization policies/resource checks |
| Jobs | DB-backed reminders plus BackgroundService; single-instance demo |
| Tests | xUnit, WebApplicationFactory, Playwright |
| Logging | ILogger, redaction of sensitive data |

If **MVC 5/.NET Framework** is required, change Identity/ORM/bootstrap configuration and record that as a formal decision BEFORE implementation. Do not silently mix framework-specific packages.

Compatibility risks: targeting .NET 8 requires Visual Studio 2022 17.8 or
newer, with an SDK-compatible Visual Studio version; the coursework's
"2017 or higher" wording is insufficient. .NET 8 support ends 2026-11-10.
LocalDB is a Windows option, and SQL Server containers on Apple Silicon via
emulation are not a supported database baseline. See decision 0001 for
Microsoft sources, exact lecturer questions, and environment checks.
Provisional implementation is authorized; execution still requires compatible
targeting packs, runtimes, and SQL Server access.

Development now selects SDK 8.0.426 (`global.json`, `latestFeature` roll-forward
within .NET 8, prereleases disabled; preserved per user instruction).
Its 8.0.4xx feature band requires Visual Studio 2022 17.11 or newer compatible
tooling; choose a currently supported Visual Studio release for assessment.
The SDK and .NET/ASP.NET Core runtimes 8.0.32 are installed user-wide on this
ARM64 Mac. See [verification](verification/phase-02-offline-persistence.md) and
[database configuration](database-setup.md). Windows verification remains pending.

## Logical architecture
Compile-time references: Domain has no project dependencies; Application references
Domain; Infrastructure references Application and Domain; Web references
Application and Infrastructure as the composition root. Deployment is one
modular-monolith web application, not microservices. Controllers validate
requests and delegate to services; services enforce authorization and domain transitions.

## Solution layout
`src/HealthTrack.Domain`, `src/HealthTrack.Application`, `src/HealthTrack.Infrastructure`, `src/HealthTrack.Web`;
`tests/HealthTrack.Domain.Tests`, `tests/HealthTrack.Application.Tests`,
`tests/HealthTrack.IntegrationTests`; `docs/`. Browser E2E tests belong to a
later frontend phase. Canonical Identity roles are `Client`, `Trainer`, and
`Admin`; "Administrator" is the display name for `Admin`.

## Boundaries and constraints
- Server determines signed-in user; user-supplied owner IDs are untrusted.
- Client records belong to a Client user, directly or through parent FKs; Trainer reads require active assignment; Admin reports aggregate by default.
- Transactions for multi-row writes; appointment conflict protection must withstand concurrency; date/time values stored UTC.
- Use strongly typed DTO/ViewModels; ASP.NET Identity password hashing; anti-forgery tokens on form mutations, server-side validation and output encoding.
- No unnecessary health metrics in logs/analytics exports. Seed synthetic data only.
- No Redis, Kubernetes, payment gateway, external AI API or cloud auth required for coursework.

## Configuration names (no secrets)
`ConnectionStrings__DefaultConnection`, `ASPNETCORE_ENVIRONMENT`, `SeedAdmin__Email`
(optional local dev), `SeedAdmin__Password` (dev-secret only),
`Notifications__WorkerEnabled` (default false), `Notifications__PollIntervalSeconds`
(default 60, allowed 10–3600). Prefer .NET user-secrets for developer credentials.
Identity password recovery is implemented with one-hour tokens and security-stamp
invalidation. `IPasswordRecoveryDelivery` has an explicitly unconfigured default;
real private delivery remains external/PARTIAL. No SMTP option is active.

## Implemented backend behavior and verification boundary

See [decision 0004](decisions/0004-offline-backend-continuation.md) and
[module gap analysis](verification/phase-03-backend-modules.md). Application
services use a small context port (`IDataSession`); EF and Identity remain in
Infrastructure. SQL Server application locks and serializable transactions
protect booking writes in code. SQL behavior has not been tested on a server.
Identity cookies validate security stamps on every request, reject inactive
users, and return API 401/403 responses. Public registration cannot create Admin.
Sensitive API responses use `Cache-Control: no-store`; logs omit exception detail
and health values. Data Protection key storage, HTTPS certificate deployment,
SQL TLS and database/backup encryption still require environment verification.

Decision [0005](decisions/0005-complete-offline-backend.md) extends this with
trainer-private workout templates, immutable plan revisions, explicit UTC workout
sessions/adherence, four goal types, diet editing/planned UTC times, reminder
preferences, expanded audits and bounded safe CSV. See the latest
[offline completion report](verification/phase-05-offline-completion.md).

## Local commands (only for selected ASP.NET Core route)
Activate the installed SDK as described in README, then `dotnet restore`,
`dotnet build`, `dotnet test`, and `dotnet tool restore`.
For offline schema review use `dotnet ef migrations has-pending-model-changes
--project src/HealthTrack.Infrastructure` and `dotnet ef migrations script
--idempotent --project src/HealthTrack.Infrastructure`.
Migration application is not authorized until the SQL Server environment is
confirmed. No automatic database creation, update, or seeding runs at startup.

## Nonfunctional acceptance
Clean-install reproducibility in lecturer-approved Visual Studio, keyboard-friendly and mobile-friendly UI, authorization tests, reproducible seed data, no committed secrets. Benchmark responsiveness empirically; do not assert unmeasured speeds.
