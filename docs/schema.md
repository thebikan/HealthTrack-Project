# HealthTrack — SQL Server Schema & Permissions

This is the working logical model. Core SQL Server mappings and an initial
offline-generated migration now exist; they have not been applied to a database.
See [model review](decisions/0003-offline-persistence-foundation.md).
Identity IDs are strings bounded to 128 characters. The migration and EF model
are the precise column definitions; application services now enforce ownership
and active assignments. SQL execution remains unverified.

## Core tables
| Table | Primary fields (illustrative) | Relationships and authorization |
|---|---|---|
| AspNetUsers | Id string PK, FullName, Email, IsActive, CreatedAtUtc | Identity-managed; self profile or Admin account management |
| AspNetRoles / AspNetUserRoles | Identity fields | Admin provisioned securely; never selectable publicly |
| ClientProfiles | Id int PK, UserId string unique FK, HeightCm decimal? | Client owns; assigned Trainer scoped access |
| TrainerProfiles | Id int PK, UserId string unique FK, Specialization | Trainer owns; Admin manages assignments |
| TrainerClientAssignments | Id PK, TrainerUserId FK, ClientUserId FK, Status, AssignedAtUtc | Admin writes; read scoped to participants/Admin |
| WorkoutPlans | Id PK, ClientUserId FK, TrainerUserId FK, Title, Status, Version, StartDate, EndDate | Assigned Trainer creates/updates; Client reads |
| WorkoutPlanExercises | Id PK, WorkoutPlanId FK, ExerciseName, Sets, Reps, DurationMinutes | Inherit parent authorization |
| WorkoutLogs | Id PK, ClientUserId FK, WorkoutPlanId FK, CompletedAtUtc | Client writes; assigned Trainer reads |
| DietPlans | Id PK, ClientUserId FK, TrainerUserId FK, Title, StartDate, EndDate | Assigned Trainer writes; Client reads |
| DietPlanMeals | Id PK, DietPlanId FK, MealName, MealType, Calories | Inherit plan authorization |
| MealLogs | Id PK, ClientUserId FK, MealName, Calories, ConsumedAtUtc | Client writes; assigned Trainer reads if needed |
| HealthMeasurements | Id PK, ClientUserId FK, WeightKg, HeightCm, RecordedAtUtc | Client writes; assigned Trainer reads |
| Appointments | Id PK, ClientUserId FK, TrainerUserId FK, StartAtUtc, EndAtUtc, Status, RowVersion | Client requests; assigned Trainer updates status |
| Notifications | Id PK, UserId FK, Title, Message, DueAtUtc, ReadAtUtc | Recipient only |
| TrainerFeedback | Id PK, TrainerUserId FK, ClientUserId FK, Text, CreatedAtUtc | Assigned Trainer creates; Client reads |
| AppointmentStatusHistory | Id PK, AppointmentId FK, PreviousStatus, Status, ChangedByUserId FK, ChangedAtUtc | Authorized appointment participants only |
| NotificationSchedules | Id PK, UserId FK, Kind, DueAtUtc, DeliveredAtUtc, Attempts, NextAttemptAtUtc, IsCancelled, AppointmentId nullable FK, DeliveryKey unique, RowVersion | Recipient only; worker delivers approved due activities |
| AuditLogs | Id PK, ActorUserId FK, SubjectUserId FK, Action, CreatedAtUtc | Admin operational metadata only; no health values |
| WorkoutTemplates | Id PK, TrainerUserId FK, Title, IsArchived | Owner Trainer only |
| WorkoutTemplateExercises | Id PK, WorkoutTemplateId FK, ExerciseName, Sets, Reps, DurationMinutes | Parent template ownership |
| WorkoutSessions | Id PK, WorkoutPlanId/ClientUserId composite FK, ScheduledAtUtc | Draft author schedules; Client completes owned session once |
| FitnessGoals | Id PK, ClientUserId FK, Type, Status, Baseline, Target, StartDate, TargetDate, CreatedAtUtc, CompletedAtUtc, CompletedValue, RowVersion | Client writes; current assigned Trainer reads |
| NotificationPreferences | Id PK, UserId unique FK, WorkoutReminders, MealReminders, AppointmentReminders | Recipient only |

## Optional P1 tables
ReportRequests and separate external ReminderDeliveries remain unused/out of scope.
Templates and goals are now implemented. Workout versions use separate WorkoutPlan
rows with PreviousPlanId and Version, rather than a separate version table.
PreviousPlanId uses a unique filtered index and composite parent/client FK.
WorkoutLog.SessionId is nullable, unique when populated, and references the
session/plan/client composite key; a CHECK requires PlanId when SessionId is present.
DietPlanMeal.PlannedTimeUtc is an optional UTC daily time within its plan dates.
Completed goals snapshot CompletedValue; later backfills do not rewrite completion.
AppointmentStatusHistory and NotificationSchedules are included in the core
persistence foundation for required appointment transitions and reminders.
Notifications have a nullable unique DeliveryKey and schedules have a required
unique DeliveryKey. The worker persists attempts/backoff before delivery and saves
the notification plus delivered state in one transaction. Guarantees require live
SQL verification; unit doubles do not establish durability or isolation.
`AddAccountAudit` adds operational audit metadata. `AddReminderDeliveryState`
adds appointment linkage, cancellation, backoff and a due-work index. There are
28 mapped tables including Identity. Seven migrations remain unapplied. The
original three migrations are preserved; four new migrations add workout library/
sessions, goals/preferences/meal times, goal completion snapshots and integrity checks.

## Relationships
Users 1:1 ClientProfile or TrainerProfile. Trainer and Client many-to-many through assignments (with status). Client 1:N plans/logs/measurements/appointments. Plan 1:N exercises or planned meals. Workout log belongs to a client and optionally a plan.

## Security and ownership policy
Every sensitive table must have a direct owner user FK or reach owner through an authorized parent. Identity role ≠ per-record permission. Client can read/write own logs and measurements. Trainer may read client data only when active assignment exists and may edit only training-plan/feedback records for authorized clients. Admin sees system aggregates and operational metadata by default, not arbitrary personal health notes.

## Constraints & indexes
- FK and unique constraints on role-specific profile UserId; consider unique active assignment strategy.
- `CHECK (WeightKg > 0)`, `CHECK (HeightCm > 0)` where non-null, `CHECK (Calories >= 0)`, `CHECK (EndAtUtc > StartAtUtc)`.
- Index `(ClientUserId, RecordedAtUtc)` measurements; `(ClientUserId, CompletedAtUtc)` workout logs; `(TrainerUserId, StartAtUtc, Status)` appointments; `(UserId, ReadAtUtc)` notifications.
- Ensure a concurrency-safe SQL Server booking strategy. SQL Server does not natively prevent arbitrary overlapping intervals with a simple unique index. Use serialized trainer-slot writes (e.g. per-trainer lock/transaction), then query for overlaps inside transaction; test concurrent requests.
- Appointments `RowVersion` for update conflicts; it alone **does not** prevent new overlapping insertions.
- Use UTC `datetime2`, `decimal(6,2)` for kg/cm where appropriate, and SQL Server `rowversion` for concurrency token.
- All mapped foreign keys restrict deletion, including Identity relationships.
  Account deactivation is the implemented lifecycle; any hard-deletion workflow
  requires explicit retention and dependent-record handling.
- Profile UserId values are alternate keys; client/trainer foreign keys reference
  the relevant profile. WorkoutLog's plan/client composite FK prevents linking a
  log to a different client's plan. An active-assignment filtered unique index
  prevents duplicate active pairs; access checks still require application services.

## Cross-database caveat
The uploaded PDF's sample uses Supabase/Postgres row-level security. HealthTrack's proposed SQL Server + ASP.NET Core implementation uses server-side authorization and optional database permissions; do **not** paste Supabase RLS SQL into SQL Server.
