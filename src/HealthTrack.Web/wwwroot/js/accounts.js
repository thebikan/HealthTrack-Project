import {
  api,
  all,
  state,
  role,
  section,
  button,
  form,
  field,
  select,
  modal,
  closeModal,
  toast,
  paged,
  date,
  badge,
  esc,
  go,
  confirmAction,
  dateFilter,
  interval,
  csv,
} from "./core.js";
const refresh = () => go(state.route);
export function auth(host, mode = "login") {
  document.body.classList.add("public");
  const register = mode === "register",
    recover = mode === "recovery",
    reset = mode === "reset";
  host.innerHTML = `<div class="auth-layout"><section class="auth-story"><span class="eyebrow">A HEALTHIER EVERYDAY</span><h1>Every big thing<br>starts small.</h1><p>Log in and continue growing.</p><div class="wellness-art" aria-hidden="true"><div class="orbit orbit-one"></div><div class="orbit orbit-two"></div><div class="art-center">✚</div><span class="art-label label-one">MOVE WITH PURPOSE</span><span class="art-label label-two">GROW AT YOUR PACE</span></div><div class="story-footer"><span>01 / TRAIN</span><span>02 / NOURISH</span><span>03 / GROW</span></div></section><section class="auth-form"><span class="eyebrow">WELCOME TO HEALTHTRACK</span><h2>${register ? "Start your journey" : recover ? "Let’s get you back in" : reset ? "Choose a new password" : "Log in to your account"}</h2><p>${register ? "Create an account and take your first step." : recover ? "Enter your account email to request recovery instructions." : reset ? "Use the recovery token delivered privately to you." : "Your next healthy chapter starts here."}</p><div id="auth-fields"></div><div class="auth-links"></div></section></div>`;
  const password = field(
    reset ? "newPassword" : "password",
    reset ? "New password" : "Password",
    "password",
    "",
    'minlength="12" autocomplete="' +
      (register || reset ? "new-password" : "current-password") +
      '"',
  );
  const markup =
    (register
      ? field(
          "fullName",
          "Full name",
          "text",
          "",
          'autocomplete="name" maxlength="200"',
        )
      : "") +
    field("email", "Email address", "email", "", 'autocomplete="email"') +
    (recover
      ? ""
      : reset
        ? field("token", "Recovery token") + password
        : password) +
    (register || reset
      ? '<p class="password-help">Use at least 12 characters, including uppercase, lowercase, a number and a symbol.</p>'
      : "") +
    (register
      ? select("role", "I’m joining as", [
          ["Client", "Client — build healthy habits"],
          ["Trainer", "Trainer — support clients"],
        ])
      : "");
  host.querySelector("#auth-fields").append(
    form(
      markup,
      register
        ? "Create account →"
        : recover
          ? "Send recovery instructions"
          : reset
            ? "Reset password"
            : "Sign in →",
      async (d) => {
        await api(
          "account/" +
            (register
              ? "register"
              : recover
                ? "recovery"
                : reset
                  ? "recovery/reset"
                  : "login"),
          "POST",
          d,
        );
        state.token = null;
        if (register) {
          toast("Account created. Sign in to continue.");
          go("login");
        } else if (recover) {
          toast("If your account is eligible, instructions will be sent.");
        } else if (reset) {
          toast("Password reset. Sign in with your new password.");
          go("login");
        } else {
          state.user = await api("account/me");
          go("overview");
        }
      },
    ),
  );
  for (const input of host.querySelectorAll('input[type="password"]')) {
    const wrap = document.createElement("div");
    wrap.className = "password-control";
    input.parentNode.insertBefore(wrap, input);
    wrap.append(input);
    const toggle = button(
      "Show",
      () => {
        const reveal = input.type === "password";
        input.type = reveal ? "text" : "password";
        toggle.textContent = reveal ? "Hide" : "Show";
        toggle.setAttribute(
          "aria-label",
          reveal ? "Hide password" : "Show password",
        );
      },
      "password-toggle",
    );
    toggle.setAttribute("aria-label", "Show password");
    wrap.append(toggle);
  }
  const emailInput = host.querySelector('input[type="email"]');
  if (emailInput) emailInput.placeholder = "Enter your email";
  for (const input of host.querySelectorAll('input[type="password"]'))
    input.placeholder = "Enter your password";
  const links = host.querySelector(".auth-links");
  links.append(
    button(
      register ? "Already a member? Sign in" : "New here? Create an account",
      () => go(register ? "login" : "register"),
      "text-button",
    ),
  );
  if (!register)
    links.append(
      button(
        recover
          ? "Have a recovery token? Reset password"
          : "Forgot your password?",
        () => go(recover ? "reset" : "recovery"),
        "text-button",
      ),
    );
  if (recover || reset)
    links.append(button("Back to sign in", () => go("login"), "text-button"));
}
export async function profile(host) {
  const panel = section("Personal details", "Keep your profile up to date.");
  host.append(panel);
  const f = form(
    field(
      "fullName",
      "Full name",
      "text",
      state.user.fullName,
      'maxlength="200"',
    ) +
      (role() === "Client"
        ? field(
            "heightCm",
            "Height in cm (optional)",
            "number",
            state.user.heightCm || "",
            'min="1" step="0.01"',
          )
        : role() === "Trainer"
          ? field(
              "specialization",
              "Specialization (optional)",
              "text",
              state.user.specialization || "",
            )
          : ""),
    "Save profile",
    async (d) => {
      await api("account/me", "PUT", {
        fullName: d.fullName,
        heightCm: d.heightCm ? +d.heightCm : null,
        specialization: d.specialization || null,
      });
      state.user = await api("account/me");
      toast("Profile updated.");
      refresh();
    },
  );
  for (const name of ["heightCm", "specialization"])
    if (f.elements[name]) f.elements[name].required = false;
  panel.append(f);
  const security = section(
    "Password & security",
    "Choose a strong, unique password.",
  );
  security.append(
    form(
      field(
        "currentPassword",
        "Current password",
        "password",
        "",
        'autocomplete="current-password"',
      ) +
        field(
          "newPassword",
          "New password",
          "password",
          "",
          'minlength="12" autocomplete="new-password"',
        ),
      "Change password",
      async (d, f) => {
        await api("account/password", "POST", d);
        f.reset();
        state.token = null;
        toast("Password updated.");
      },
    ),
  );
  host.append(security);
  host.append(
    button("Sign out", async () => {
      await api("account/logout", "POST", {});
      state.user = null;
      state.client = null;
      state.token = null;
      go("login");
    }),
  );
}
export async function users(host) {
  const panel = section(
    "People on your platform",
    "Find accounts and manage access.",
  );
  host.append(panel);
  const list = document.createElement("div");
  function load(query = "") {
    list.replaceChildren();
    paged(
      list,
      "account/users?" + query,
      [
        ["fullName", "Name"],
        ["email", "Email"],
        ["isActive", "Account status", (v) => badge(v ? "Active" : "Inactive")],
      ],
      (r) => [
        button(r.isActive ? "Deactivate" : "Activate", () =>
          confirmAction(
            (r.isActive ? "Deactivate" : "Activate") + " account",
            `Change access for ${r.fullName}?`,
            async () => {
              await api(
                `account/users/${encodeURIComponent(r.id)}/activation`,
                "PUT",
                { isActive: !r.isActive },
              );
              toast("Account status updated.");
              load(query);
            },
          ),
        ),
      ],
    );
  }
  const filters = form(
    field("search", "Name or email") +
      select("role", "Role", [
        ["", "All roles"],
        "Client",
        "Trainer",
        "Admin",
      ]) +
      select("active", "Status", [
        ["", "All statuses"],
        ["true", "Active"],
        ["false", "Inactive"],
      ]),
    "Search accounts",
    async (d) => load(new URLSearchParams(d).toString()),
  );
  for (const input of filters.querySelectorAll("input,select"))
    input.required = false;
  filters.classList.add("filter-form");
  panel.append(filters, list);
  load();
}
export async function assignments(host) {
  const admin = role() === "Admin";
  const panel = section(
    admin ? "Connect trainers & clients" : "Your assigned clients",
    admin
      ? "The right support makes all the difference."
      : "Open a client workspace to manage plans and review progress.",
  );
  host.append(panel);
  if (admin)
    panel.querySelector(".section-head").append(
      button(
        "+ Assign trainer",
        async () => {
          const [trainers, clients] = await Promise.all([
            all("account/users?role=Trainer&active=true"),
            all("account/users?role=Client&active=true"),
          ]);
          if (!trainers.length || !clients.length)
            throw Error("An active trainer and client account are required.");
          modal(
            "Create an assignment",
            form(
              select(
                "trainerUserId",
                "Trainer",
                trainers.map((x) => [x.id, `${x.fullName} · ${x.email}`]),
              ) +
                select(
                  "clientUserId",
                  "Client",
                  clients.map((x) => [x.id, `${x.fullName} · ${x.email}`]),
                ),
              "Assign trainer",
              async (d) => {
                await api("assignments", "POST", d);
                closeModal();
                toast("Trainer assigned.");
                refresh();
              },
            ),
          );
        },
        "primary",
      ),
    );
  paged(
    panel,
    "assignments",
    [
      ["clientUserId", "Client"],
      ["trainerUserId", "Trainer"],
      ["status", "Status", badge],
      ["assignedAtUtc", "Assigned", (v) => esc(date(v))],
    ],
    (r) =>
      r.status === "Active"
        ? [
            admin
              ? button("Revoke", () =>
                  confirmAction(
                    "Revoke assignment",
                    "The trainer will lose access to this client’s records.",
                    async () => {
                      await api(`assignments/${r.id}/revoke`, "POST", {
                        rowVersion: r.rowVersion,
                      });
                      refresh();
                    },
                  ),
                )
              : button("Open client", () => {
                  state.client = r.clientUserId;
                  go("client");
                }),
          ]
        : [],
  );
}
export async function audit(host) {
  const panel = section(
    "Platform activity",
    "Administrative metadata, without personal health details.",
  );
  host.append(panel);
  const list = document.createElement("div");
  function load(query) {
    list.replaceChildren();
    paged(list, "reports/audit?" + query, [
      ["createdAtUtc", "When", (v) => esc(date(v, true))],
      ["action", "Action"],
      ["actorUserId", "Performed by"],
      ["subjectUserId", "Subject"],
    ]);
  }
  dateFilter(panel, async (q) => load(q));
  panel.append(list);
  load(interval());
}
