import {
  api,
  state,
  role,
  clientId,
  section,
  button,
  table,
  paged,
  date,
  day,
  badge,
  esc,
  empty,
  go,
  dateFilter,
  interval,
  metrics,
  csv,
} from "./core.js";
import { chooseClient } from "./plans.js";
let charts = [];
export function destroyCharts() {
  charts.forEach((c) => c.destroy());
  charts = [];
}
function chart(host, rows, value, label) {
  const panel = section(label, "Based on your recorded activity.");
  host.append(panel);
  if (!rows.length) {
    panel.append(empty("Add records to see this trend."));
    return;
  }
  if (window.Chart) {
    const wrap = document.createElement("div");
    wrap.className = "chart-wrap";
    const canvas = document.createElement("canvas");
    canvas.setAttribute("role", "img");
    canvas.setAttribute(
      "aria-label",
      label + "; exact values are in the table below.",
    );
    wrap.append(canvas);
    panel.append(wrap);
    charts.push(
      new window.Chart(canvas, {
        type: "line",
        data: {
          labels: rows.map((r) => date(r.dayUtc)),
          datasets: [
            {
              label,
              data: rows.map((r) => r[value]),
              borderColor: "#19786d",
              backgroundColor: "#19786d18",
              fill: true,
              tension: 0.25,
              pointRadius: 3,
              borderWidth: 2,
            },
          ],
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          animation: matchMedia("(prefers-reduced-motion: reduce)").matches
            ? false
            : { duration: 180 },
          plugins: { legend: { display: false } },
          scales: {
            x: { grid: { display: false }, ticks: { maxTicksLimit: 6 } },
            y: {
              beginAtZero: value !== "averageWeightKg",
              grid: { color: "#e8eeeb" },
            },
          },
        },
      }),
    );
  }
  const details = document.createElement("details");
  details.open = !window.Chart;
  details.innerHTML = "<summary>View exact values</summary>";
  details.append(
    table(rows, [
      ["dayUtc", "Date", (v) => esc(date(v))],
      [value, label],
    ]),
  );
  panel.append(details);
}
export async function progress(host) {
  if (!(await chooseClient(host))) return;
  const toolbar = section(
    "Your progress, in perspective",
    "Choose a period to explore the patterns behind your effort.",
  );
  host.append(toolbar);
  const result = document.createElement("div");
  host.append(result);
  async function load(q) {
    destroyCharts();
    const [p, a] = await Promise.all([
      api(`reports/clients/${clientId()}/progress?${q}`),
      api(`reports/clients/${clientId()}/adherence?${q}`),
    ]);
    result.replaceChildren(
      metrics([
        ["Scheduled sessions due", a.scheduledSessionsDue],
        ["Sessions completed", a.completedSessions],
        ["Session adherence", a.adherencePercent, "%"],
        ["Plan coverage", p.currentPlanCoveragePercent, "%"],
      ]),
    );
    const grid = document.createElement("div");
    grid.className = "chart-grid";
    result.append(grid);
    chart(grid, p.weight, "averageWeightKg", "Average weight · kg");
    chart(grid, p.workouts, "completedWorkouts", "Completed workouts");
    chart(grid, p.calories, "recordedCalories", "Recorded calories · kcal");
    result.append(csv(`reports/clients/${clientId()}/progress.csv?${q}`));
  }
  dateFilter(toolbar, load);
  await load(interval());
}
export async function reports(host) {
  const panel = section(
    "Performance overview",
    "A focused view of platform engagement.",
  );
  host.append(panel);
  const result = document.createElement("div");
  host.append(result);
  async function load(q) {
    const admin = role() === "Admin";
    const data = await api(
      admin ? "reports/dashboard/admin?" + q : "reports/trainer?" + q,
    );
    result.replaceChildren(
      metrics(
        Object.entries(data)
          .filter(([, v]) => typeof v === "number")
          .map(([k, v]) => [k.replace(/([A-Z])/g, " $1"), v]),
      ),
    );
    if (data.accounts)
      result.append(
        metrics(Object.entries(data.accounts).map(([k, v]) => [k, v])),
      );
    result.append(csv(`reports/${admin ? "admin" : "trainer"}.csv?${q}`));
  }
  dateFilter(panel, load);
  await load(interval());
}
export async function dashboard(host) {
  const r = role(),
    data = await api(`reports/dashboard/${r.toLowerCase()}?${interval()}`);
  const greeting = document.createElement("div");
  greeting.className = "dashboard-greeting";
  greeting.innerHTML = `<div><span class="eyebrow">${r === "Client" ? "YOUR EVERYDAY, A LITTLE BETTER" : r === "Trainer" ? "SUPPORT THAT MAKES A DIFFERENCE" : "A HEALTHIER PLATFORM"}</span><h1>${r === "Client" ? "Make today a step forward." : r === "Trainer" ? "Great progress starts with support." : "A clear view of your community."}</h1><p>Welcome back, ${esc(state.user.fullName.split(" ")[0])}. ${r === "Client" ? "Let’s make room for your wellbeing." : r === "Trainer" ? "Your clients’ next steps start here." : "Here’s what’s happening across HealthTrack."}</p></div>`;
  greeting.append(
    button(
      r === "Client"
        ? "+ Log a measurement"
        : r === "Trainer"
          ? "View your clients"
          : "Manage assignments",
      () => go(r === "Client" ? "measurements" : "assignments"),
      "primary",
    ),
  );
  host.append(greeting);
  if (r === "Client") {
    host.append(
      metrics([
        ["Workouts · last 30 days", data.workoutsLast30Days],
        ["Active workout plans", data.publishedPlans],
        ["Current diet plans", data.currentDietPlans],
        ["Latest weight", data.latestMeasurement?.weightKg, "kg"],
      ]),
    );
    const grid = document.createElement("div");
    grid.className = "dashboard-grid";
    host.append(grid);
    const p = await api(`reports/clients/${clientId()}/progress?${interval()}`);
    chart(grid, p.weight, "averageWeightKg", "Your weight trend");
    const next = section(
      "Your next check-in",
      "Make time for the support you need.",
    );
    if (data.nextAppointment) {
      const a = data.nextAppointment;
      next.innerHTML += `<div class="appointment-feature"><span class="eyebrow">UPCOMING CONSULTATION</span><h3>${esc(date(a.startAtUtc))}</h3><p>${esc(date(a.startAtUtc, true))}</p>${badge(a.status)}</div>`;
    } else
      next.append(
        empty("No upcoming appointments. Book a check-in with your trainer."),
      );
    next.append(
      button("View appointments →", () => go("appointments"), "text-button"),
    );
    grid.append(next);
    const strip = section(
      "Keep your momentum",
      "Everything you need for a balanced day.",
    );
    const actions = document.createElement("div");
    actions.className = "quick-actions";
    for (const [name, desc, route] of [
      ["Your training", "Follow your assigned programme", "workouts"],
      ["Your nutrition", "Explore your diet schedule", "diet"],
      ["Your goals", "See what you’re working toward", "goals"],
    ]) {
      const b = button("", () => go(route));
      b.innerHTML = `<strong>${name}<span>↗</span></strong><small>${desc}</small>`;
      actions.append(b);
    }
    strip.append(actions);
    host.append(strip);
    const notices = section(
      "Recent updates",
      `${data.unreadNotifications} unread notifications`,
    );
    paged(notices, "notifications?unreadOnly=true", [
      ["title", "Update"],
      ["message", "Message"],
    ]);
    host.append(notices);
  } else if (r === "Trainer") {
    host.append(
      metrics([
        ["Active clients", data.activeClients],
        ["Upcoming appointments", data.upcomingAppointments],
      ]),
    );
    const clients = section(
      "People you’re supporting",
      "Select a client to review their activity and plan their next steps.",
    );
    clients.append(
      table(
        data.clients,
        [
          ["clientUserId", "Client"],
          ["publishedPlans", "Published plans"],
          ["workoutsLast30Days", "Workouts · 30 days"],
        ],
        (c) => [
          button("Open workspace →", () => {
            state.client = c.clientUserId;
            go("client");
          }),
        ],
      ),
    );
    clients.append(
      button(
        "View all assigned clients",
        () => go("assignments"),
        "text-button",
      ),
    );
    host.append(clients);
  } else {
    host.append(
      metrics(
        Object.entries(data.accounts).map(([k, v]) => [
          k.replace(/([A-Z])/g, " $1"),
          v,
        ]),
      ),
    );
    host.append(
      metrics([
        ["Active assignments", data.activeAssignments],
        ["Recorded workouts", data.workouts],
        ["Pending appointments", data.pendingAppointments],
        ["Completed appointments", data.completedAppointments],
      ]),
    );
    const actions = section(
      "Keep your community moving",
      "Manage access, connect trainers, and review platform activity.",
    );
    actions.append(
      button("Manage accounts", () => go("users")),
      button("Trainer assignments", () => go("assignments")),
      button("Explore reports", () => go("reports")),
    );
    host.append(actions);
  }
}
export async function client(host) {
  if (!(await chooseClient(host))) return;
  const panel = section(
    "Client workspace",
    "Plan, review and support this client’s next step.",
  );
  const a = document.createElement("div");
  a.className = "quick-actions";
  for (const [text, route] of [
    ["Workout plans", "workouts"],
    ["Diet plans", "diet"],
    ["Progress & adherence", "progress"],
    ["Measurements", "measurements"],
    ["Goals", "goals"],
    ["Trainer feedback", "feedback"],
  ])
    a.append(button(text + " →", () => go(route)));
  panel.append(a);
  host.append(panel);
  const data = await api(
    `reports/clients/${clientId()}/progress?${interval()}`,
  );
  host.append(
    metrics([
      ["Published plans", data.currentPublishedPlans],
      ["Plans with activity", data.plansWithLogs],
      ["Plan coverage", data.currentPlanCoveragePercent, "%"],
    ]),
  );
}
