import { apiFetch } from "./client";

/**
 * A dispute as the client who opened it sees it.
 *
 * Mirrors the backend's client-facing shape (`Dtos/Disputes/DisputeDto.cs`): no staff-only
 * fields, only the client's own reason and, once decided, the resolution.
 */
export interface Dispute {
  id: number;
  orderId: number;
  reason: string;
  status: string;
  statusLabel: string;
  resolutionType: string | null;
  resolutionTypeLabel: string | null;
  resolutionText: string | null;
  refundAmountRub: number | null;
  resolvedAt: string | null;
  createdAt: string;
}

function headers(cookieHeader?: string) {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

/** The client's own dispute for this order — whichever is most recent. 404 if there never was
 * one, which a caller treats the same as "no dispute" rather than an error. */
export function getDispute(orderId: number, cookieHeader?: string): Promise<Dispute> {
  return apiFetch<Dispute>(`/orders/${orderId}/dispute`, { headers: headers(cookieHeader) });
}
