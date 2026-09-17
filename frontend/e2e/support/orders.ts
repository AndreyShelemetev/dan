import type { Page } from "@playwright/test";

const API = process.env.E2E_API_URL ?? "http://localhost:5100/api/v1";

/**
 * Creates a burial site and a draft order against the first published package, then submits it
 * to the dispatcher's queue.
 *
 * Arranged through the API, not the order form: a spec about what happens to an order after it
 * is submitted has no business also re-testing how the request form builds one.
 */
export async function createSubmittedOrder(
  page: Page,
): Promise<{ id: number; number: string }> {
  const cemeteries = await (await page.request.get(`${API}/cemeteries?query=`)).json();
  const packages = await (await page.request.get(`${API}/service-packages`)).json();

  const site = await (
    await page.request.post(`${API}/burial-sites`, {
      data: {
        cemeteryId: cemeteries.data[0].id,
        deceasedFullName: "Сидоров Сидор Сидорович",
        plotSection: "уч. 7, ряд 1",
        landmarks: "у главной аллеи, синяя ограда",
      },
    })
  ).json();

  const order = await (
    await page.request.post(`${API}/orders`, {
      data: {
        burialSiteId: site.data.id,
        packageCode: packages.data[0].code,
        comment: "Проверка денежного пути",
      },
    })
  ).json();

  const submitted = await page.request.post(`${API}/orders/${order.data.id}/submit`);
  if (!submitted.ok()) {
    throw new Error(`Could not submit the arranged order: HTTP ${submitted.status()}`);
  }

  return { id: order.data.id as number, number: order.data.number as string };
}

/** A 1×1 PNG. The API re-encodes whatever it is given, so the content does not matter — only
 *  that it really is an image. */
export const PIXEL_PNG = {
  name: "grave.png",
  mimeType: "image/png",
  buffer: Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
    "base64",
  ),
};
