import { apiFetch } from "./client";

export interface EstimateLine {
  /** work | material | discount | delivery | extra */
  type: string;
  title: string;
  quantity: number;
  unit: string | null;
  unitPriceRub: number;
  totalRub: number;
}

export interface Estimate {
  version: number;
  /** draft | published | accepted | rejected | superseded */
  status: string;
  totalRub: number;
  validUntil: string | null;
  note: string | null;
  publishedAt: string | null;
  acceptedAt: string | null;
  rejectedAt: string | null;
  lines: EstimateLine[];
}

export interface OrderHistoryEntry {
  toStatus: string;
  label: string;
  reason: string | null;
  createdAt: string;
}

export interface OrderSummary {
  id: number;
  number: string;
  status: string;
  /** What the client is told — internal statuses describe our process, not their situation. */
  statusLabel: string;
  /** The one action expected of them now, or null when the ball is on our side. */
  cta: string | null;
  packageTitle: string;
  deceasedFullName: string;
  preferredFrom: string | null;
  preferredTo: string | null;
  createdAt: string;
}

export interface Order extends OrderSummary {
  burialSiteId: number;
  packageCode: string;
  packageVersion: string;
  packagePriceFromRub: number;
  warrantyDays: number;
  warrantyUntil: string | null;
  comment: string | null;
  cancellationReason: string | null;
  submittedAt: string | null;
  paidAt: string | null;
  completedAt: string | null;
  estimates: Estimate[];
  history: OrderHistoryEntry[];
}

export interface CreateOrderInput {
  burialSiteId: number;
  packageCode: string;
  preferredFrom?: string | null;
  preferredTo?: string | null;
  comment?: string | null;
}

function headers(cookieHeader?: string): HeadersInit | undefined {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

export const orders = {
  list: (cookieHeader?: string) =>
    apiFetch<OrderSummary[]>("/orders", { headers: headers(cookieHeader) }),

  get: (id: number, cookieHeader?: string) =>
    apiFetch<Order>(`/orders/${id}`, { headers: headers(cookieHeader) }),

  create: (body: CreateOrderInput) => apiFetch<Order>("/orders", { method: "POST", body }),

  submit: (id: number) => apiFetch<Order>(`/orders/${id}/submit`, { method: "POST" }),

  cancel: (id: number, reason?: string) =>
    apiFetch<Order>(`/orders/${id}/cancel`, { method: "POST", body: { reason } }),

  /** The version is required, never inferred: agreeing to "the current estimate" is not agreeing
   *  to a set of lines. */
  acceptEstimate: (id: number, version: number) =>
    apiFetch<Order>(`/orders/${id}/estimate-acceptance`, { method: "POST", body: { version } }),

  rejectEstimate: (id: number, version: number, reason?: string) =>
    apiFetch<Order>(`/orders/${id}/estimate-rejection`, { method: "POST", body: { version, reason } }),
};

/** The estimate the client is being asked to decide on, if any. */
export function pendingEstimate(order: Order): Estimate | null {
  return order.estimates.find((e) => e.status === "published") ?? null;
}

/** What was agreed, once they have agreed to something. */
export function acceptedEstimate(order: Order): Estimate | null {
  return order.estimates.find((e) => e.status === "accepted") ?? null;
}

export const LINE_TYPE_LABEL: Record<string, string> = {
  work: "Работа",
  material: "Материалы",
  discount: "Скидка",
  delivery: "Логистика",
  extra: "Допработа",
};
