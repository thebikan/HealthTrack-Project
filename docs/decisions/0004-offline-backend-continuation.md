# Decision 0004 — Sequential backend implementation without a live database

Date: 2026-10-21. Status: User-authorized provisional work; lecturer approval pending.

The user explicitly requested continuation from existing code through mandatory
backend modules, available tests and gap analysis. This extends decision 0003's
offline scope beyond entities. SQL-dependent acceptance stays incomplete; do not
connect/apply migrations/provision a server without separate approval. Do not
install further system dependencies or commit/push automatically.

## Behavior decisions

- Canonical roles remain Client, Trainer, Admin. Admin has aggregate/account
  privileges, not general access to personal health records. Active assignments
  are checked in use cases; role claims alone are insufficient.
- Identity role/Admin setup is an explicit development command. Never promote
  an existing public account from a supplied seed email. No startup migration
  or automatic seeding. Public registration requires previously provisioned roles.
- Account disabling invalidates security stamps and revokes assignments; enabling
  does not silently recreate assignments. Account deletion is not implemented.
- Pending and approved appointments reserve trainer time; adjacent intervals are
  allowed. Rescheduling requires renewed approval. Writes use ordered transaction
  application locks and serializable isolation; real concurrency is unverified.
- Published workout plans are immutable. Archiving preserves records; actual
  version-history/template features remain deferred. Diet assignment creates a
  new plan; destructive historical editing is not introduced.
- Fitness progress reports label distinct logged/current published plans as
  coverage, not adherence. Expected session frequency is absent from requirements.
- Workout/meal reminders are client-scheduled one-time UTC activities, not
  inferred recurrence. Approval schedules reminders for both appointment parties
  24 hours before start, or immediately if within that window. Rescheduling or
  cancellation cancels outstanding reminders. Delivery is in-app only.
- Reminder attempts/backoff are committed before delivery; notification and
  delivery state are saved atomically, keyed uniquely. Worker defaults disabled,
  handles failed cycles with fresh scopes and sanitized logging. Live crash/race
  behavior remains a required test, not an offline guarantee.
- User restored SDK 8.0.426 while explicitly preserving `latestFeature` and
  `allowPrerelease: false`. Project target stays net8.0; no .NET 10 migration.

Password reset/email confirmation, external healthcare-professional representation,
original coursework compliance and assessment compatibility remain unresolved.
No delivery adapter, verification evidence, or academic approval is fabricated.
See [phase report](../verification/phase-03-backend-modules.md).
