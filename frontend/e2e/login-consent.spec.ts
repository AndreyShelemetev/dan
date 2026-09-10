import { expect, test } from "@playwright/test";
import { ACCOUNTS, dismissCookieBanner } from "./support/login";

/**
 * Consent gates the sign-in form.
 *
 * The server refuses registration without it anyway (`legal_not_accepted`), so this is about not
 * letting someone fill in a form and press a button that was always going to fail. The button is
 * disabled, and — because a disabled control that does not say why is a dead end — the reason is
 * on the page and tied to the button for screen readers.
 */
test.describe("login consent gate", () => {
  test("the button is disabled until consent is given", async ({ page }) => {
    await page.goto("/login/");
    await dismissCookieBanner(page);

    const submit = page.getByRole("button", { name: "Получить код" });
    const consent = page.getByRole("checkbox");

    await page.getByLabel("Email").fill(ACCOUNTS.client.email);

    await expect(submit).toBeDisabled();
    await expect(page.getByText("Чтобы продолжить, отметьте согласие выше.")).toBeVisible();

    // The reason is announced with the button, not just printed near it.
    const describedBy = await submit.getAttribute("aria-describedby");
    expect(describedBy).toBe("consent-required");

    await consent.check();
    await expect(submit).toBeEnabled();
    await expect(page.getByText("Чтобы продолжить, отметьте согласие выше.")).toBeHidden();

    // Unchecking puts the gate back — the state follows the checkbox, it is not a one-way latch.
    await consent.uncheck();
    await expect(submit).toBeDisabled();
  });
});
