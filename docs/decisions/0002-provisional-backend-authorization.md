# Decision 0002 — Provisional backend authorization

- Date: 2026-10-20
- Status: Accepted by user for provisional development only
- Lecturer approval: PENDING
- Commit: None

## Authorization and scope

The user explicitly authorizes complete sequential backend development using
C#, ASP.NET Core MVC on .NET 8, EF Core 8, ASP.NET Core Identity, Microsoft SQL
Server, and a modular monolith with Clean Architecture principles. This
supersedes decision 0001's implementation prohibition and the previous
one-step-only workflow. It does not establish academic approval.

System dependency installation, deletion of existing data, and commits/pushes
require separate approval. Do not substitute another target framework/database
or roll .NET 8 tests forward onto .NET 10 to present them as .NET 8 verification.
Advance sequentially after a phase's acceptance checks pass; report unavailable
dependencies and stop dependent work when required execution is blocked.

## Reconciled conventions

- Canonical Identity role strings: `Client`, `Trainer`, `Admin`. The master
  request's "Administrator" refers to the `Admin` role; it is a display label,
  not a separate privilege or accepted public registration value.
- Test projects: `HealthTrack.Domain.Tests`, `HealthTrack.Application.Tests`,
  and `HealthTrack.IntegrationTests`. Browser E2E testing is deferred to frontend
  integration; backend HTTP journeys remain required.
- Domain has no project dependencies. Application references Domain.
  Infrastructure references Application and Domain. Web composes Application
  and Infrastructure. No reverse dependencies.
- The PRD is the working requirements reference. Required modules precede
  templates, fitness goals, advanced analytics, CSV exports, and plan versioning.
  Security audit records and concurrency-safe booking are mandatory protections.
- Schema additions in the master request are reviewed in the relevant phase;
  optional tables are not added speculatively.

## Unverified coursework requirements

The original coursework brief is absent. Framework acceptance, exact mandatory
feature coverage, healthcare-professional representation, email requirements,
deadline/weighting, group size, report/Viva and submission instructions must be
checked against that brief and lecturer responses. No compliance claim is made.

## Environment evidence

Historical inventory below predates installation. The user subsequently
authorized .NET 8 SDK/runtime installation and offline persistence development;
see [decision 0003](0003-offline-persistence-foundation.md) and
[current verification](../verification/phase-02-offline-persistence.md).

Read-only checks found SDK 10.0.203 and only .NET/ASP.NET Core 10.0.7 runtimes
and reference packs. No .NET 8 SDK/runtime/reference pack is installed in the
inspected dotnet installation. SDK 10 can target `net8.0` with restored reference
packs; this does not change the target or supply the .NET 8 runtime for tests.
See [Microsoft SDK compatibility guidance](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).

Docker CLI 29.5.2 is installed, but the configured daemon socket
`~/.docker/run/docker.sock` does not exist. `docker version` and `docker ps`
cannot connect. Neither `sqlcmd` nor `podman` was found on PATH. No remote SQL
Server endpoint has been supplied. SQL Server availability/connectivity is
therefore unverified; this is not a claim that no remote server exists.

No system dependency was installed, Docker daemon started, or database modified.
The .NET 8 lifecycle, Apple Silicon SQL Server, and Windows Visual Studio risks
in decision 0001 remain applicable.
