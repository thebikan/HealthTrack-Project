# HealthTrack — Product Requirements (PRD)

## Source and status
Coursework case study: CS6004ES Coursework 2 (2025/26), HealthTrack Personal Health & Fitness Management System. **Required** versus **recommended enhancement** are explicitly distinguished below. Coursework specifies group up to four, individual report and Viva. Verify contradictory deadline/weighting with lecturer.

This PRD is the working implementation reference. The original coursework brief
is not present in the repository, so academic scope, submission requirements,
deadline/weighting, group size, and framework acceptance are unverified.
See [decision 0002](decisions/0002-provisional-backend-authorization.md).
`Admin` is the canonical role value; "Administrator" is its display label.

## Pitch / problem
HealthTrack lets clients track workouts, meals, health measurements, and consultations, while trainers organize personalized fitness programs and administrators manage the system. Today this information is fragmented, making progress and appointments harder to track.

## Users
- Client: manages only own measurements and activity, follows assigned plans, requests appointments.
- Trainer: views assigned clients, assigns plans, gives feedback, approves/manages relevant consultations.
- Admin: manages accounts, assignments, and aggregate system activity, with limited access to individual health data by default.

## Required v1 — preserve all coursework functionality
1. Secure registration/login for three roles, with safe Admin provisioning and role-based authorization.
2. Role dashboards (Client: health, plans, next appointments; Trainer: assigned-client progress and feedback; Admin: usage, activity, assignments).
3. Trainer workout/diet plan assignment; client workout, meal and measurement logging.
4. Client consultation booking, trainer approval/update/cancel; healthcare-professional representation needs a scope decision.
5. Weight/BMI, calorie and fitness trends; trainer/admin performance/engagement reports.
6. Workout, meal, appointment reminders; protect sensitive health data with authentication, encryption and validation.

## v1 acceptance journeys
- Admin assigns Trainer T to Client C; T can work with C but not unrelated Client D.
- T publishes plan for C; C views plan, logs workout, sees progress chart.
- C requests appointment with T; T approves; conflicting reservations are rejected.
- Admin views system-level report; sensitive client data is not unnecessarily exposed.

## Recommended extensions (only after P0)
Implementation timing update (2026-10-22): the user explicitly authorizes the P1
backend features before live SQL verification; see decision 0005. This changes
development order, not academic acceptance or SQL verification requirements.

P1: fitness goals, reusable plan templates/versioning, audit trail, trainer feedback notes, CSV export. P2: rule-based adherence summaries. P3, out of scope unless explicitly approved: wearable sync, AI medical/diet advice, real-time chat, payments, mobile app.

## User stories
- As a Client, I can see my next workout and upcoming appointment without searching several pages.
- As a Client, I can log meals and measurements and see dated progress.
- As a Trainer, I can assign an appropriate workout to one of my current clients.
- As an Admin, I can monitor engagement and manage trainer assignments without browsing private notes unnecessarily.

## Success measure and evidence
Acceptance: all required end-to-end journeys and critical authorization tests pass on a clean assessment setup; collect screenshots, test outcomes and Viva demo script. An 88%+ grade is an aspiration, not guaranteed by extra features.

## Out of scope / open decisions
- Do not provide medical diagnoses or prescriptive AI treatment.
- Confirm MVC version, Visual Studio environment, consultation model for external healthcare professionals, current submission instructions, and email requirement with lecturer.
