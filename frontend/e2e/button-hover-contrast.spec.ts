import { type Page, expect, test } from "@playwright/test";
import { ACCOUNTS, signIn } from "./support/login";

/**
 * A filled button must keep a readable label while the pointer is on it.
 *
 * This has been reported twice: a bare Tailwind colour utility is specificity (0,1,0) and loses
 * to the global `a:hover { color: var(--accent-deep) }` in globals.css (0,1,1), so a ButtonLink —
 * which renders an `<a>` — takes the link hover colour and the label sinks into the fill. The
 * fix is a `hover:text-*` on every variant; this test is what stops it coming back a third time.
 *
 * Ratios are measured in a real browser from computed styles, never estimated.
 */
const PUBLIC_PAGES = [
  "/", "/services/", "/how-it-works/", "/trust/", "/faq/",
  "/login/", "/legal/privacy/", "/legal/cookies/", "/legal/consent/",
];

const SIGNED_IN_PAGES = ["/cabinet/", "/cabinet/orders/", "/cabinet/new/"];

const STAFF_PAGES = ["/admin/queue/", "/admin/qa/", "/admin/catalog/", "/admin/plans/"];

function luminance(rgb: string): number {
  const [r, g, b] = (rgb.match(/\d+(\.\d+)?/g) ?? ["0", "0", "0"]).slice(0, 3).map(Number);
  const f = (c: number) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

test("no filled button loses its label on hover", async ({ page }) => {
  test.setTimeout(180_000);

  const failures = await sweep(page, PUBLIC_PAGES);

  await signIn(page, ACCOUNTS.client);
  failures.push(...(await sweep(page, SIGNED_IN_PAGES)));

  await page.context().clearCookies();
  await signIn(page, ACCOUNTS.admin);
  failures.push(...(await sweep(page, STAFF_PAGES)));

  expect(failures, `\n${failures.join("\n")}\n`).toEqual([]);
});

async function sweep(page: Page, paths: string[]): Promise<string[]> {
  const failures: string[] = [];

  for (const path of paths) {
    const response = await page.goto(path);

    // A 404 sweeps an empty page and passes for the wrong reason — the first version of this
    // test listed four routes that do not exist and reported them as clean.
    if (response && !response.ok()) {
      failures.push(`${path} · not reachable (${response.status()})`);
      continue;
    }

    // Only filled controls: a transparent background inherits the page, and cannot hide its
    // label in a fill it does not have.
    const filled = await page.evaluate(() =>
      [...document.querySelectorAll("a, button")]
        .map((el, index) => ({ el, index }))
        .filter(({ el }) => {
          const s = getComputedStyle(el);
          const box = el.getBoundingClientRect();
          return (
            box.width > 0 &&
            box.height > 0 &&
            (el.textContent ?? "").trim().length > 0 &&
            s.backgroundColor !== "rgba(0, 0, 0, 0)" &&
            s.backgroundColor !== "transparent"
          );
        })
        .map(({ index }) => index),
    );

    for (const index of filled) {
      const control = page.locator("a, button").nth(index);
      await control.scrollIntoViewIfNeeded().catch(() => {});
      await control.hover({ timeout: 5_000 }).catch(() => {});

      const state = await control.evaluate((el) => {
        const s = getComputedStyle(el);
        return {
          color: s.color,
          background: s.backgroundColor,
          text: (el.textContent ?? "").trim().slice(0, 30),
        };
      });

      const ratio = contrast(state.color, state.background);
      if (ratio < 4.5) {
        failures.push(
          `${path} · "${state.text}" · ${state.color} on ${state.background} = ${ratio.toFixed(2)}:1`,
        );
      }
    }
  }

  return failures;
}
