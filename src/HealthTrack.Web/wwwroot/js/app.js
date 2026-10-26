import { icon } from "./icons.js";
import { $, state, role, api, button, go, esc, closeModal } from "./core.js";
import { auth, profile, users, assignments, audit } from "./accounts.js";
import { plans } from "./plans.js";
import {
  logs,
  goals,
  appointments,
  notifications,
  feedback,
} from "./records.js";
import {
  dashboard,
  progress,
  reports,
  client,
  destroyCharts,
} from "./analytics.js";
const routes = {
  overview: ["Overview", "⌂", dashboard],
  workouts: ["Workout plans", "↗", (h) => plans(h)],
  history: ["Workout history", "◷", (h) => logs(h, "workouts")],
  diet: ["Diet schedule", "◫", (h) => plans(h, "diet")],
  meals: ["Meal diary", "◴", (h) => logs(h, "meals")],
  measurements: ["Measurements", "⊞", (h) => logs(h, "measurements")],
  progress: ["Progress analytics", "↗", progress],
  goals: ["Fitness goals", "◎", goals],
  appointments: ["Appointments", "▦", appointments],
  notifications: ["Notifications", "♧", notifications],
  profile: ["Profile & security", "○", profile],
  assignments: ["Assigned clients", "♧", assignments],
  client: ["Client workspace", "⊞", client],
  templates: ["Workout templates", "▤", (h) => plans(h, "template")],
  feedback: ["Trainer feedback", "≡", feedback],
  reports: ["Reports & analytics", "▥", reports],
  users: ["User management", "♧", users],
  audit: ["Audit log", "≡", audit],
};
const menus = {
  Client: [
    ["YOUR WELLNESS", ["overview", "workouts", "history", "diet", "meals"]],
    ["YOUR PROGRESS", ["measurements", "progress", "goals", "feedback"]],
    ["STAY CONNECTED", ["appointments", "notifications", "profile"]],
  ],
  Trainer: [
    ["YOUR PRACTICE", ["overview", "assignments", "client", "appointments"]],
    [
      "PLAN & SUPPORT",
      [
        "workouts",
        "templates",
        "diet",
        "progress",
        "measurements",
        "goals",
        "feedback",
      ],
    ],
    ["WORKSPACE", ["reports", "notifications", "profile"]],
  ],
  Admin: [
    ["YOUR PLATFORM", ["overview", "users", "assignments", "reports", "audit"]],
    ["WORKSPACE", ["notifications", "profile"]],
  ],
};
let revision = 0;
function shell() {
  document.body.classList.toggle("public", !state.user);
  const nav = $("nav");
  nav.replaceChildren();
  if (!state.user) {
    $("#breadcrumb").textContent = "✚ HealthTrack.";
    return;
  }
  for (const [heading, items] of menus[role()]) {
    const group = document.createElement("div");
    group.className = "nav-group";
    group.innerHTML = `<span class="nav-label">${heading}</span>`;
    for (const route of items) {
      const a = document.createElement("a");
      a.href = "#" + route;
      a.innerHTML = `<span class="nav-icon" aria-hidden="true">${icon(route)}</span>${esc(route === "assignments" && role() === "Admin" ? "Trainer assignments" : routes[route][0])}`;
      if (state.route === route) {
        a.className = "active";
        a.setAttribute("aria-current", "page");
      }
      group.append(a);
    }
    nav.append(group);
  }
  const initials = state.user.fullName
    .split(" ")
    .map((s) => s[0])
    .slice(0, 2)
    .join("");
  $("#account-shortcut").textContent = initials;
  $("#sidebar-account").innerHTML =
    `<span class="avatar">${esc(initials)}</span><div><strong>${esc(state.user.fullName)}</strong><small>${role() === "Admin" ? "Administrator" : role()} account</small></div>`;
  $("#breadcrumb").textContent =
    "Workspace / " + (routes[state.route]?.[0] || "Overview");
}
async function render() {
  const version = ++revision;
  destroyCharts();
  if ($("#dialog").open) closeModal();
  state.route = location.hash.slice(1) || "overview";
  shell();
  const main = $("main");
  main.replaceChildren();
  if (!state.user) {
    auth(
      main,
      ["register", "recovery", "reset"].includes(state.route)
        ? state.route
        : "login",
    );
    return;
  }
  const allowed = menus[role()].flatMap(([, items]) => items);
  if (!allowed.includes(state.route)) {
    main.innerHTML =
      '<div class="error" role="alert">This page is not available for your role.</div>';
    main.append(button("Back to overview", () => go("overview")));
    return;
  }
  if (state.route !== "overview")
    main.innerHTML = `<div class="page-heading"><div><span class="eyebrow">${role() === "Client" ? "YOUR WELLNESS SPACE" : role() === "Trainer" ? "YOUR PRACTICE" : "PLATFORM MANAGEMENT"}</span><h1>${esc(routes[state.route][0])}</h1></div><span class="page-marker">HEALTHTRACK / ${esc(role().toUpperCase())}</span></div>`;
  const loading = document.createElement("div");
  loading.className = "loading-state";
  loading.innerHTML =
    '<span role="status">Loading your workspace…</span><div class="skeleton"></div><div class="skeleton short"></div>';
  main.append(loading);
  const content = document.createElement("div");
  try {
    await routes[state.route][2](content);
    if (version !== revision) return;
    loading.remove();
    main.append(content);
  } catch (e) {
    if (version !== revision) return;
    loading.remove();
    const error = document.createElement("div");
    error.className = "error";
    error.setAttribute("role", "alert");
    error.textContent = e.message;
    main.append(error, button("Try again", render));
  }
  document.title = (routes[state.route]?.[0] || "Welcome") + " · HealthTrack";
}
$("#dialog-close").onclick = closeModal;
$("#dialog").addEventListener("close", () => {});
function collapse(closed) {
  document.body.classList.toggle("nav-collapsed", closed);
  $("#menu-toggle").setAttribute("aria-expanded", String(!closed));
  $("#sidebar").inert = closed;
}
$("#menu-toggle").onclick = () =>
  collapse(!document.body.classList.contains("nav-collapsed"));
matchMedia("(max-width: 799px)").addEventListener("change", (e) =>
  collapse(e.matches),
);
document.addEventListener("keydown", (e) => {
  if (e.key === "Escape" && !$("#dialog").open && innerWidth < 800) {
    collapse(true);
    $("#menu-toggle").focus();
  }
});
$("#notification-shortcut").innerHTML = icon("notifications");
$("#account-shortcut").onclick = () => go("profile");
$("#notification-shortcut").onclick = () => go("notifications");
$("#today").textContent = new Date().toLocaleDateString(undefined, {
  weekday: "short",
  month: "short",
  day: "numeric",
});
window.addEventListener("hashchange", () => {
  if (innerWidth < 800) {
    collapse(true);
  }
  render();
});
window.addEventListener("session-expired", () => go("login"));
if (innerWidth < 800) {
  collapse(true);
}
try {
  state.user = await api("account/me");
} catch (e) {
  if (e.status !== 401)
    console.warn("Account service unavailable; sign-in is available.");
}
await render();
