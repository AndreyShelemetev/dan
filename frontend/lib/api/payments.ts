import { apiFetch } from "./client";

export interface Payment {
  id: number;
  orderId: number;
  status: string;
  statusLabel: string;
  amountRub: number;
  refundedRub: number;
  estimateVersion: number;
  /** Where to send the client to pay. Null once there is nothing left to do. */
  confirmationUrl: string | null;
  paidAt: string | null;
  createdAt: string;
  isActive: boolean;
}

function headers(cookieHeader?: string) {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

export const payments = {
  /** Starts or resumes paying. Repeated calls return the live attempt, never a second charge. */
  start: (orderId: number): Promise<Payment> =>
    apiFetch<Payment>(`/orders/${orderId}/payments`, { method: "POST" }),

  latest: (orderId: number, cookieHeader?: string): Promise<Payment> =>
    apiFetch<Payment>(`/orders/${orderId}/payments/latest`, { headers: headers(cookieHeader) }),

  /** Asks the provider where the payment stands. Coming back from their page proves only that
   *  the client returned — never that they paid. */
  sync: (paymentId: number): Promise<Payment> =>
    apiFetch<Payment>(`/payments/${paymentId}/sync`, { method: "POST" }),

  /** Development only: stands in for finishing on the provider's page. */
  devConfirm: (paymentId: number): Promise<Payment> =>
    apiFetch<Payment>(`/dev/payments/${paymentId}/confirm`, { method: "POST" }),
};
