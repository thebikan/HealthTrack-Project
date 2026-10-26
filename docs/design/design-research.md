# HealthTrack design research — 2026-10-24

## Method and limits
Public page content was retrieved using the browsing tool before implementation.
These are nine accessible reference pages, not nine independently tested products.
Research observations below concern exposed structure/content and documented patterns;
responsive behavior and accessibility of the reference products were not independently tested.
No paid libraries, copied design assets, or React components are included.

| Reference | Observations and application |
| --- | --- |
| [shadcn/ui](https://ui.shadcn.com/) | Grouped navigation, compact form controls, labeled targets and contextual actions. Keep controls visually related without making every section a floating card. |
| [Sidebar](https://ui.shadcn.com/docs/components/base/sidebar) | Composable navigation architecture. Translate to role groups, active-page semantics and a collapsible native shell. |
| [Data table](https://ui.shadcn.com/docs/components/base/data-table) | Separate data presentation from filtering and pagination. HealthTrack uses semantic tables, bounded page requests and explicit row actions. |
| [Dialog](https://ui.shadcn.com/docs/components/base/dialog) | Task-focused overlays. Use native dialog modality, labeled titles, explicit close controls and return focus. |
| [Chart](https://ui.shadcn.com/docs/components/base/chart) | Contextual chart presentation rather than isolated graphics. Show units, date filters and exact-value summaries. Implement with Chart.js, not React/Recharts. |
| [Field](https://ui.shadcn.com/docs/components/base/field) | Labels, descriptions and validation belong together. Use visible labels, optional-field markers and a focusable error summary. |
| [Empty](https://ui.shadcn.com/docs/components/base/empty) | Empty content is an intentional state with an explanation. Do not fabricate example records in production. |
| [Mobbin public page](https://mobbin.com/) | Organizes references around real screens and flows, including fitness/onboarding examples. Adopt flow-first review; authenticated screen collections were not inspected. |
| [Dribbble fitness dashboard gallery](https://dribbble.com/search/fitness-dashboard) | Fitness work is discoverable by dashboard, chart and activity categories. Used as a category-level direction only; gallery thumbnails do not establish functional usability. |

All three requested Pinterest searches were attempted but inaccessible through the
browsing tool: [healthcare](https://www.pinterest.com/search/pins/?q=healthcare%20dashboard%20ui%20design),
[fitness](https://www.pinterest.com/search/pins/?q=fitness%20dashboard%20web%20design),
[SaaS](https://www.pinterest.com/search/pins/?q=modern%20saas%20dashboard%20ui).
No observations about their unseen designs are claimed.

## Selected directions
1. **Calm editorial wellness:** evergreen, warm pale surfaces, restrained typography,
   original CSS orbital illustration on authentication pages. No stock medical imagery.
2. **Compact clinical workspace:** grouped sidebar, narrow header, explicit scope,
   shared metric strip and legible tables with units.
3. **Action-oriented coaching:** client context selector, editable exercise/meal rows,
   contextual publish/revision/session actions rather than a generic JSON editor.
4. **Explainable progress:** actual dated series, subdued single-accent charts,
   precise values in accessible tables and honest empty states.

Typography is system UI/Segoe UI with Inter if already installed: no remote font
tracking or network requirement. Charts use the existing backend's definitions;
coverage and adherence remain separate. Statuses use both words and color.

## Initial audit
The previous UI had one Razor view, a monolithic script, raw JSON nested cells,
no shared layout, no working plan builders, no appointment mutation controls,
no administrator mutation controls and no chart rendering. Pagination and date
selection were absent. Preserve API authorization and CSRF while replacing these
presentation and workflow gaps.
