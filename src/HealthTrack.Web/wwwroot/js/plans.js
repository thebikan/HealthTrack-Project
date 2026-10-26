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
} from "./core.js";
const refresh = () => go(state.route);
export async function chooseClient(host) {
  if (role() === "Client") return true;
  const assignments = (await all("assignments")).filter(
    (x) => x.status === "Active",
  );
  const ids = [...new Set(assignments.map((x) => x.clientUserId))];
  if (!ids.length) {
    host.append(
      empty(
        "No active clients are assigned to you yet. An administrator can create an assignment.",
      ),
    );
    return false;
  }
  if (!ids.includes(state.client)) state.client = ids[0];
  const picker = document.createElement("div");
  picker.className = "client-picker";
  picker.innerHTML = select(
    "client",
    "Working with client",
    ids.map((id) => [id, id]),
    state.client,
  );
  picker.querySelector("select").onchange = (e) => {
    state.client = e.target.value;
    refresh();
  };
  host.append(picker);
  return true;
}
function repeated(kind, rows = []) {
  const box = document.createElement("div");
  box.className = "repeat-editor";
  const list = document.createElement("div");
  function add(row = {}) {
    const line = document.createElement("fieldset");
    line.className = "repeat-row";
    line.innerHTML =
      kind === "exercise"
        ? `<legend>Exercise</legend>${field("name", "Exercise name", "text", row.name || "")}${field("sets", "Sets (optional)", "number", row.sets ?? "", 'min="1" max="100"')}${field("reps", "Repetitions (optional)", "number", row.reps ?? "", 'min="1" max="10000"')}${field("durationMinutes", "Minutes (optional)", "number", row.durationMinutes ?? "", 'min="0.1" step="0.1"')}`
        : `<legend>Meal</legend>${field("name", "Meal name", "text", row.name || "")}${select("mealType", "Meal type", ["Breakfast", "Lunch", "Dinner", "Snack"], row.mealType || "Breakfast")}${field("calories", "Calories (kcal)", "number", row.calories ?? "", 'min="0" step="0.1"')}${field("plannedTimeUtc", "Time in UTC (optional)", "time", row.plannedTimeUtc?.slice(0, 5) || "")}`;
    for (const input of line.querySelectorAll("input"))
      if (
        ["sets", "reps", "durationMinutes", "plannedTimeUtc"].includes(
          input.name,
        )
      )
        input.required = false;
    line.append(button("Remove", () => line.remove(), "text-button"));
    list.append(line);
  }
  for (const row of rows.length ? rows : [{}]) add(row);
  box.append(
    list,
    button(`+ Add ${kind}`, () => add()),
  );
  box.values = () =>
    [...list.children].map((line) =>
      Object.fromEntries(
        [...line.querySelectorAll("input,select")].map((input) => [
          input.name,
          ["sets", "reps", "durationMinutes", "calories"].includes(input.name)
            ? input.value === ""
              ? null
              : Number(input.value)
            : input.name === "plannedTimeUtc"
              ? input.value
                ? input.value + ":00"
                : null
              : input.value,
        ]),
      ),
    );
  return box;
}
function builder(kind, existing) {
  const template = kind === "template",
    diet = kind === "diet";
  const editor = repeated(
    diet ? "meal" : "exercise",
    existing?.[diet ? "meals" : "exercises"],
  );
  const f = form(
    field("title", "Plan title", "text", existing?.title || "") +
      (template
        ? ""
        : field(
            "startDate",
            "Start date",
            "date",
            existing?.startDate || day(),
          ) +
          field("endDate", "End date", "date", existing?.endDate || day(28))),
    existing
      ? "Save changes"
      : template
        ? "Save template"
        : diet
          ? "Assign diet plan"
          : "Create draft",
    async (d) => {
      const entries = editor.values();
      if (!entries.length)
        throw Error("Add at least one " + (diet ? "meal" : "exercise") + ".");
      if (!template && d.endDate < d.startDate)
        throw Error("End date must be on or after start date.");
      const body = {
        ...d,
        ...(template ? {} : { clientUserId: state.client }),
        [diet ? "meals" : "exercises"]: entries,
      };
      await api(
        (diet
          ? "nutrition/plans"
          : template
            ? "workouts/templates"
            : "workouts/plans") + (existing ? "/" + existing.id : ""),
        existing ? "PUT" : "POST",
        body,
      );
      closeModal();
      toast("Plan saved.");
      refresh();
    },
  );
  f.insertBefore(editor, f.querySelector(".form-error"));
  modal(
    existing
      ? "Edit " + (diet ? "diet plan" : template ? "template" : "workout draft")
      : template
        ? "Build a workout template"
        : diet
          ? "Build a diet plan"
          : "Build a workout plan",
    f,
  );
}
async function sessions(plan) {
  const rows = await api(
    `workouts/clients/${clientId()}/plans/${plan.id}/sessions`,
  );
  const content = section(
    "Scheduled workouts",
    "Times are displayed in your local timezone.",
  );
  content.append(
    table(
      rows,
      [["scheduledAtUtc", "Scheduled for", (v) => esc(date(v, true))]],
      role() === "Client" && plan.status === "Published"
        ? (r) =>
            new Date(r.scheduledAtUtc) <= new Date()
              ? [
                  button("Log session", async () => {
                    await api("workouts/logs", "POST", {
                      planId: plan.id,
                      sessionId: r.id,
                      completedAtUtc: new Date().toISOString(),
                    });
                    toast("Session recorded.");
                    closeModal();
                    refresh();
                  }),
                ]
              : []
        : null,
    ),
  );
  if (role() === "Trainer" && plan.status === "Draft") {
    const f = form(
      '<label>Session times (one local date and time per line)<textarea name="times" placeholder="2026-11-01T09:00" required></textarea></label>',
      "Replace schedule",
      async (d) => {
        const times = d.times
          .split("\n")
          .filter((x) => x.trim())
          .map(utc);
        await api(`workouts/plans/${plan.id}/sessions`, "PUT", {
          clientUserId: state.client,
          times,
        });
        closeModal();
        toast("Schedule updated.");
        refresh();
      },
    );
    f.querySelector("textarea").value = rows
      .map((r) => localTime(r.scheduledAtUtc))
      .join("\n");
    content.append(f);
  }
  modal(plan.title + " · sessions", content);
}
export async function plans(host, kind = "workout") {
  const template = kind === "template",
    diet = kind === "diet";
  if (!template && !(await chooseClient(host))) return;
  const panel = section(
    template
      ? "Your template library"
      : diet
        ? "Nutrition, planned"
        : "Your training programme",
    template
      ? "Reusable sessions, ready to make your own."
      : diet
        ? "Meals and daily timing from your trainer."
        : "A clear plan for your next step.",
  );
  host.append(panel);
  if (role() === "Trainer")
    panel
      .querySelector(".section-head")
      .append(
        button(
          "+ " +
            (template
              ? "New template"
              : diet
                ? "New diet plan"
                : "New workout plan"),
          () => builder(kind),
          "primary",
        ),
      );
  const path = template
    ? "workouts/templates"
    : `${diet ? "nutrition" : "workouts"}/clients/${clientId()}/plans`;
  let page = 1;
  const list = document.createElement("div");
  panel.append(list);
  async function load() {
    list.replaceChildren();
    const rows = await api(path + `?page=${page}&size=20`);
    if (!rows.length)
      list.append(
        empty(
          "Your " +
            (template ? "templates" : "assigned plans") +
            " will appear here.",
        ),
      );
    for (const plan of rows) {
      const card = document.createElement("article");
      card.className = "plan";
      card.innerHTML = `<div class="plan-heading"><div><span class="eyebrow">${template ? "WORKOUT LIBRARY" : diet ? "NUTRITION PLAN" : "WORKOUT PLAN · VERSION " + plan.version}</span><h3>${esc(plan.title)}</h3></div>${badge(plan.status || (plan.isArchived ? "Archived" : template ? "Template" : "Assigned"))}</div>${template ? "" : `<p class="muted">${esc(date(plan.startDate))} — ${esc(date(plan.endDate))}</p>`}`;
      card.append(
        table(
          plan[diet ? "meals" : "exercises"],
          diet
            ? [
                ["name", "Meal"],
                ["mealType", "Type"],
                ["calories", "kcal"],
                ["plannedTimeUtc", "Time (UTC)"],
              ]
            : [
                ["name", "Exercise"],
                ["sets", "Sets"],
                ["reps", "Reps"],
                ["durationMinutes", "Minutes"],
              ],
        ),
      );
      const a = document.createElement("div");
      a.className = "actions";
      if (role() === "Trainer") {
        if (diet || template || plan.status === "Draft")
          a.append(button("Edit", () => builder(kind, plan)));
        if (!diet && !template) {
          a.append(button("Sessions", () => sessions(plan)));
          for (const [text, endpoint] of plan.status === "Draft"
            ? [["Publish plan", "publish"]]
            : plan.status === "Published"
              ? [
                  ["Create revision", "revisions"],
                  ["Archive", "archive"],
                ]
              : [["Create revision", "revisions"]])
            a.append(
              button(text, () =>
                confirmAction(
                  text,
                  "Apply this change to " + plan.title + "?",
                  async () => {
                    await api(`workouts/plans/${plan.id}/${endpoint}`, "POST", {
                      clientUserId: state.client,
                    });
                    toast("Plan updated.");
                    refresh();
                  },
                ),
              ),
            );
        }
        if (template) {
          a.append(
            button("Clone for client", async () => {
              const assignments = (await all("assignments")).filter(
                (x) => x.status === "Active",
              );
              if (!assignments.length)
                throw Error("Assign a client before cloning a template.");
              modal(
                "Create a plan from template",
                form(
                  select(
                    "clientUserId",
                    "Client",
                    assignments.map((x) => [x.clientUserId, x.clientUserId]),
                  ) +
                    field("startDate", "Start date", "date", day()) +
                    field("endDate", "End date", "date", day(28)),
                  "Create draft",
                  async (d) => {
                    await api(`workouts/templates/${plan.id}/clone`, "POST", d);
                    closeModal();
                    toast("Draft created for the selected client.");
                  },
                ),
              );
            }),
          );
          a.append(
            button("Archive", () =>
              confirmAction(
                "Archive template",
                "Remove this template from your active library?",
                async () => {
                  await api(
                    `workouts/templates/${plan.id}/archive`,
                    "POST",
                    {},
                  );
                  refresh();
                },
              ),
            ),
          );
        }
      } else if (!diet) {
        a.append(button("View sessions", () => sessions(plan)));
        if (plan.status === "Published")
          a.append(
            button(
              "Log workout",
              () =>
                modal(
                  "Record workout",
                  form(
                    field(
                      "completedAtUtc",
                      "Completed at (local time)",
                      "datetime-local",
                      localTime(),
                    ),
                    "Save workout",
                    async (d) => {
                      await api("workouts/logs", "POST", {
                        planId: plan.id,
                        completedAtUtc: utc(d.completedAtUtc),
                      });
                      closeModal();
                      toast("Workout recorded.");
                    },
                  ),
                ),
              "primary",
            ),
          );
      }
      card.append(a);
      list.append(card);
    }
    const controls = document.createElement("div");
    controls.className = "pagination";
    const prev = button("← Previous", () => {
        page--;
        return load();
      }),
      next = button("Next →", () => {
        page++;
        return load();
      });
    prev.disabled = page === 1;
    next.disabled = rows.length < 20;
    controls.append(prev, document.createTextNode("Page " + page), next);
    list.append(controls);
  }
  await load();
}
