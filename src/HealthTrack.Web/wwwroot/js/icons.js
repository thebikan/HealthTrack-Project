// Small original stroke icons; decorative SVGs are hidden from assistive technology.
const paths = {
  overview: '<path d="m3 10 9-7 9 7v11h-7v-7h-4v7H3Z"/>',
  workouts: '<path d="M3 8v8m3-11v14m12-14v14m3-11v8M6 12h12"/>',
  history: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l4 2"/>',
  diet: '<path d="M5 3v7m3-7v7m-6-7v7h6M5 10v11M18 3v18m0-18c-5 3-5 9 0 9"/>',
  meals: '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="4"/>',
  measurements:
    '<rect x="4" y="3" width="16" height="18" rx="3"/><path d="M8 7h8m-4 0 2 3"/>',
  progress: '<path d="M3 3v18h18M6 16l4-5 4 2 6-8"/>',
  goals:
    '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1"/>',
  appointments:
    '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v5m10-5v5M3 11h18m-13 4h2m4 0h2"/>',
  notifications: '<path d="M5 16h14l-2-4V8a5 5 0 0 0-10 0v4Zm5 4h4"/>',
  profile:
    '<circle cx="12" cy="8" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/>',
  assignments:
    '<circle cx="8" cy="7" r="3"/><path d="M2 20v-3a6 6 0 0 1 12 0v3m2-16a3 3 0 0 1 0 6m1 3a5 5 0 0 1 5 5v2"/>',
  templates:
    '<rect x="7" y="3" width="14" height="15" rx="2"/><path d="M3 7v14h14M11 8h6m-6 5h4"/>',
  feedback: '<path d="M3 3h18v14H9l-6 4Zm4 5h10M7 12h7"/>',
  audit: '<path d="M5 3h10l4 4v14H5Zm10 0v5h4M8 12h8m-8 4h6"/>',
};
export function icon(name) {
  const key =
    { client: "profile", users: "assignments", reports: "progress" }[name] ||
    name;
  return `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[key] || paths.overview}</svg>`;
}
