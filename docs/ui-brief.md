# HealthTrack — UI/UX Brief

The 2026-10-24 user specification authorizes a premium wellness design within the
existing ASP.NET Core MVC stack. The implemented direction is calm evergreen,
warm neutral surfaces, compact data views and purposeful coaching workflows.
See [design research](design/design-research.md) and
[design system](design/design-system.md) for references, exact tokens and rules.

Client navigation covers training, diet, logs, measurements, progress, goals,
appointments, feedback, notifications and profile. Trainer navigation groups
assigned-client workspaces, plan builders/templates, progress, feedback and
performance. Administrator navigation exposes account access, assignments,
aggregate reporting and audit metadata, not private client health detail.

Use the shared Razor layout and reusable browser components. Every visible action
must call the real authorized endpoint or navigate to an implemented screen.
Use explicit loading, empty, error and success states; never fabricate user metrics.
Charts accompany exact-value tables. Preserve CSRF, UTC conversion and concurrency
tokens. Responsive layouts cover 320–1440 px. Native dialog controls, keyboard
focus, labels, reduced motion and readable contrast are required.

Verification and remaining limitations are documented in
[frontend UI review](verification/frontend-ui-review.md). Live SQL journeys remain
separate from fixture-backed browser rendering and offline backend tests.
