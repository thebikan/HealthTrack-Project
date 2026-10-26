export const state = {
  user: null,
  token: null,
  client: null,
  route: "overview",
  revision: 0,
};
export const $ = (s, p = document) => p.querySelector(s);
export const esc = (v) =>
  String(v ?? "").replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
export const title = (s) =>
  s
    .replace(/([A-Z])/g, " $1")
    .replace(/^./, (c) => c.toUpperCase())
    .replace(/Utc/g, "")
    .trim();
export const role = () =>
  state.user?.roles.includes("Admin")
    ? "Admin"
    : state.user?.roles.includes("Trainer")
      ? "Trainer"
      : "Client";
export const date = (v, time = false) =>
  v
    ? new Date(v.length === 10 ? v + "T12:00:00" : v).toLocaleString(
        undefined,
        time
          ? { dateStyle: "medium", timeStyle: "short" }
          : { dateStyle: "medium" },
      )
    : "Not recorded";
export const localTime = (v = new Date()) => {
  const d = new Date(v);
  return new Date(d - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
};
export const day = (offset = 0) =>
  localTime(new Date(Date.now() + offset * 864e5)).slice(0, 10);
export const utc = (v) => new Date(v).toISOString();
export function interval(from = day(-30), to = day(1)) {
  return new URLSearchParams({
    from: utc(from + "T00:00"),
    to: utc(to + "T00:00"),
  }).toString();
}
export const clientId = () =>
  encodeURIComponent(role() === "Client" ? state.user.id : state.client || "");
export async function api(path, method = "GET", body) {
  if (method !== "GET" && !state.token)
    state.token = (await api("account/csrf")).token;
  let response;
  try {
    response = await fetch("/api/" + path, {
      method,
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        ...(method !== "GET" ? { "X-CSRF-TOKEN": state.token } : {}),
      },
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
  } catch {
    throw Error("Unable to connect. Check your connection and try again.");
  }
  if (!response.ok) {
    let data;
    try {
      data = await response.json();
    } catch {}
    const error = Error(
      data?.detail ||
        Object.values(data?.errors || {})
          .flat()
          .join(" ") ||
        {
          401: "Your session has expired. Please sign in again.",
          403: "You no longer have access to this record.",
          404: "This record is unavailable.",
          409: "This record changed or conflicts with another record. Refresh before trying again.",
          429: "Too many attempts. Please wait a minute.",
          503: "This service is not configured yet.",
        }[response.status] ||
        data?.title ||
        "The service is unavailable. Please try again.",
    );
    error.status = response.status;
    if (response.status === 401 && state.user) {
      state.user = null;
      state.token = null;
      window.dispatchEvent(new Event("session-expired"));
    }
    throw error;
  }
  return response.status === 204
    ? null
    : response.headers.get("content-type")?.includes("json")
      ? response.json()
      : null;
}
export async function all(path) {
  const rows = [];
  for (let page = 1; ; page++) {
    const batch = await api(
      path + (path.includes("?") ? "&" : "?") + `page=${page}&size=100`,
    );
    rows.push(...batch);
    if (batch.length < 100) return rows;
  }
}
let toastTimer;
export function toast(message) {
  $("#notice").textContent = message;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => ($("#notice").textContent = ""), 6500);
}
export function button(text, action, style = "secondary") {
  const b = document.createElement("button");
  b.type = "button";
  b.className = style;
  b.textContent = text;
  b.onclick = async () => {
    b.disabled = true;
    try {
      await action();
    } catch (e) {
      toast(e.message);
    } finally {
      b.disabled = false;
    }
  };
  return b;
}
export function section(name, description = "") {
  const s = document.createElement("section");
  s.className = "panel";
  s.innerHTML = `<div class="section-head"><div><h2>${esc(name)}</h2>${description ? `<p>${esc(description)}</p>` : ""}</div></div>`;
  return s;
}
export function empty(
  message = "Your records will appear here when you add them.",
) {
  const e = document.createElement("div");
  e.className = "empty";
  e.innerHTML = `<span aria-hidden="true">↗</span><h3>A fresh start</h3><p>${esc(message)}</p>`;
  return e;
}
export const badge = (s) =>
  `<span class="badge ${["Active", "Approved", "Published", "Completed"].includes(s) ? "positive" : ["Cancelled", "Rejected", "Inactive", "Archived"].includes(s) ? "neutral" : "pending"}">${esc(s)}</span>`;
export function field(name, label, type = "text", value = "", extra = "") {
  return `<label>${esc(label)}<input name="${esc(name)}" type="${type}" value="${esc(value)}" ${extra} required></label>`;
}
export function select(name, label, options, value = "") {
  return `<label>${esc(label)}<select name="${name}" required>${options
    .map((o) => {
      const [v, t] = Array.isArray(o) ? o : [o, title(o)];
      return `<option value="${esc(v)}" ${String(v) === String(value) ? "selected" : ""}>${esc(t)}</option>`;
    })
    .join("")}</select></label>`;
}
export function form(markup, submit, action) {
  const f = document.createElement("form");
  f.innerHTML = `<div class="form-grid">${markup}</div><p class="form-error" role="alert" tabindex="-1" hidden></p><div class="form-actions"><button type="submit" class="primary">${esc(submit)}</button><span class="muted">Fields are required unless marked optional.</span></div>`;
  f.onsubmit = async (e) => {
    e.preventDefault();
    const b = $("[type=submit]", f),
      err = $(".form-error", f);
    b.disabled = true;
    b.textContent = "Saving…";
    err.hidden = true;
    try {
      await action(Object.fromEntries(new FormData(f)), f);
    } catch (e) {
      err.textContent = e.message;
      err.hidden = false;
      err.focus();
    } finally {
      b.disabled = false;
      b.textContent = submit;
    }
  };
  return f;
}
let returnFocus;
export function modal(name, content) {
  returnFocus = document.activeElement;
  $("#dialog-title").textContent = name;
  $("#dialog-content").replaceChildren(content);
  if (!$("#dialog").open) $("#dialog").showModal();
}
export function closeModal() {
  $("#dialog").close();
  returnFocus?.focus();
}
export function confirmAction(name, message, action) {
  const d = document.createElement("div");
  const p = document.createElement("p");
  p.textContent = message;
  d.append(p);
  const controls = document.createElement("div");
  controls.className = "actions";
  controls.append(
    button("Keep unchanged", closeModal),
    button(
      name,
      async () => {
        await action();
        closeModal();
      },
      "danger",
    ),
  );
  d.append(controls);
  modal(name, d);
}
export function table(rows, columns, actions) {
  if (!rows.length) return empty();
  const wrap = document.createElement("div");
  wrap.className = "table-wrap";
  wrap.tabIndex = 0;
  wrap.setAttribute("aria-label", "Scrollable records");
  const t = document.createElement("table");
  t.innerHTML = `<thead><tr>${columns.map((c) => `<th scope="col">${esc(c[1])}</th>`).join("")}${actions ? '<th scope="col">Actions</th>' : ""}</tr></thead>`;
  const body = document.createElement("tbody");
  for (const row of rows) {
    const tr = document.createElement("tr");
    for (const [key, , format] of columns) {
      const td = document.createElement("td");
      if (format) td.innerHTML = format(row[key], row);
      else td.textContent = row[key] ?? "—";
      tr.append(td);
    }
    if (actions) {
      const td = document.createElement("td");
      td.className = "row-actions";
      for (const action of actions(row).filter(Boolean)) td.append(action);
      tr.append(td);
    }
    body.append(tr);
  }
  t.append(body);
  wrap.append(t);
  return wrap;
}
export function paged(host, path, columns, actions) {
  let page = 1,
    revision = 0;
  const body = document.createElement("div"),
    controls = document.createElement("div");
  controls.className = "pagination";
  host.append(body, controls);
  async function load() {
    const version = ++revision;
    body.innerHTML = '<p role="status">Loading records…</p>';
    controls.replaceChildren();
    try {
      const rows = await api(
        path + (path.includes("?") ? "&" : "?") + `page=${page}&size=20`,
      );
      if (version !== revision) return;
      body.replaceChildren(table(rows, columns, actions));
      const previous = button("← Previous", () => {
          page--;
          return load();
        }),
        next = button("Next →", () => {
          page++;
          return load();
        });
      previous.disabled = page === 1;
      next.disabled = rows.length < 20;
      const label = document.createElement("span");
      label.textContent = `Page ${page}`;
      controls.append(previous, label, next);
    } catch (e) {
      body.textContent = e.message;
      controls.append(button("Try again", load));
    }
  }
  load();
  return load;
}
export function dateFilter(host, action, from = day(-30), to = day(1)) {
  const f = form(
    field("from", "From", "date", from) +
      field("to", "Until (exclusive)", "date", to),
    "Apply dates",
    async (d) => {
      if (d.to <= d.from)
        throw Error("The end date must be after the start date.");
      await action(interval(d.from, d.to));
    },
  );
  f.classList.add("filter-form");
  host.append(f);
  return f;
}
export function metrics(data) {
  const d = document.createElement("div");
  d.className = "metrics";
  for (const [name, value, unit] of data) {
    const item = document.createElement("article");
    item.innerHTML = `<span>${esc(name)}</span><strong>${esc(value ?? "—")}<small>${esc(unit || "")}</small></strong>`;
    d.append(item);
  }
  return d;
}
export function go(route) {
  if (location.hash === "#" + route)
    window.dispatchEvent(new HashChangeEvent("hashchange"));
  else location.hash = route;
}
export function csv(path) {
  const a = document.createElement("a");
  a.className = "button secondary";
  a.href = "/api/" + path;
  a.textContent = "↓ Download CSV";
  return a;
}
