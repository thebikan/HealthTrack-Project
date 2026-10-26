# HealthTrack design system

Implemented 2026-10-24. MVC Razor shell, native HTML controls, modular browser
JavaScript, local Chart.js 4.4.8. No frontend build pipeline or framework migration.

## Tokens
| Purpose | Value |
| --- | --- |
| Background | `#F6F8F6` |
| Surface | `#FFFFFF` |
| Primary text | `#182D29` |
| Secondary text | `#5D716B` |
| Accent | `#19786D` |
| Deep accent | `#174D45` |
| Accent surface | `#E9F2ED` |
| Border | `#DFE7E2` |
| Error | `#B42335` |

Type: Inter when installed, Segoe UI, system-ui. Page titles 28–29 px; section
headings 17 px; body 13–14 px; table text 12 px. Authentication has a larger
editorial heading. Spacing primarily 4/8/12/16/20/24/32/48 px. Controls have
6–7 px radii, panels 10 px, dialogs 14 px. Borders provide separation; shadows
are reserved for overlays. Transitions 160–180 ms, removed for reduced motion.

## Layout and reusable components
`Views/Shared/_Layout.cshtml` owns navigation, account controls, dialog and toast
regions. `Views/Home/Index.cshtml` supplies the main landmark. `js/core.js` owns
buttons, field/select markup, forms, dialogs, confirmation, status badges, table,
pagination, metrics, date filters, loading/error helpers and API transport.
Feature modules: accounts, plans, records, analytics; `app.js` owns route dispatch.

Navigation groups follow user tasks and canonical Client/Trainer/Admin roles.
Desktop sidebar is 240 px (210 px at narrower desktop), collapsible. Mobile
navigation overlays below a sticky header; every destination remains available.
Content widths are bounded; tables scroll in keyboard-focusable containers.
Charts have dedicated responsive containers and exact-value tables.

Forms use native date/time controls. Local datetime inputs convert to UTC for API
requests. Diet recurring time explicitly uses UTC because that is the backend
contract. Concurrency-sensitive mutations send the server-provided rowVersion.
No account credentials, health data, or access tokens are persisted in localStorage.
Only in-memory page/client context and URL fragment navigation are used.

## Accessibility practices
Visible focus outlines; semantic landmarks/headings/tables; explicit form labels;
native dialog focus containment and Escape; return focus after close; live toast
and loading messages; focusable validation summary; text status labels; reduced
motion; chart data tables. This is an implementation target, not WCAG certification.

## Data and licensing
Charts only consume API data. Synthetic fixture values exist solely in browser
review tests. Chart.js is vendored with its MIT license. The CSS illustration and
interface styling are original; no reference-site image assets were copied.

## Authentication visual revision — 2026-10-24
The user's supplied botanical login reference supersedes the earlier editorial
split-panel authentication design. Public pages now use an original generated
knitted-sprout/tabletop background, warm cream (#FFF3E9) form surface, natural brown
text and green (#478D18) primary buttons. The authenticated workspace remains
unchanged. Registration/recovery share the responsive treatment. Password reveal
is implemented; Google authentication is omitted because no provider is configured.
The image is stored locally at `wwwroot/images/wellness-sprout.png`.
