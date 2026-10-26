# HealthTrack — App Flow

## Navigation & screens
Public: Landing, Register Client/Trainer, Login, Access Denied, Password Management.
Client: Dashboard, My Workout Plan, Workout Log, Diet Schedule, Meal Log, Health Measurements, Progress & Goals, Appointments, Notifications, Profile.
Trainer: Dashboard, Assigned Clients, Client Detail, Workout Plans/Templates, Diet Plans, Appointments, Feedback, Reports.
Admin: Dashboard, Users, Trainer Assignments, Activity/Reports, Audit Log (P1), Settings.

## Journeys
1. Client registration -> identity verification (if enabled) -> role Client -> Client dashboard -> measurement -> progress chart.
2. Admin provisions Trainer or trainer registers -> Admin assigns client -> Trainer assigned-clients list -> create draft workout -> publish/assign -> Client sees plan -> Client logs workout.
3. Client -> Appointments -> choose assigned trainer, proposed slot -> pending -> Trainer approves/rejects -> Client receives notification -> approved consultation -> complete.
4. Trainer -> client progress -> date-filtered trends -> feedback note -> Client sees feedback.
5. Admin -> Users -> activate/deactivate account or manage assignments -> audit entry -> return to dashboard.

## Navigation policies
- Unauthenticated user: public pages only.
- Client: own record pages only; no Admin or Trainer navigation.
- Trainer: assigned-client records and relevant consultations only.
- Admin: system and assignment management; sensitive-client detail requires explicit additional policy.

## Page states
Every data page: loading, empty data, permission denied (403), missing record (404), invalid input, transient failure with retry, and successful save confirmation. Forms preserve safe inputs on validation errors. Empty states are descriptive and invite authorized next actions.

## Critical edge cases
- Trainer assignment revoked during form editing: submission denied.
- Client tries to modify another person's log using changed URL/body ID: denied.
- Two users book same trainer timeslot simultaneously: at most one wins.
- Attempt to approve already cancelled booking: rejected with current status.
- Missing measurements: don't divide by zero or invent BMI.
- Reminder retried: idempotent; no duplicate delivery.
- Deleted/deactivated user: session/authorization reflects changed status.

## Open decision
How should nonregistered healthcare professionals be represented? First delivery supports trainer consultations and optionally external-professional appointment metadata without adding an unsupported role; obtain instructor approval before extending.
