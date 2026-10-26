# HealthTrack

Provisional ASP.NET Core MVC backend for the HealthTrack coursework project.
The user has authorized .NET 8 development; lecturer approval and the Windows
assessment environment are still pending. See
[decision 0002](docs/decisions/0002-provisional-backend-authorization.md).

## Current scope

The backend contains account/authentication services, assignment and ownership
checks, workout/diet plans and logs, health measurements/BMI, trainer feedback,
appointment lifecycle, role dashboards, progress reports, and in-app reminders.
MVC API controllers and a responsive Razor frontend are implemented. The UI includes
role dashboards, plan builders, logging, appointments, goals, notifications and
administration. See [frontend verification](docs/verification/frontend-ui-review.md)
and [design system](docs/design/design-system.md).
The latest suite has 194 passing tests and 13 explicitly skipped live-SQL tests.
No live SQL Server has been used: persistence,
Identity SQL journeys, query execution, and cross-process concurrency remain
unverified. These modules are PARTIAL against full acceptance, not deployment-ready.
Advanced backend features now include fitness goals, trainer-owned workout
templates/cloning, immutable revisions, scheduled-session adherence, diet editing,
reminder preferences and safe CSV exports. Password-reset token/workflow code
exists; real recovery delivery still needs an approved private adapter.
See [verification and gap analysis](docs/verification/phase-05-offline-completion.md),
[backend installation](docs/backend-installation.md)
and [implementation status](docs/implementation-plan.md).

## Projects

- `src/HealthTrack.Domain`: domain rules; no outward project dependencies.
- `src/HealthTrack.Application`: use cases and contracts; references Domain.
- `src/HealthTrack.Infrastructure`: adapters; references Application and Domain.
- `src/HealthTrack.Web`: MVC composition root; references Application and Infrastructure.
- `tests/HealthTrack.Domain.Tests`: domain tests and dependency guards.
- `tests/HealthTrack.Application.Tests`: application tests and dependency guards.
- `tests/HealthTrack.IntegrationTests`: HTTP host tests; later SQL Server integration tests.

Canonical roles are `Client`, `Trainer`, and `Admin`; "Administrator" is a
display label. Public registration accepts Client/Trainer only. Role/Admin
provisioning requires an explicit development command after database approval.

## Prerequisites and commands

`global.json` selects SDK **8.0.426**, preserving `latestFeature` roll-forward
within .NET 8 and `allowPrerelease: false` at the user's request.
The target is `net8.0`; no runtime major-version roll-forward is configured.
Windows assessment requires a compatible supported Visual Studio installation
and SDK; compatibility with the actual assessment machine remains unverified.

On this Mac, the official ARM64 SDK is installed separately under
`~/.local/share/dotnet8`, with Microsoft.NETCore.App and Microsoft.AspNetCore.App
**8.0.32**. The original system .NET 10 installation remains untouched.
Activate .NET 8 in each development terminal:

```sh
export DOTNET_ROOT="$HOME/.local/share/dotnet8"
export PATH="$DOTNET_ROOT:$PATH"
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

No shell profile was modified. Without activation, the existing system `dotnet`
does not discover the separate user installation. Select this SDK path in IDE
tooling or launch the IDE from an activated terminal when needed.
macOS 27.0.1 was observed locally; Microsoft's published .NET 8 OS support table
currently lists macOS through version 26. Local test success is not official
OS or Windows-assessment certification.

Once prerequisites are available:

```sh
dotnet restore HealthTrack.sln
dotnet build HealthTrack.sln --no-restore
dotnet test HealthTrack.sln --no-build --no-restore
dotnet run --project src/HealthTrack.Web -- --urls https://localhost:7240
```

HTTPS requires an existing trusted development certificate; certificate
installation/trust was not performed. Secure cookies require HTTPS.
Get `/api/account/csrf` and send its `token` as `X-CSRF-TOKEN` on every mutation,
including login/register/logout. Fetch a fresh token after authentication changes.
API timestamps use UTC (`Z`), enums use names, and row versions use base64 JSON.
Lists default to page 1/size 20 (maximum 100); report intervals are end-exclusive.

Tests cover domain/application logic with data doubles, real MVC/CSRF/role
filter journeys with synthetic authentication, Identity events with test stores,
worker failure handling, and offline EF model/migration inspection. They do not
establish SQL persistence, real login sessions, SQL query execution, or concurrent
booking safety. Reminder processing defaults off until SQL Server is approved.

Keep credentials out of source control. See [database setup](docs/database-setup.md)
for environment/user-secrets configuration, supported remote/Windows connection
options, and offline migration commands. No SQL Server connection string or
Admin password is provided. Configure deployment hostnames explicitly; the
initial allowed hosts are `localhost` and `127.0.0.1`.

No system dependencies or databases are installed by this repository.

## Frontend preview

Open `/` on the running MVC host. Assets are local; no Node build is required.
Real sign-in needs the configured SQL Server and HTTPS for secure cookies.
Fixture-based browser review is documented in the frontend verification report;
its screenshots do not establish live database acceptance.
