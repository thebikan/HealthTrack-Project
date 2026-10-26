# Decision 0003 — Offline SQL Server persistence foundation

Status: Accepted for provisional development, not academic approval.

The user authorizes installing official ARM64 .NET 8 alongside .NET 10, then
continuing entity, EF mapping, and Identity infrastructure work without a live
database. This permits offline schema verification despite step 02's outstanding
live-server acceptance checks. No database server installation/provisioning,
connection, migration application, or existing-data deletion is authorized.

## Model review before migration generation

Use the required core entities in the schema, with Identity kept in Infrastructure.
Profiles have unique user keys. Trainer/client references target the relevant
profile's user key, not just arbitrary Identity users. Profile creation and
role consistency still require application authorization in later steps.

Workout logs referencing a plan use a composite plan/client foreign key, so a
log cannot reference a different client's plan. All historical foreign keys
restrict deletion. Plan dates use SQL `date`; event timestamps use UTC
`datetime2` and reject non-UTC values before saving. Decimal values have explicit
precision and SQL checks. Appointment and assignment updates use rowversion.

Appointment status history and notification schedules are included because
they support required lifecycle/reminder functionality. A nullable unique
notification delivery key supports later retry deduplication. No background
worker, lifecycle service, booking lock, or delivery guarantee is implemented
by defining these tables. Rowversion does not prevent overlapping inserts.

Templates, goals, report requests, plan version history, and other optional
feature tables remain deferred. Future feature migrations may extend the model.

## Configuration and testing

Runtime persistence reads only `ConnectionStrings:DefaultConnection` from
configuration (environment variables or development user-secrets). No connection
string fallback is provided. Missing configuration raises a redacted error when
the DbContext is resolved. An empty MVC host can start without database access;
it is not a database readiness check.

Design-time context creation selects the SQL Server provider without an endpoint,
allowing offline migration generation. It never calls Migrate/EnsureCreated.
Tests inspect EF's SQL Server model, constraints, generated DDL and migrations,
Identity service registration, and local validation. These are not live database
integration tests and do not prove SQL Server execution or concurrency safety.
