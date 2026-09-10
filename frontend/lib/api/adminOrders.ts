import { apiFetch } from "./client";
import type { Order, OrderSummary } from "./orders";

export interface SaveEstimateLine {
  /** work | material | discount | delivery | extra */
  type: string;
  title: string;
  quantity: number;
  unit?: string | null;
  /** Negative only on a discount line. */
  unitPriceRub: number;
}

export interface SaveEstimate {
  note?: string | null;
  validUntil?: string | null;
  lines: SaveEstimateLine[];
}

function headers(cookieHeader?: string): HeadersInit | undefined {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

export const adminOrders = {
  /** Without a status, returns the orders that actually need a person — anything terminal or
   *  already waiting on the client is noise for someone working through a queue. */
  queue: (status?: string, cookieHeader?: string) =>
    apiFetch<OrderSummary[]>(`/admin/orders${status ? `?status=${encodeURIComponent(status)}` : ""}`, {
      headers: headers(cookieHeader),
    }),

  /** Staff view: unlike the client's, it includes draft estimates. */
  get: (id: number, cookieHeader?: string) =>
    apiFetch<Order>(`/admin/orders/${id}`, { headers: headers(cookieHeader) }),

  createDraft: (id: number, body: SaveEstimate) =>
    apiFetch<unknown>(`/admin/orders/${id}/estimates`, { method: "POST", body }),

  updateDraft: (id: number, version: number, body: SaveEstimate) =>
    apiFetch<unknown>(`/admin/orders/${id}/estimates/${version}`, { method: "PATCH", body }),

  publish: (id: number, version: number) =>
    apiFetch<Order>(`/admin/orders/${id}/estimates/${version}/publish`, { method: "POST" }),
};
