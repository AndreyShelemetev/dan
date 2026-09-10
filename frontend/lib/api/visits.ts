import { apiFetch } from "./client";

export interface ChecklistItem {
  key: string;
  title: string;
  optional: boolean;
  result: string;
  resultLabel: string;
  note: string | null;
  sortOrder: number;
}

/**
 * The report as the client sees it.
 *
 * Mirrors the server's client-facing shape, which deliberately omits the executor's payout and
 * QA's internal note. Neither is filtered here — neither is ever sent.
 */
export interface VisitReport {
  id: number;
  status: string;
  statusLabel: string;
  visitedOn: string | null;
  note: string | null;
  checklist: ChecklistItem[];
}

export interface Visit extends VisitReport {
  orderId: number;
  orderNumber: string;
  executorUserId: number | null;
  executorName: string | null;
  scheduledFor: string | null;
  offerExpiresAt: string | null;
  submittedAt: string | null;
  reviewedAt: string | null;
  executorNote: string | null;
  reviewNote: string | null;
  declineReason: string | null;
  /** Commercially confidential — staff and the executor themselves only. */
  payoutRub: number | null;
  deceasedFullName: string;
  cemeteryName: string | null;
  plotSection: string | null;
  landmarks: string | null;
}

function headers(cookieHeader?: string) {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

/** The client's report. 404 until QA has approved it — an unreviewed report is a claim. */
export function getReport(orderId: number, cookieHeader?: string): Promise<VisitReport> {
  return apiFetch<VisitReport>(`/orders/${orderId}/report`, { headers: headers(cookieHeader) });
}

export const orderActions = {
  acceptWork: (orderId: number): Promise<unknown> =>
    apiFetch(`/orders/${orderId}/acceptance`, { method: "POST" }),

  dispute: (orderId: number, reason: string): Promise<unknown> =>
    apiFetch(`/orders/${orderId}/dispute`, { method: "POST", body: { reason } }),
};

export const executorVisits = {
  list: (cookieHeader?: string): Promise<Visit[]> =>
    apiFetch<Visit[]>("/executor/visits", { headers: headers(cookieHeader) }),

  get: (id: number, cookieHeader?: string): Promise<Visit> =>
    apiFetch<Visit>(`/executor/visits/${id}`, { headers: headers(cookieHeader) }),

  accept: (id: number): Promise<Visit> =>
    apiFetch<Visit>(`/executor/visits/${id}/accept`, { method: "POST" }),

  decline: (id: number, note: string): Promise<Visit> =>
    apiFetch<Visit>(`/executor/visits/${id}/decline`, { method: "POST", body: { note } }),

  start: (id: number): Promise<Visit> =>
    apiFetch<Visit>(`/executor/visits/${id}/start`, { method: "POST" }),

  submit: (
    id: number,
    body: { note: string | null; checklist: { key: string; result: string; note: string | null }[] },
  ): Promise<Visit> => apiFetch<Visit>(`/executor/visits/${id}/report`, { method: "POST", body }),
};

export interface ExecutorOption {
  id: number;
  displayName: string;
}

export const qa = {
  executors: (cookieHeader?: string): Promise<ExecutorOption[]> =>
    apiFetch<ExecutorOption[]>("/admin/executors", { headers: headers(cookieHeader) }),

  forOrder: (orderId: number, cookieHeader?: string): Promise<Visit[]> =>
    apiFetch<Visit[]>(`/admin/orders/${orderId}/visits`, { headers: headers(cookieHeader) }),

  queue: (cookieHeader?: string): Promise<Visit[]> =>
    apiFetch<Visit[]>("/admin/qa/queue", { headers: headers(cookieHeader) }),

  approve: (visitId: number): Promise<Visit> =>
    apiFetch<Visit>(`/admin/visits/${visitId}/approve`, { method: "POST" }),

  sendBack: (visitId: number, note: string): Promise<Visit> =>
    apiFetch<Visit>(`/admin/visits/${visitId}/rework`, { method: "POST", body: { note } }),

  assign: (
    orderId: number,
    body: { executorUserId: number; scheduledFor?: string | null; payoutRub?: number | null },
  ): Promise<Visit> => apiFetch<Visit>(`/admin/orders/${orderId}/visits`, { method: "POST", body }),
};
