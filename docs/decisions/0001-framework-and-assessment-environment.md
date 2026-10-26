# Decision 0001 — MVC framework and assessment environment

- Date: 2026-10-20 (Asia/Colombo)
- Status: Pending external confirmation; not an approved technology decision
- Scope: Implementation-plan step 00 only
- Commit: None; awaiting user review and approval

**Historical record:** [decision 0002](0002-provisional-backend-authorization.md)
supersedes the implementation prohibition below following explicit user
authorization. Lecturer approval and assessment compatibility remain pending;
this record is not evidence of approval.

## Context and repository evidence

Read `CLAUDE.md`, `README.md`, and all six planning documents under `docs/`.
The repository has a tracked README and previously untracked instructions and
planning documents. Inspection found no solution, application source, project
files, package references, migrations, or test projects.

`CLAUDE.md` requires lecturer confirmation of MVC 5 versus ASP.NET Core MVC
before scaffolding. The TRD proposes ASP.NET Core MVC with .NET 8 but explicitly
does not approve it. The statement "VS 2017 or newer" does not identify the
actual assessment installation or prove compatibility with that proposal.

## Confirmation record

On 2026-10-20, the user proposed C#, ASP.NET Core MVC / .NET 8, EF Core 8,
ASP.NET Core Identity, and Microsoft SQL Server. Their planned development
environment is macOS with VS Code and the .NET SDK, with subsequent Windows
Visual Studio verification. They explicitly stated that lecturer approval,
assessment OS/software versions, and SQL Server availability are still pending.
This records a preference, not approval. Do not begin implementation until the
framework requirement is resolved.

These entries must be supplied from the lecturer or assessment environment;
they must not be inferred from the developer's machine.

| Required information | Confirmed value / evidence |
|---|---|
| Lecturer approval source and date | Pending |
| Accepted MVC family: MVC 5 or ASP.NET Core MVC | Pending |
| Approved target .NET Framework or .NET version | Pending |
| Assessment operating system and architecture | Pending |
| Visual Studio edition and exact version | Pending |
| Installed web-development workload and relevant components | Pending |
| Available SDK/runtime or .NET Framework targeting pack | Pending |
| SQL Server/LocalDB edition, version, and availability | Pending |
| Assessment setup permissions and package-restore/network constraints | Pending |

After confirmation, check the selected framework's official compatibility
requirements against the recorded Visual Studio and runtime/targeting-pack
versions. Record supporting links and the outcome here, and update the TRD
to distinguish the approved stack from any remaining proposals.

## Compatibility risks and official references

Reviewed on 2026-10-20:

- .NET 8 targeting requires Visual Studio 2022 17.8 or later; Visual Studio
  2017 does not meet that requirement. The exact SDK feature band must also
  match the installed Visual Studio/MSBuild support range. A minimum version
  is not a guarantee that an old Visual Studio release remains supported.
  See [targeting requirements](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/version-requirements)
  and [SDK/Visual Studio compatibility](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).
- .NET 8 reaches end of support on 2026-11-10. Confirm whether the assessment
  and maintenance period extend beyond that date, and obtain an explicit
  target-version decision rather than silently upgrading.
  See [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- LocalDB is a Windows development option, not a macOS database installation.
  See [LocalDB installation](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb).
- Microsoft supports SQL Server Linux containers on Intel/AMD x86-64 Linux
  hosts; Rosetta, Prism, and QEMU emulation are not tested or supported.
  For this ARM64 Mac, establish access to a supported SQL Server host rather
  than assuming an emulated local container is assessment-ready.
  See [SQL Server container requirements](https://learn.microsoft.com/en-us/sql/linux/containers/deploy).
- A successful macOS build would not establish Windows Visual Studio,
  database authentication, or clean-assessment-machine compatibility.
  These remain separate verification tasks after approval.

## Exact questions for the lecturer

1. Does “ASP.NET MVC” permit ASP.NET Core MVC with C# and Razor views, or is
   ASP.NET MVC 5 on .NET Framework mandatory?
2. If Core MVC is accepted, may I use .NET 8, EF Core 8, ASP.NET Core Identity,
   and Microsoft SQL Server? Given .NET 8's 2026-11-10 end-of-support date,
   is that target acceptable through assessment, or is another supported
   version required? Please confirm the approved target in writing.
3. May I develop on macOS with VS Code and demonstrate/build the submission
   in Windows Visual Studio, or must development itself use Visual Studio?
4. What exact Windows edition/version, CPU architecture, Visual Studio
   edition/version, web workload, and .NET SDK/runtime will be available?
   Can missing components be installed or updated before assessment?
5. Which SQL Server edition/version and instance will be available? Is
   LocalDB accepted? Which authentication method and database creation/
   migration permissions will be provided? May development use a remote
   SQL Server instance?
6. Must assessment work offline? Are NuGet restore/network access allowed,
   and will you expect Visual Studio launch, IIS Express, IIS, or another
   launch method? What submission/setup artifacts are required?

Record the response date and source without storing private correspondence or
credentials in the repository.

## Environment checks

Perform inventory checks now; installation, project generation, restore/build,
and database changes are deferred until the framework requirement is resolved.

| Environment | Check and evidence to record |
|---|---|
| macOS | Run `sw_vers`, `uname -m`, `dotnet --info`, `dotnet --list-sdks`, and `dotnet --list-runtimes`; compare OS/architecture with the approved SDK's supported platforms. |
| VS Code | Use Help > About and Extensions to record the editor and C# tooling versions; `code --version` and `code --list-extensions --show-versions` are alternatives if its CLI is installed. |
| Windows assessment | Use `winver` and Settings > System > About for OS/architecture; Visual Studio Help > About for exact edition/version; Visual Studio Installer for ASP.NET and web development workload and SDK components. Run the three `dotnet` inventory commands above on that machine too. |
| SDK alignment | Compare the actual SDK feature band with Microsoft's Visual Studio compatibility table. Once approved, agree a common SDK for later project configuration; no `global.json` is created in step 00. |
| SQL Server | Obtain host/instance, edition/version, authentication, TLS certificate requirements, network/VPN access, and permissions from the administrator. With authorized access, execute read-only `SELECT SERVERPROPERTY('ProductVersion'), SERVERPROPERTY('Edition');` to verify the server. Do not record credentials. |
| Windows LocalDB, if selected | Run `sqllocaldb versions` and `sqllocaldb info` to inventory installation and existing instances; do not assume it is installed with Visual Studio. |
| Later acceptance | After approval and the relevant implementation steps, verify clean Windows restore/build/tests, Visual Studio launch, and SQL Server connection/migration using the assessment configuration. No such execution is claimed in step 00. |

Local inventory executed on 2026-10-20: `dotnet --info` reported macOS 27.0,
RID `osx-arm64`, SDK **10.0.203**, and only **10.0.7** Microsoft.NETCore.App
and Microsoft.AspNetCore.App runtimes. `uname -m` returned `arm64`.
No .NET 8 SDK/runtime was listed by this installation, and no `global.json`
was found. A newer SDK can target earlier frameworks, but this inventory does
not demonstrate the proposed .NET 8 runtime or assessment setup is ready.
VS Code, Windows, database connectivity, and macOS 27 SDK support have not been
verified. No tools, runtimes, or databases were installed or changed.

## Architecture constraints carried forward

Retain the TRD's modular-monolith MVC architecture: Domain, Application,
Infrastructure, and Web, with controllers delegating to application services.
The intended dependency direction is Application toward Domain, Infrastructure
toward application contracts/domain, and Web composing the application and
infrastructure. Domain must not depend on Web or persistence implementations.
No projects or references are created in step 00.

Framework-specific Identity, ORM, testing, and configuration choices remain
provisional. If MVC 5 is required, revise the Core-only proposals before
scaffolding; do not mix packages from the two framework families.

## Acceptance and verification

| Criterion | Result |
|---|---|
| Inspect the existing repository and required documents | Passed; documentation-only repository inspected |
| Record lecturer-approved framework and target version | Pending external confirmation |
| Record assessment environment and verify compatibility | Pending external confirmation |
| Avoid incompatible scaffolding | Passed; no solution or application/test projects created |
| Preserve step 00 scope | Documentation changes only; steps 01–25 remain unstarted |

Verification for this documentation-only step consists of checking local
Markdown links, absence of application scaffolding, and patch whitespace.
There is no application test suite to execute. Runtime, build, database, and
Visual Studio compatibility have not been tested; those claims require an
approved stack and, in later steps, executable projects.

Executed on 2026-10-20:

- Python documentation checks: PASS; both local Markdown links resolve.
- Python scope check: PASS; no solution/project files or `src/` or `tests/` directories.
- Python whitespace check: PASS for all three changed documents, including
  untracked files.
- `git diff --check`: PASS; this command alone does not check untracked documents.

Step 00 must remain incomplete until the pending confirmation and compatibility
checks above are resolved. Do not start step 01 as part of this task.
