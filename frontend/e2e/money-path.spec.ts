import { expect, test } from "@playwright/test";
import { ACCOUNTS, signIn } from "./support/login";
import { createSubmittedOrder, PIXEL_PNG } from "./support/orders";

const API = process.env.E2E_API_URL ?? "http://localhost:5100/api/v1";

/** Distinctive enough that it cannot be confused with the price, the quantity or any other
 *  number already on the page — which is the point of picking it. */
const PAYOUT_RUB = "4321";

/** The order's own status, read straight from the API. `OrderStatusPresentation` reuses the same
 *  label for the status badge and for the matching history entry, so a same-page text match is
 *  ambiguous by construction — the status code is not. */
async function orderStatus(page: import("@playwright/test").Page, orderId: number): Promise<string> {
  const body = await (await page.request.get(`${API}/orders/${orderId}`)).json();
  return body.data.status as string;
}

/** The visit's own status, from the executor's side of the API — usable while signed in as the
 *  executor, unlike {@link orderStatus} which only the order's owning client can read. */
async function visitStatus(page: import("@playwright/test").Page, visitId: number): Promise<string> {
  const body = await (await page.request.get(`${API}/executor/visits/${visitId}`)).json();
  return body.data.status as string;
}

/**
 * The whole money path, on the stub provider: a request is priced, accepted, paid, dispatched,
 * photographed, reviewed and closed — and the executor's payout never reaches the client.
 *
 * Every step here is a different role signing in, because that is what actually moves an order
 * forward in this product; a client never sees or picks an executor, and only QA may show a
 * report to a client. Breaking any single handoff should turn this one spec red.
 */
test.describe("money path", () => {
  test("a request is priced, paid, dispatched, reported, reviewed and accepted", async ({ page }) => {
    await signIn(page, ACCOUNTS.client);
    const order = await createSubmittedOrder(page);

    // The dispatcher prices the request and publishes it to the client.
    await page.context().clearCookies();
    await signIn(page, ACCOUNTS.dispatcher);
    await page.goto(`/admin/queue/${order.id}/`);

    await page.getByLabel("Название строки 1").fill("Уборка и мойка памятника");
    await page.getByLabel("Цена строки 1").fill("3000");
    await page.getByRole("button", { name: "Опубликовать клиенту" }).click();
    await expect(page.getByText("ждёт решения клиента")).toBeVisible();

    // The client accepts the estimate and pays through the redirect. Coming back from the
    // provider must not be what marks the order paid (BR-007, D09) — only the explicit stub
    // confirmation below does.
    await page.context().clearCookies();
    await signIn(page, ACCOUNTS.client);
    await page.goto(`/cabinet/orders/${order.id}/`);

    await page.getByRole("button", { name: "Принять смету" }).click();
    await expect(page.getByRole("heading", { name: "Оплата" })).toBeVisible();

    await page.getByRole("button", { name: "Перейти к оплате" }).click();
    await page.waitForURL(/\?stub_payment=/);
    await expect(page.getByRole("heading", { name: "Оплата" })).toBeVisible();

    await page.getByRole("button", { name: "Подтвердить оплату (тест)" }).click();
    await expect(page.getByRole("heading", { name: "Оплата" })).toBeHidden();
    await expect.poll(() => orderStatus(page, order.id)).toBe("paid");

    // The dispatcher assigns an executor. The client never chooses or sees this — the payout is
    // entered here and nowhere the client can reach.
    await page.context().clearCookies();
    await signIn(page, ACCOUNTS.dispatcher);
    await page.goto(`/admin/queue/${order.id}/`);

    await page.getByLabel("Исполнитель").selectOption({ label: ACCOUNTS.executor.label });
    await page.getByLabel("Вознаграждение, ₽").fill(PAYOUT_RUB);
    await page.getByRole("button", { name: "Предложить визит" }).click();
    // "Визит" only replaces the assignment form once the offer exists.
    await expect(page.getByRole("heading", { name: "Визит" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Предложить визит" })).toBeHidden();

    // The executor accepts the visit, arrives, photographs the grave and files the report.
    await page.context().clearCookies();
    await signIn(page, ACCOUNTS.executor);

    const visits = await (await page.request.get(`${API}/executor/visits`)).json();
    const visit = (visits.data as Array<{ id: number; orderId: number }>).find(
      (v) => v.orderId === order.id,
    );
    if (!visit) throw new Error("Assigned visit did not reach the executor's queue.");

    await page.goto(`/executor/${visit.id}/`);
    await page.getByRole("button", { name: "Принять визит" }).click();
    await page.getByRole("button", { name: "Я на месте" }).click();

    const before = page.getByRole("region", { name: "Фотографии «до»" });
    await before.locator("input[type=file]").setInputFiles(PIXEL_PNG);
    await expect(before.getByRole("listitem")).toHaveCount(1, { timeout: 30_000 });

    const after = page.getByRole("region", { name: "Фотографии «после»" });
    await after.locator("input[type=file]").setInputFiles(PIXEL_PNG);
    await expect(after.getByRole("listitem")).toHaveCount(1, { timeout: 30_000 });

    // A concurrent request can still race the session cookie's rotate-on-use and abort this
    // exact POST in flight, which bounces the tab to /login before the button ever reflects it —
    // so success is confirmed against the visit's own status, not trusted from the UI, and the
    // whole action is retried (re-authenticating first, if the bounce happened) rather than
    // trusting a "hidden" that can be true for the wrong reason.
    let submitted = false;
    for (let attempt = 0; attempt < 3 && !submitted; attempt++) {
      await page.getByRole("button", { name: "Отправить отчёт" }).click();
      try {
        await expect.poll(() => visitStatus(page, visit.id), { timeout: 8_000 }).toBe("submitted");
        submitted = true;
      } catch {
        await page.context().clearCookies();
        await signIn(page, ACCOUNTS.executor);
        await page.goto(`/executor/${visit.id}/`);
      }
    }
    if (!submitted) throw new Error("Executor report never reached 'submitted' after retries.");

    // QA reviews the report and passes it on. The queue is shared by every visit awaiting
    // review, so the card is picked out by this order's own number.
    await page.context().clearCookies();
    await signIn(page, ACCOUNTS.qa);
    await page.goto("/admin/qa/");

    const reviewCard = page.locator("article", { hasText: order.number });
    await expect(reviewCard).toBeVisible();
    await reviewCard.getByRole("button", { name: "Принять и показать клиенту" }).click();
    await expect(reviewCard).toBeHidden();

    // The client sees the report and closes the order. The payout that was entered above must
    // not be anywhere in what the client's browser received.
    await page.context().clearCookies();
    await signIn(page, ACCOUNTS.client);

    const reportResponse = await page.request.get(`${API}/orders/${order.id}/report`);
    expect(reportResponse.ok()).toBeTruthy();
    const reportBody = await reportResponse.text();
    expect(reportBody).not.toContain(PAYOUT_RUB);
    expect(reportBody.toLowerCase()).not.toContain("payout");

    await page.goto(`/cabinet/orders/${order.id}/`);
    await expect(page.getByRole("heading", { name: "Отчёт о визите" })).toBeVisible();

    await page.getByRole("button", { name: "Принять работу" }).click();
    // The report itself stays visible after acceptance (BR-012's warranty window still needs
    // it) — only the accept/dispute decision disappears.
    await expect(page.getByRole("button", { name: "Принять работу" })).toBeHidden();
    await expect.poll(() => orderStatus(page, order.id)).toBe("completed");
  });
});
