import { expect, test } from "@playwright/test";
import { ACCOUNTS, accountMenu, signIn } from "./support/login";

/**
 * The header's account controls.
 *
 * These are the parts server-rendered HTML cannot prove: the menu lists its entries only once
 * opened, and signing out is a redirect that happens after a request.
 */
test.describe("account menu", () => {
  test("an admin reaches the admin panel from the header", async ({ page }) => {
    await signIn(page, ACCOUNTS.admin);

    const trigger = accountMenu(page, ACCOUNTS.admin);
    await expect(trigger).toHaveAttribute("aria-expanded", "false");

    await trigger.click();
    await expect(trigger).toHaveAttribute("aria-expanded", "true");

    await page.getByRole("menuitem", { name: "Админка" }).click();
    await expect(page).toHaveURL(/\/admin\/catalog\/?$/);
    await expect(page.getByRole("heading", { name: "Пакеты услуг" })).toBeVisible();
  });

  test("Escape closes the menu and returns focus to the trigger", async ({ page }) => {
    await signIn(page, ACCOUNTS.admin);

    const trigger = accountMenu(page, ACCOUNTS.admin);
    await trigger.click();
    await expect(page.getByRole("menu")).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(page.getByRole("menu")).toBeHidden();
    await expect(trigger).toBeFocused();
  });

  test("a dispatcher is offered the queue but not the catalogue", async ({ page }) => {
    await signIn(page, ACCOUNTS.dispatcher);
    await accountMenu(page, ACCOUNTS.dispatcher).click();

    await expect(page.getByRole("menuitem", { name: "Очередь заказов" })).toBeVisible();
    await expect(page.getByRole("menuitem", { name: "Админка" })).toHaveCount(0);
  });

  test("a client is offered no staff entries", async ({ page }) => {
    await signIn(page, ACCOUNTS.client);
    await accountMenu(page, ACCOUNTS.client).click();

    await expect(page.getByRole("menuitem", { name: "Мои заказы" })).toBeVisible();
    await expect(page.getByRole("menuitem", { name: "Админка" })).toHaveCount(0);
    await expect(page.getByRole("menuitem", { name: "Очередь заказов" })).toHaveCount(0);
  });

  test("signing out lands on the homepage and offers Войти again", async ({ page }) => {
    await signIn(page, ACCOUNTS.client);
    await page.goto("/cabinet/orders/");

    await accountMenu(page, ACCOUNTS.client).click();
    await page.getByRole("menuitem", { name: "Выйти" }).click();

    await expect(page).toHaveURL(/localhost:\d+\/$/);
    await expect(page.getByRole("link", { name: "Войти" })).toBeVisible();
    await expect(accountMenu(page, ACCOUNTS.client)).toHaveCount(0);
  });
});
