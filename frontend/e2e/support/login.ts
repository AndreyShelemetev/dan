import { type Page, expect } from "@playwright/test";

const API = process.env.E2E_API_URL ?? "http://localhost:5100/api/v1";

/** The development accounts, and the name the header shows for each. */
export const ACCOUNTS = {
  client: { email: "client@pamyat.test", label: "Тестовый клиент" },
  dispatcher: { email: "dispatcher@pamyat.test", label: "Тестовый диспетчер" },
  admin: { email: "admin@pamyat.test", label: "Тестовый администратор" },
} as const;

/**
 * Signs in through the real OTP flow, reading the code back from the development inbox.
 *
 * Not a shortcut past the UI: the session cookie is HttpOnly and issued by the API, so the only
 * honest way to arrive logged in is to actually log in.
 */
export async function signIn(page: Page, account: (typeof ACCOUNTS)[keyof typeof ACCOUNTS]) {
  await page.goto("/login/");
  await dismissCookieBanner(page);

  await page.getByLabel("Email").fill(account.email);
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Получить код" }).click();

  // Wait for the second step before reading the inbox. Fetching it straight after the click
  // races the request that mints the code, and wins often enough to return the previous one.
  const codeField = page.getByLabel("Код из письма");
  await expect(codeField).toBeVisible();

  const inbox = await page.request.get(
    `${API}/dev/last-otp?destination=${encodeURIComponent(account.email)}`,
  );
  const code = (await inbox.json()).data.code as string;

  await codeField.fill(code);
  await page.getByRole("button", { name: "Подтвердить" }).click();

  await expect(accountMenu(page, account)).toBeVisible();
}

/** The banner overlays the foot of every page and will happily swallow a click meant for
 *  something behind it. Answering it once per test is cheaper than dodging it repeatedly. */
export async function dismissCookieBanner(page: Page) {
  const accept = page.getByRole("button", { name: "Только необходимые" });
  if (await accept.isVisible().catch(() => false)) {
    await accept.click();
    await expect(accept).toBeHidden();
  }
}

/** The header's account trigger. Its accessible name is the signed-in person's name. */
export function accountMenu(page: Page, account: (typeof ACCOUNTS)[keyof typeof ACCOUNTS]) {
  return page.getByRole("button", { name: account.label });
}
