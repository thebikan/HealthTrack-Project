# Decision 0005 — Complete authorized backend work before remote SQL

Date: 2026-10-22. Accepted by user for provisional development. Lecturer approval pending.

The user explicitly authorizes all remaining database-independent backend work,
including goals, templates/versioning, recovery workflow, preferences and CSV.
This supersedes earlier P1 deferral until live SQL acceptance. Remote SQL setup,
connections, migration execution, actual integration verification, system installs,
destructive operations and commits/pushes are not authorized by this task.

## Implemented semantics

- Identity reset tokens use the registered Data Protection provider with a one-hour
  lifetime. Successful reset changes the security stamp, invalidating old tokens
  and cookies. Tokens are passed only to `IPasswordRecoveryDelivery`; no token is
  returned by API or logged. The default adapter is explicitly unconfigured:
  recovery requests return a global 503, rather than pretending to deliver mail.
  Real delivery and durable/protected production keys are external setup tasks.
- Workout templates are trainer-private. Cloning copies exercises into an assigned
  client's draft. Later template edits do not alter that copy. Revisions create
  a distinct draft ID with incremented Version and PreviousPlanId. One successor
  per version prevents ambiguous branches. Previous plans/logs remain unchanged.
  Publishing a revision does not automatically archive its predecessor; explicit
  archive preserves history. Only published plans may enter client-visible archives.
- Drafts can contain up to 366 explicit UTC sessions; published sessions are
  immutable. A session may be completed once and must belong to the logged plan
  and client. Revisions do not copy old dated sessions; schedule the new draft.
  Adherence = completed due scheduled sessions / due scheduled sessions in the
  requested UTC interval. Future sessions and unscheduled logs are excluded;
  no denominator means null. Plan coverage remains a separate named metric.
- Diet edits require the creating trainer and a current assignment. Replacing
  planned meals never changes consumed meal logs. An optional UTC TimeOnly gives
  a daily planned meal time within the plan dates; it is not a local-zone recurrence.
- Goal types: weight reduction/gain, total workout completions, and workout
  consistency (distinct UTC days with logs). Weight baseline is captured from a
  real measurement; missing baseline blocks creation. Targets must follow direction,
  counts are integers, consistency cannot exceed available days. New goals start
  today or later; dates are inclusive, at most ten years. Completed goals snapshot
  the value so backfilled data cannot rewrite completion. Goals are client-owned;
  active assigned trainers may read progress. No medical advice is generated.
- Reminder preferences default enabled. Disabling a kind cancels its pending
  schedules, and workers recheck preferences at delivery. Re-enabling does not
  resurrect cancelled schedules. Transactional appointment updates remain visible
  regardless of reminder preferences.
- CSV is bounded UTF-8 with BOM/CRLF, invariant numbers, UTC dates, quoted cells
  and formula-prefix protection. Client exports require the same ownership/assignment
  checks as progress reads. Admin exports contain aggregates only; no feedback,
  email addresses, account IDs or tokens are exported.
- Appointment overlap checks protect both participants, including a client booking
  different trainers. Pending/Approved intervals reserve time; adjacent intervals
  remain valid. Request, approval and reschedule use the same check under sorted
  client/trainer transaction locks. Cross-process SQL behavior remains unverified.
- Audit metadata now includes registration, successful login/logout, password
  change/reset, initial Admin provisioning, account/assignment changes and appointment
  transitions. Existing appointment history provides per-record lifecycle detail.
  Audit events do not store passwords, tokens or health values.

No claim of live SQL, Windows assessment compatibility, performance benchmarks or
production encryption follows from offline unit/framework tests. See the
[final checklist](../verification/final-sql-server-integration-checklist.md).
