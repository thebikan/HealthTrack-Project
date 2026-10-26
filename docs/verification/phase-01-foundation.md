# Phase 01 — Solution foundation verification

Original attempt: 2026-10-20. Current status: COMPLETE after .NET 8 verification.
Commit: none.

Update verified 2026-10-21: the user authorized .NET 8 installation. SDK 8.0.426
and both shared runtimes 8.0.32 are installed user-wide. Restore/build succeeded
with zero warnings/errors, and the three original tests passed (one per project).
See [installation and subsequent persistence evidence](phase-02-offline-persistence.md).
The blocked results below are retained as the historical first attempt, not
the current environment status.

## Delivered scope

Reconciled provisional authorization, canonical `Admin` naming, three test project
names, sequential backend workflow, and unverified coursework scope. Created
four source projects and three xUnit test projects in a classic `.sln`, all
targeting `net8.0` with C# 12 and nullable reference checking.

The MVC composition root registers controllers/views, problem details, exception
handling, HTTPS redirection, and production HSTS. These settings compiled;
runtime behavior is not verified. No feature controllers, Identity, entities,
EF configuration, migrations, or business modules exist yet. Empty layer projects
are foundation boundaries, not completed modules.

## Original executed checks (before installation)

| Check | Result |
|---|---|
| SDK/runtime inventory | SDK 10.0.203; only Microsoft.NETCore.App and Microsoft.AspNetCore.App 10.0.7 |
| Docker availability | CLI 29.5.2; daemon socket absent, connection failed |
| SQL Server connectivity | Not executed; no accessible instance supplied |
| Static project reference graph | PASS; four source projects follow the approved inward dependency direction |
| MSBuild property evaluation | PASS; Web is net8.0/C# 12 and Domain.Tests is net8.0/IsTestProject=true |
| NuGet restore | PASS for all seven projects, including .NET 8 reference packs |
| Solution build | PASS; seven projects, zero warnings, zero errors |
| xUnit execution | BLOCKED; all three test runs aborted before test execution, zero tests passed |
| Lecturer/Windows assessment verification | Pending; not inferred from local compilation |

Initial sandbox restore could not resolve `api.nuget.org`. A permitted
network-enabled retry succeeded. Initial sandbox test execution failed on local
MSBuild pipe permissions; a permitted retry exposed the actual missing-runtime
blocker. Stalled initial processes were terminated.

The retry reported:

```text
Framework: 'Microsoft.NETCore.App', version '8.0.0' (arm64)
.NET location: /usr/local/share/dotnet/
The following frameworks were found:
  10.0.7
Test Run Aborted.
```

The baseline 8.0.0 request permits compatible 8.0 servicing patches; it does not
require installing the obsolete initial patch. ASP.NET Core 8 is also required
for Web and its integration tests. No .NET 10 runtime roll-forward was used.

## Reproduction

Commands were run from the repository root using these process-local settings:

```sh
export DOTNET_CLI_HOME=/tmp/healthtrack-dotnet-cli
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export NUGET_PACKAGES="$PWD/artifacts/packages"

dotnet restore HealthTrack.sln --source https://api.nuget.org/v3/index.json --disable-parallel
dotnet build HealthTrack.sln --no-restore --disable-build-servers -m:1
dotnet test HealthTrack.sln --no-build --no-restore --disable-build-servers -m:1 --logger trx --results-directory artifacts/test-results
```

TRX files are in ignored `artifacts/test-results/` and record aborted runs.
Packages/reference packs are repository-local in ignored `artifacts/packages/`.
No system SDK/runtime or SQL Server dependency was installed. The first CLI
invocation emitted certificate-generation/keychain diagnostics; no certificate
trust command was run, and later invocations disabled certificate generation.

## Original acceptance and next action (resolved by installation)

Solution creation, references, and build criteria pass. Test execution remains
blocked, so this phase is not marked COMPLETE. Do not begin the dependent
database phase until required foundation tests execute successfully.

Provide an environment containing .NET 8 and ASP.NET Core 8 runtimes, or
separately approve installation of a compatible .NET 8 SDK/runtime. Then rerun
the test command and fix any actual test failures. SQL Server work subsequently
requires an authorized reachable server and development database permissions;
no SQLite substitution or emulated-database success claim is allowed.

## Exact files changed in this phase

Existing files edited (some were already untracked before this phase):

- `CLAUDE.md`
- `README.md`
- `docs/prd.md`
- `docs/trd.md`
- `docs/implementation-plan.md`
- `docs/decisions/0001-framework-and-assessment-environment.md`

New files:

- `.gitignore`
- `Directory.Build.props`
- `HealthTrack.sln`
- `docs/decisions/0002-provisional-backend-authorization.md`
- `docs/verification/phase-01-foundation.md`
- `src/HealthTrack.Domain/HealthTrack.Domain.csproj`
- `src/HealthTrack.Application/HealthTrack.Application.csproj`
- `src/HealthTrack.Infrastructure/HealthTrack.Infrastructure.csproj`
- `src/HealthTrack.Web/HealthTrack.Web.csproj`
- `src/HealthTrack.Web/Program.cs`
- `src/HealthTrack.Web/appsettings.json`
- `tests/Directory.Build.props`
- `tests/HealthTrack.Domain.Tests/HealthTrack.Domain.Tests.csproj`
- `tests/HealthTrack.Domain.Tests/ArchitectureTests.cs`
- `tests/HealthTrack.Application.Tests/HealthTrack.Application.Tests.csproj`
- `tests/HealthTrack.Application.Tests/ArchitectureTests.cs`
- `tests/HealthTrack.IntegrationTests/HealthTrack.IntegrationTests.csproj`
- `tests/HealthTrack.IntegrationTests/HostTests.cs`

`docs/app-flow.md`, `docs/ui-brief.md`, and `docs/schema.md` were read and preserved.
No commits, pushes, or existing-data deletion occurred.
