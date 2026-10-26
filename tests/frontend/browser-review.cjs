// Run with PLAYWRIGHT_MODULE pointing to an existing Playwright install.
// API responses below are synthetic fixtures, never production fallbacks.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const fs = require("node:fs");
const assert = require("node:assert/strict");
const origin = process.env.HEALTHTRACK_PREVIEW_URL || "http://localhost:5188";
const now = new Date(),
  iso = now.toISOString(),
  today = iso.slice(0, 10),
  future = new Date(+now + 864e5).toISOString();
const version = "AAAAAAAAAAE=";
const assignment = {
  id: 1,
  clientUserId: "client-fixture",
  trainerUserId: "trainer-fixture",
  status: "Active",
  assignedAtUtc: iso,
  rowVersion: version,
};
const exercise = {
  name: "Controlled squat",
  sets: 3,
  reps: 12,
  durationMinutes: null,
};
const plan = {
  id: 1,
  title: "Build your foundation",
  status: "Draft",
  version: 1,
  startDate: today,
  endDate: future.slice(0, 10),
  exercises: [exercise],
};
const progress = {
  workouts: [{ dayUtc: iso, completedWorkouts: 2 }],
  calories: [{ dayUtc: iso, recordedCalories: 1800 }],
  weight: [
    { dayUtc: iso, averageWeightKg: 72 },
    { dayUtc: future, averageWeightKg: 71.8 },
  ],
  currentPublishedPlans: 1,
  plansWithLogs: 1,
  currentPlanCoveragePercent: 100,
};
(async () => {
  const browser = await chromium.launch({ headless: true, channel: "chrome" });
  const results = [],
    errors = [];
  for (const role of ["Public", "Client", "Trainer", "Admin"]) {
    const context = await browser.newContext({
      viewport: { width: 1440, height: 1000 },
    });
    const page = await context.newPage();
    page.on("pageerror", (e) => errors.push(role + ": " + e.message));
    const writes = [];
    await page.route("**/api/**", async (route) => {
      const req = route.request(),
        url = new URL(req.url()),
        p = url.pathname.replace("/api/", "");
      let data = [];
      if (req.method() !== "GET") {
        writes.push({
          path: p,
          body: req.postDataJSON(),
          csrf: req.headers()["x-csrf-token"],
        });
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({ id: 5 }),
        });
      }
      if (p === "account/me") {
        if (role === "Public") return route.fulfill({ status: 401 });
        data = {
          id: role === "Client" ? "client-fixture" : "trainer-fixture",
          fullName: "Alex Morgan",
          email: "alex@example.test",
          roles: [role],
          heightCm: 175,
          isActive: true,
        };
      } else if (p === "account/csrf") data = { token: "fixture-token" };
      else if (p === "assignments") data = [assignment];
      else if (p === "account/users")
        data = [
          {
            id: "client-fixture",
            fullName: "Jamie Chen",
            email: "jamie@example.test",
            isActive: true,
          },
        ];
      else if (p === "reports/dashboard/client")
        data = {
          publishedPlans: 1,
          currentDietPlans: 1,
          workoutsLast30Days: 12,
          unreadNotifications: 1,
          latestMeasurement: { weightKg: 72 },
          nextAppointment: { startAtUtc: future, status: "Approved" },
        };
      else if (p === "reports/dashboard/trainer")
        data = {
          activeClients: 1,
          upcomingAppointments: 2,
          clients: [
            {
              clientUserId: "client-fixture",
              publishedPlans: 1,
              workoutsLast30Days: 12,
            },
          ],
        };
      else if (p === "reports/dashboard/admin")
        data = {
          accounts: {
            total: 24,
            activeClients: 18,
            activeTrainers: 4,
            inactive: 2,
          },
          activeAssignments: 12,
          workouts: 42,
          meals: 68,
          measurements: 18,
          pendingAppointments: 3,
          approvedAppointments: 5,
          completedAppointments: 8,
        };
      else if (p.endsWith("/progress")) data = progress;
      else if (p.endsWith("/adherence"))
        data = {
          scheduledSessionsDue: 3,
          completedSessions: 2,
          adherencePercent: 66.7,
        };
      else if (p === "reports/trainer")
        data = {
          activeClients: 1,
          workoutPlans: 3,
          dietPlans: 1,
          feedbackEntries: 4,
          completedAppointments: 2,
        };
      else if (p.startsWith("reports/audit"))
        data = [
          {
            createdAtUtc: iso,
            action: "TrainerAssigned",
            actorUserId: "admin-fixture",
            subjectUserId: "client-fixture",
          },
        ];
      else if (p === "workouts/templates")
        data = [
          {
            id: 1,
            title: "Strength essentials",
            isArchived: false,
            exercises: [exercise],
          },
        ];
      else if (p.endsWith("/sessions")) data = [{ id: 1, scheduledAtUtc: iso }];
      else if (p.startsWith("workouts/") && p.endsWith("/plans"))
        data = [{ ...plan, status: role === "Client" ? "Published" : "Draft" }];
      else if (p.startsWith("nutrition/") && p.endsWith("/plans"))
        data = [
          {
            id: 1,
            title: "Balanced week",
            startDate: today,
            endDate: future.slice(0, 10),
            meals: [
              {
                name: "Oats and berries",
                mealType: "Breakfast",
                calories: 350,
                plannedTimeUtc: "07:00:00",
              },
            ],
          },
        ];
      else if (p.endsWith("/measurements"))
        data = [
          { recordedAtUtc: iso, weightKg: 72, heightCm: 175, bmi: 23.51 },
        ];
      else if (p.startsWith("goals/"))
        data = [
          {
            id: 1,
            type: "WorkoutCompletion",
            status: "Active",
            target: 20,
            current: 12,
            progressPercent: 60,
            targetDate: future.slice(0, 10),
            rowVersion: version,
          },
        ];
      else if (p === "appointments")
        data = [
          {
            id: 1,
            ...assignment,
            startAtUtc: future,
            endAtUtc: new Date(+new Date(future) + 3600000).toISOString(),
            status: "Pending",
            rowVersion: version,
          },
        ];
      else if (p.endsWith("/history"))
        data = [{ previousStatus: null, status: "Pending", changedAtUtc: iso }];
      else if (p === "notifications/preferences")
        data = {
          workoutReminders: true,
          mealReminders: true,
          appointmentReminders: true,
        };
      else if (p === "notifications/schedules")
        data = [
          {
            id: 1,
            kind: "Workout",
            dueAtUtc: future,
            isCancelled: false,
            rowVersion: version,
          },
        ];
      else if (p === "notifications")
        data = [
          {
            id: 1,
            title: "Your next step",
            message: "Your trainer shared a new plan.",
            dueAtUtc: iso,
            readAtUtc: null,
          },
        ];
      else if (p.endsWith("/feedback"))
        data = [
          {
            createdAtUtc: iso,
            text: "Keep up the steady progress.",
            trainerUserId: "trainer-fixture",
          },
        ];
      else if (p.startsWith("nutrition/") && p.endsWith("/logs"))
        data = [{ name: "Lunch", calories: 450, consumedAtUtc: iso }];
      else if (p.endsWith("/logs")) data = [{ planId: 1, completedAtUtc: iso }];
      if (url.searchParams.get("page") > 1) data = [];
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(data),
      });
    });
    await page.goto(origin);
    await page.waitForSelector(
      role === "Public" ? ".auth-form" : ".dashboard-greeting",
    );
    for (const width of [1440, 1280, 1024, 768, 390, 320]) {
      await page.setViewportSize({ width, height: 1000 });
      await page.waitForTimeout(220);
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth > innerWidth,
      );
      assert.equal(overflow, false, `${role} overflow at ${width}`);
      await page.screenshot({
        path: `artifacts/frontend/${role.toLowerCase()}-${width}.png`,
        fullPage: true,
      });
      results.push({ role, width, overflow });
    }
    await page.setViewportSize({ width: 1440, height: 1000 });
    if (role !== "Public") {
      const links = await page
        .locator("nav a")
        .evaluateAll((as) => as.map((a) => a.hash));
      for (const hash of links) {
        await page.goto(origin + "/" + hash);
        await page.waitForTimeout(160);
        await page.waitForSelector("main .panel, main .metrics");
        assert.equal(
          await page.locator("main > .error").count(),
          0,
          `${role} ${hash}`,
        );
        for (const width of [1440, 390, 320]) {
          await page.setViewportSize({ width, height: 1000 });
          await page.waitForTimeout(200);
          assert.equal(
            await page.evaluate(
              () => document.documentElement.scrollWidth > innerWidth,
            ),
            false,
            `${role} ${hash} overflow at ${width}`,
          );
          results.push({ role, route: hash, width, rendered: true });
        }
        await page.setViewportSize({ width: 1440, height: 1000 });
      }
    }
    if (role === "Client") {
      await page.goto(origin + "/#measurements");
      await page.getByRole("button", { name: "+ Add measurement" }).click();
      await page.getByLabel("Weight (kg)", { exact: true }).fill("71.5");
      await page
        .getByRole("button", { name: "Save record", exact: true })
        .click();
      await page.waitForTimeout(200);
      assert(
        writes.some(
          (w) =>
            w.path === "health/measurements" &&
            w.body.weightKg === 71.5 &&
            w.csrf === "fixture-token",
        ),
      );
      await page.goto(origin + "/#goals");
      await page.getByRole("button", { name: "Complete", exact: true }).click();
      await page
        .getByRole("button", { name: "Complete goal", exact: true })
        .click();
      await page.waitForTimeout(150);
      assert(
        writes.some(
          (w) => w.path === "goals/1/finish" && w.body.rowVersion === version,
        ),
      );
    }
    if (role === "Trainer") {
      await page.goto(origin + "/#workouts");
      await page.getByRole("button", { name: "+ New workout plan" }).click();
      await page.getByLabel("Plan title").fill("Test programme");
      await page.getByLabel("Exercise name").fill("Squat");
      await page.getByLabel("Repetitions (optional)").fill("12");
      await page
        .getByRole("button", { name: "Create draft", exact: true })
        .click();
      await page.waitForTimeout(150);
      assert(
        writes.some(
          (w) => w.path === "workouts/plans" && w.body.exercises[0].reps === 12,
        ),
      );
      await page.goto(origin + "/#appointments");
      await page.getByRole("button", { name: "Approve", exact: true }).click();
      await page
        .getByRole("button", { name: "Approved appointment", exact: true })
        .click();
      await page.waitForTimeout(150);
      assert(
        writes.some(
          (w) =>
            w.path === "appointments/1/status" &&
            w.body.status === "Approved" &&
            w.body.rowVersion === version,
        ),
      );
    }
    if (role === "Client") {
      await page.goto(origin + "/#meals");
      await page.getByRole("button", { name: "+ Log meal" }).click();
      await page.getByLabel("Meal name", { exact: true }).fill("Lunch");
      await page.getByLabel("Calories (kcal)", { exact: true }).fill("420");
      await page
        .getByRole("button", { name: "Save record", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(
        writes.some(
          (w) => w.path === "nutrition/logs" && w.body.calories === 420,
        ),
      );
      await page.goto(origin + "/#appointments");
      await page.getByRole("button", { name: "+ Book appointment" }).click();
      await page
        .getByLabel("Starts (local time)", { exact: true })
        .fill(future.slice(0, 16));
      await page
        .getByLabel("Ends (local time)", { exact: true })
        .fill(new Date(+new Date(future) + 3600000).toISOString().slice(0, 16));
      await page
        .getByRole("button", { name: "Request appointment", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(
        writes.some(
          (w) =>
            w.path === "appointments" &&
            w.body.trainerUserId === "trainer-fixture" &&
            w.body.startAtUtc.endsWith("Z"),
        ),
      );
      await page.goto(origin + "/#notifications");
      await page
        .getByRole("button", { name: "Mark read", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(writes.some((w) => w.path === "notifications/1/read"));
      await page
        .getByRole("button", { name: "Save preferences", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(
        writes.some(
          (w) =>
            w.path === "notifications/preferences" &&
            w.body.workoutReminders === true,
        ),
      );
    }
    if (role === "Trainer") {
      await page.goto(origin + "/#workouts");
      await page
        .getByRole("button", { name: "Publish plan", exact: true })
        .click();
      await page
        .getByRole("dialog")
        .getByRole("button", { name: "Publish plan", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(
        writes.some(
          (w) =>
            w.path === "workouts/plans/1/publish" &&
            w.body.clientUserId === "client-fixture",
        ),
      );
      await page.goto(origin + "/#diet");
      await page.getByRole("button", { name: "+ New diet plan" }).click();
      await page
        .getByLabel("Plan title", { exact: true })
        .fill("Balanced week");
      await page
        .getByLabel("Meal name", { exact: true })
        .fill("Breakfast oats");
      await page.getByLabel("Calories (kcal)", { exact: true }).fill("350");
      await page
        .getByRole("button", { name: "Assign diet plan", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(
        writes.some(
          (w) =>
            w.path === "nutrition/plans" && w.body.meals[0].calories === 350,
        ),
      );
      await page.goto(origin + "/#feedback");
      await page
        .getByLabel("Feedback", { exact: true })
        .fill("Steady progress.");
      await page
        .getByRole("button", { name: "Share feedback", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(
        writes.some(
          (w) =>
            w.path === "health/feedback" && w.body.text === "Steady progress.",
        ),
      );
      await page.goto(origin + "/#templates");
      await page
        .getByRole("button", { name: "Clone for client", exact: true })
        .click();
      await page
        .getByRole("button", { name: "Create draft", exact: true })
        .click();
      await page.waitForTimeout(160);
      assert(writes.some((w) => w.path === "workouts/templates/1/clone"));
    }
    if (role === "Admin") {
      await page.goto(origin + "/#users");
      await page
        .getByRole("button", { name: "Deactivate", exact: true })
        .click();
      await page
        .getByRole("button", { name: "Deactivate account", exact: true })
        .click();
      await page.waitForTimeout(150);
      assert(
        writes.some(
          (w) => w.path.endsWith("/activation") && w.body.isActive === false,
        ),
      );
    }
    if (role === "Public") {
      for (const route of ["register", "recovery", "reset"]) {
        await page.goto(origin + "/#" + route);
        await page.waitForSelector(".auth-form");
        await page.setViewportSize({ width: 320, height: 900 });
        assert.equal(
          await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
          ),
          false,
        );
      }
      assert.equal(await page.locator('option[value="Admin"]').count(), 0);
    }
    if (role === "Client") {
      await page.setViewportSize({ width: 320, height: 900 });
      await page.goto(origin + "/#measurements");
      await page.getByRole("button", { name: "+ Add measurement" }).click();
      await page.screenshot({
        path: "artifacts/frontend/measurement-dialog-320.png",
        fullPage: true,
      });
      await page.getByLabel("Weight (kg)", { exact: true }).fill("73");
      await page.route("**/api/health/measurements", (route) =>
        route.fulfill({
          status: 409,
          contentType: "application/json",
          body: JSON.stringify({
            detail: "The record changed. Refresh and try again.",
          }),
        }),
      );
      await page
        .getByRole("button", { name: "Save record", exact: true })
        .click();
      await page.waitForSelector(".form-error:not([hidden])");
      assert.equal(
        await page.getByLabel("Weight (kg)", { exact: true }).inputValue(),
        "73",
      );
      assert.equal(
        await page.evaluate(() => document.activeElement.className),
        "form-error",
      );
      await page.keyboard.press("Escape");
      assert.equal(await page.locator("dialog[open]").count(), 0);
      await page.route("**/api/goals/clients/**", (route) =>
        route.fulfill({
          status: 200,
          contentType: "application/json",
          body: "[]",
        }),
      );
      await page.goto(origin + "/#goals");
      await page.waitForSelector("main .empty");
      await page.goto(origin + "/#users");
      await page.waitForSelector("main .error");
      results.push({
        role,
        validationPreservesInput: true,
        dialogEscape: true,
        emptyState: true,
        roleDenied: true,
      });
    }
    await context.close();
  }
  await browser.close();
  fs.writeFileSync(
    "artifacts/frontend/browser-review.json",
    JSON.stringify({ results, errors }, null, 2),
  );
  assert.deepEqual(errors, []);
  console.log(
    `${results.length} rendering checks and role-specific mutation checks passed; no browser errors.`,
  );
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
