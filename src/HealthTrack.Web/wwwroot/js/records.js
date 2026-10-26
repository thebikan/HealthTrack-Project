import {
  api,
  all,
  state,
  role,
  clientId,
  section,
  button,
  form,
  field,
  select,
  modal,
  closeModal,
  toast,
  table,
  paged,
  date,
  day,
  localTime,
  utc,
  badge,
  esc,
  empty,
  go,
  confirmAction,
  dateFilter,
  interval,
  metrics,
  csv,
} from "./core.js";
import { chooseClient } from "./plans.js";
const refresh = () => go(state.route);
export async function logs(host, kind) {
  if (!(await chooseClient(host))) return;
  const health = kind === "measurements",
    meal = kind === "meals";
  const path = health
    ? `health/clients/${clientId()}/measurements`
    : `${meal ? "nutrition" : "workouts"}/clients/${clientId()}/logs`;
  const panel = section(
    health
      ? "Measurement history"
      : meal
        ? "Your food diary"
        : "Workout history",
    "A record of the effort you put in. Times are shown in your timezone.",
  );
  host.append(panel);
  const columns = health
    ? [
        ["recordedAtUtc", "Recorded", (v) => esc(date(v, true))],
        ["weightKg", "Weight (kg)"],
        ["heightCm", "Height (cm)"],
        ["bmi", "BMI"],
      ]
    : meal
      ? [
          ["consumedAtUtc", "Consumed", (v) => esc(date(v, true))],
          ["name", "Meal"],
          ["calories", "Calories (kcal)"],
        ]
      : [
          ["completedAtUtc", "Completed", (v) => esc(date(v, true))],
          ["planId", "Plan reference"],
        ];
  const list = document.createElement("div");
  dateFilter(panel, async (q) => {
    list.replaceChildren();
    paged(list, path + "?" + q, columns);
  });
  panel.append(list);
  paged(list, path + "?" + interval(), columns);
  if (role() === "Client")
    panel.querySelector(".section-head").append(
      button(
        "+ " + (health ? "Add measurement" : meal ? "Log meal" : "Log workout"),
        () =>
          modal(
            health
              ? "Record a measurement"
              : meal
                ? "Log a meal"
                : "Log an independent workout",
            form(
              (health
                ? field(
                    "weightKg",
                    "Weight (kg)",
                    "number",
                    "",
                    'min="1" step="0.01"',
                  ) +
                  field(
                    "heightCm",
                    "Height (cm)",
                    "number",
                    state.user.heightCm || "",
                    'min="1" step="0.01"',
                  )
                : meal
                  ? field("name", "Meal name") +
                    field(
                      "calories",
                      "Calories (kcal)",
                      "number",
                      "",
                      'min="0" step="0.1"',
                    )
                  : "") +
                field(
                  "time",
                  "When (local time)",
                  "datetime-local",
                  localTime(),
                ),
              "Save record",
              async (d) => {
                const body = health
                  ? {
                      weightKg: +d.weightKg,
                      heightCm: +d.heightCm,
                      recordedAtUtc: utc(d.time),
                    }
                  : meal
                    ? {
                        name: d.name,
                        calories: +d.calories,
                        consumedAtUtc: utc(d.time),
                      }
                    : { planId: null, completedAtUtc: utc(d.time) };
                await api(
                  health
                    ? "health/measurements"
                    : meal
                      ? "nutrition/logs"
                      : "workouts/logs",
                  "POST",
                  body,
                );
                closeModal();
                toast("Record saved.");
                refresh();
              },
            ),
          ),
        "primary",
      ),
    );
}
export async function goals(host) {
  if (!(await chooseClient(host))) return;
  const panel = section(
    "Goals with purpose",
    "Choose a target, then follow your progress one day at a time.",
  );
  host.append(panel);
  if (role() === "Client")
    panel.querySelector(".section-head").append(
      button(
        "+ Set a goal",
        () =>
          modal(
            "Set a fitness goal",
            form(
              select("type", "Goal type", [
                "WeightReduction",
                "WeightGain",
                "WorkoutConsistency",
                "WorkoutCompletion",
              ]) +
                field(
                  "target",
                  "Target weight (kg), workout count or active days",
                  "number",
                  "",
                  'min="1" step="0.01"',
                ) +
                field("startDate", "Start date", "date", day()) +
                field("targetDate", "Target date", "date", day(30)),
              "Create goal",
              async (d) => {
                await api("goals", "POST", { ...d, target: +d.target });
                closeModal();
                refresh();
                toast("Goal created.");
              },
            ),
          ),
        "primary",
      ),
    );
  paged(
    panel,
    `goals/clients/${clientId()}`,
    [
      ["type", "Goal", (v) => esc(titleGoal(v))],
      ["status", "Status", badge],
      ["target", "Target"],
      ["current", "Current"],
      [
        "progressPercent",
        "Progress",
        (v) =>
          v === null
            ? "No baseline yet"
            : `<progress max="100" value="${Number(v)}" aria-label="Goal progress"></progress> ${Number(v)}%`,
      ],
      ["targetDate", "Target date", (v) => esc(date(v))],
    ],
    (r) =>
      role() === "Client" && r.status === "Active"
        ? [
            button("Edit", () =>
              modal(
                "Update goal",
                form(
                  field(
                    "target",
                    "Target",
                    "number",
                    r.target,
                    'min="1" step="0.01"',
                  ) + field("targetDate", "Target date", "date", r.targetDate),
                  "Save goal",
                  async (d) => {
                    await api(`goals/${r.id}`, "PUT", {
                      target: +d.target,
                      targetDate: d.targetDate,
                      rowVersion: r.rowVersion,
                    });
                    closeModal();
                    refresh();
                  },
                ),
              ),
            ),
            ...["Complete", "Cancel"].map((action) =>
              button(action, () =>
                confirmAction(
                  action + " goal",
                  "This will finish the goal and preserve its current result.",
                  async () => {
                    await api(`goals/${r.id}/finish`, "POST", {
                      rowVersion: r.rowVersion,
                      cancel: action === "Cancel",
                    });
                    refresh();
                  },
                ),
              ),
            ),
          ]
        : [],
  );
}
const titleGoal = (v) =>
  ({
    WeightReduction: "Weight reduction · kg",
    WeightGain: "Weight gain · kg",
    WorkoutConsistency: "Active workout days",
    WorkoutCompletion: "Completed workouts",
  })[v] || v;
export async function appointments(host) {
  const panel = section(
    "Time for a check-in",
    "Book a consultation with your assigned trainer. All times are local.",
  );
  host.append(panel);
  if (role() === "Client")
    panel.querySelector(".section-head").append(
      button(
        "+ Book appointment",
        async () => {
          const assignments = (await all("assignments")).filter(
            (x) => x.status === "Active",
          );
          if (!assignments.length)
            throw Error("You need an assigned trainer before booking.");
          modal(
            "Request an appointment",
            form(
              select(
                "trainerUserId",
                "Assigned trainer",
                assignments.map((x) => [x.trainerUserId, x.trainerUserId]),
              ) + timeFields(),
              "Request appointment",
              async (d) => {
                await api("appointments", "POST", {
                  trainerUserId: d.trainerUserId,
                  ...times(d),
                });
                closeModal();
                refresh();
                toast("Appointment requested.");
              },
            ),
          );
        },
        "primary",
      ),
    );
  const list = document.createElement("div");
  const columns = [
    ["startAtUtc", "Starts", (v) => esc(date(v, true))],
    ["endAtUtc", "Ends", (v) => esc(date(v, true))],
    [
      role() === "Client" ? "trainerUserId" : "clientUserId",
      role() === "Client" ? "Trainer" : "Client",
    ],
    ["status", "Status", badge],
  ];
  const actions = (r) => {
    const a = [
      button("History", () => {
        const p = section("Appointment history");
        paged(p, `appointments/${r.id}/history`, [
          ["previousStatus", "From"],
          ["status", "To", badge],
          ["changedAtUtc", "Changed", (v) => esc(date(v, true))],
        ]);
        modal("Appointment history", p);
      }),
    ];
    if (["Pending", "Approved"].includes(r.status)) {
      a.push(
        button("Reschedule", () =>
          modal(
            "Reschedule appointment",
            form(timeFields(r), "Request new time", async (d) => {
              await api(`appointments/${r.id}/reschedule`, "POST", {
                ...times(d),
                rowVersion: r.rowVersion,
              });
              closeModal();
              refresh();
              toast("Rescheduled. Trainer approval is required again.");
            }),
          ),
        ),
      );
      const statuses =
        role() === "Trainer"
          ? r.status === "Pending"
            ? ["Approved", "Rejected", "Cancelled"]
            : [
                "Cancelled",
                ...(new Date(r.endAtUtc) <= new Date() ? ["Completed"] : []),
              ]
          : ["Cancelled"];
      for (const status of statuses)
        a.push(
          button(
            {
              Approved: "Approve",
              Rejected: "Reject",
              Cancelled: "Cancel",
              Completed: "Complete",
            }[status],
            () =>
              confirmAction(
                status + " appointment",
                "Update this appointment to " + status.toLowerCase() + "?",
                async () => {
                  await api(`appointments/${r.id}/status`, "POST", {
                    status,
                    rowVersion: r.rowVersion,
                  });
                  refresh();
                },
              ),
          ),
        );
    }
    return a;
  };
  dateFilter(
    panel,
    async (q) => {
      list.replaceChildren();
      paged(list, "appointments?" + q, columns, actions);
    },
    day(-7),
    day(60),
  );
  panel.append(list);
  paged(list, "appointments?" + interval(day(-7), day(60)), columns, actions);
}
const timeFields = (r = {}) =>
  field(
    "startAtUtc",
    "Starts (local time)",
    "datetime-local",
    r.startAtUtc ? localTime(r.startAtUtc) : "",
  ) +
  field(
    "endAtUtc",
    "Ends (local time)",
    "datetime-local",
    r.endAtUtc ? localTime(r.endAtUtc) : "",
  );
function times(d) {
  if (d.endAtUtc <= d.startAtUtc)
    throw Error("End time must be after start time.");
  return { startAtUtc: utc(d.startAtUtc), endAtUtc: utc(d.endAtUtc) };
}
export async function notifications(host) {
  const panel = section(
    "Your updates",
    "Stay connected to your plans and appointments.",
  );
  host.append(panel);
  paged(
    panel,
    "notifications",
    [
      ["title", "Update"],
      ["message", "Message"],
      ["dueAtUtc", "Received", (v) => esc(date(v, true))],
      ["readAtUtc", "Status", (v) => badge(v ? "Read" : "Unread")],
    ],
    (r) =>
      !r.readAtUtc
        ? [
            button("Mark read", async () => {
              await api(`notifications/${r.id}/read`, "POST", {});
              refresh();
            }),
          ]
        : [],
  );
  const prefs = await api("notifications/preferences");
  const settings = section(
    "Reminder preferences",
    "Turning a reminder type off also cancels its pending reminders.",
  );
  const f = form(
    Object.entries(prefs)
      .map(
        ([k, v]) =>
          `<label class="check"><input type="checkbox" name="${k}" ${v ? "checked" : ""}>${esc(k.replace("Reminders", " reminders"))}</label>`,
      )
      .join(""),
    "Save preferences",
    async (_, f) => {
      await api(
        "notifications/preferences",
        "PUT",
        Object.fromEntries(
          Object.keys(prefs).map((k) => [k, f.elements[k].checked]),
        ),
      );
      toast("Preferences saved.");
      refresh();
    },
  );
  settings.append(f);
  host.append(settings);
  const schedules = section("Scheduled reminders");
  host.append(schedules);
  if (role() === "Client")
    schedules.querySelector(".section-head").append(
      button("+ New reminder", () => {
        const requestId = crypto.randomUUID();
        modal(
          "Schedule a reminder",
          form(
            select("kind", "Reminder type", ["Workout", "Meal"]) +
              field("dueAtUtc", "Remind me at (local time)", "datetime-local"),
            "Schedule reminder",
            async (d) => {
              await api("notifications/schedules", "POST", {
                kind: d.kind,
                dueAtUtc: utc(d.dueAtUtc),
                requestId,
              });
              closeModal();
              refresh();
            },
          ),
        );
      }),
    );
  paged(
    schedules,
    "notifications/schedules",
    [
      ["kind", "Reminder"],
      ["dueAtUtc", "Due", (v) => esc(date(v, true))],
      [
        "isCancelled",
        "State",
        (v, r) =>
          badge(v ? "Cancelled" : r.deliveredAtUtc ? "Delivered" : "Scheduled"),
      ],
    ],
    (r) =>
      !r.isCancelled && !r.deliveredAtUtc
        ? [
            button("Cancel", () =>
              confirmAction(
                "Cancel reminder",
                "Cancel this pending reminder?",
                async () => {
                  await api(`notifications/schedules/${r.id}/cancel`, "POST", {
                    rowVersion: r.rowVersion,
                  });
                  refresh();
                },
              ),
            ),
          ]
        : [],
  );
}
export async function feedback(host) {
  if (!(await chooseClient(host))) return;
  const panel = section(
    "A little guidance",
    "Notes shared between you and your trainer.",
  );
  host.append(panel);
  if (role() === "Trainer")
    panel.append(
      form(
        '<label>Feedback<textarea name="text" maxlength="2000" required></textarea></label>',
        "Share feedback",
        async (d) => {
          await api("health/feedback", "POST", {
            ...d,
            clientUserId: state.client,
          });
          toast("Feedback shared.");
          refresh();
        },
      ),
    );
  paged(panel, `health/clients/${clientId()}/feedback`, [
    ["createdAtUtc", "Shared", (v) => esc(date(v, true))],
    ["text", "Feedback"],
    ["trainerUserId", "Trainer"],
  ]);
}
