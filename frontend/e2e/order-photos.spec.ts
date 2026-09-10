import { expect, test } from "@playwright/test";
import { ACCOUNTS, signIn } from "./support/login";

const API = process.env.E2E_API_URL ?? "http://localhost:5100/api/v1";

/**
 * A client attaches photos to their own request, and the dispatcher who prices it can see them.
 *
 * The pair is the point: the photo exists so somebody else can look at the grave without a trip,
 * and a rule that only let the owner read it would make the upload pointless.
 */
test.describe("order photos", () => {
  test("a client attaches a photo and the dispatcher pricing it can see it", async ({ page }) => {
    await signIn(page, ACCOUNTS.client);

    // Arranged through the API, not the order form: this test is about the photos, and an order
    // picked from the client's existing list would be at whatever stage a previous run left it.
    const orderId = await createDraftOrder(page);

    await page.goto(`/cabinet/orders/${orderId}/`);

    const gallery = page.getByRole("region", { name: "Фотографии к заявке" });
    await expect(gallery).toBeVisible();
    await expect(gallery.getByRole("listitem")).toHaveCount(0);

    await gallery
      .locator("input[type=file]")
      .setInputFiles({ name: "grave.png", mimeType: "image/png", buffer: PIXEL });

    // The photo appears only once the API has verified the bytes, stripped EXIF and built a
    // thumbnail — so waiting for it is waiting on the whole pipeline, not just the PUT.
    await expect(gallery.getByRole("listitem")).toHaveCount(1, { timeout: 30_000 });

    // Submit it, so it reaches the dispatcher's queue the way a real request would.
    const submitted = await page.request.post(`${API}/orders/${orderId}/submit`);
    expect(submitted.ok()).toBeTruthy();

    await page.context().clearCookies();
    await signIn(page, ACCOUNTS.dispatcher);
    await page.goto(`/admin/queue/${orderId}/`);

    const staffPhotos = page.getByRole("region", { name: /Фотографии от клиента/ });
    await expect(staffPhotos.getByRole("listitem")).toHaveCount(1);
  });
});

/** Creates a burial site and a draft order against the first published package. */
async function createDraftOrder(page: import("@playwright/test").Page): Promise<number> {
  const cemeteries = await (await page.request.get(`${API}/cemeteries?query=`)).json();
  const packages = await (await page.request.get(`${API}/service-packages`)).json();

  const site = await (
    await page.request.post(`${API}/burial-sites`, {
      data: {
        cemeteryId: cemeteries.data[0].id,
        deceasedFullName: "Петров Пётр Петрович",
        plotSection: "уч. 4, ряд 2",
        landmarks: "второй ряд от центральной аллеи, чёрная ограда",
      },
    })
  ).json();

  const order = await (
    await page.request.post(`${API}/orders`, {
      data: {
        burialSiteId: site.data.id,
        packageCode: packages.data[0].code,
        comment: "Проверка загрузки фотографий",
      },
    })
  ).json();

  return order.data.id as number;
}

/** A 1×1 PNG. The API re-encodes whatever it is given, so the content does not matter — only
 *  that it really is an image. */
const PIXEL = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
  "base64",
);
