import { apiFetch } from "./client";

/**
 * Mirrors the backend's BurialSiteDto
 * (backend/src/PamyatRyadom.Api/Dtos/BurialSites/BurialSiteDtos.cs).
 *
 * Life dates are strings, not Dates, on purpose — the backend stores what the
 * family actually knows ("около 1943", "март 1998 (по документам 1997)"), and
 * parsing that into a calendar date would either fail or invent precision.
 */
export interface BurialSite {
  id: number;
  cemeteryId: number;
  cemeteryName: string | null;
  deceasedFullName: string;
  birthDateText: string | null;
  deathDateText: string | null;
  plotSection: string | null;
  landmarks: string | null;
  geoLat: number | null;
  geoLng: number | null;
  notes: string | null;
  /** "unverified" | "sufficient" | "insufficient" — drives whether an inspection is offered. */
  locationQuality: string;
  /** What the caller may do here: "view" | "order" | "manage". */
  permission: string;
  isOwner: boolean;
  photoCount: number;
  createdAt: string;
}

export interface Cemetery {
  id: number;
  name: string;
  region: string | null;
  address: string | null;
  hours: string | null;
}

export interface BurialSiteMember {
  id: number;
  userId: number | null;
  /** Masked unless the caller manages the record. */
  contact: string | null;
  permission: string;
  accepted: boolean;
  acceptedAt: string | null;
  createdAt: string;
}

export interface CreateBurialSiteInput {
  cemeteryId: number;
  deceasedFullName: string;
  birthDateText?: string | null;
  deathDateText?: string | null;
  plotSection?: string | null;
  landmarks?: string | null;
  notes?: string | null;
}

/** The invitation token is returned exactly once, at creation — only its hash is stored. */
export interface InvitationCreated {
  member: BurialSiteMember;
  token: string;
}

export const LOCATION_QUALITY = {
  unverified: "unverified",
  sufficient: "sufficient",
  insufficient: "insufficient",
} as const;

export const PERMISSION = {
  view: "view",
  order: "order",
  manage: "manage",
} as const;

/** Whether this permission allows placing an order — i.e. spending money. */
export function canOrder(permission: string): boolean {
  return permission === PERMISSION.order || permission === PERMISSION.manage;
}

export function canManage(permission: string): boolean {
  return permission === PERMISSION.manage;
}

function cookieHeaders(cookieHeader?: string): HeadersInit | undefined {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

export function listBurialSites(cookieHeader?: string): Promise<BurialSite[]> {
  return apiFetch<BurialSite[]>("/burial-sites", { headers: cookieHeaders(cookieHeader) });
}

export function getBurialSite(id: number, cookieHeader?: string): Promise<BurialSite> {
  return apiFetch<BurialSite>(`/burial-sites/${id}`, { headers: cookieHeaders(cookieHeader) });
}

export function createBurialSite(input: CreateBurialSiteInput): Promise<BurialSite> {
  return apiFetch<BurialSite>("/burial-sites", { method: "POST", body: input });
}

export function updateBurialSite(
  id: number,
  input: Partial<Omit<CreateBurialSiteInput, "cemeteryId">>,
): Promise<BurialSite> {
  return apiFetch<BurialSite>(`/burial-sites/${id}`, { method: "PATCH", body: input });
}

export function deleteBurialSite(id: number): Promise<{ deleted: boolean }> {
  return apiFetch<{ deleted: boolean }>(`/burial-sites/${id}`, { method: "DELETE" });
}

export function listCemeteries(cookieHeader?: string): Promise<Cemetery[]> {
  return apiFetch<Cemetery[]>("/cemeteries", { headers: cookieHeaders(cookieHeader) });
}

export function listMembers(siteId: number, cookieHeader?: string): Promise<BurialSiteMember[]> {
  return apiFetch<BurialSiteMember[]>(`/burial-sites/${siteId}/members`, {
    headers: cookieHeaders(cookieHeader),
  });
}

export function inviteMember(
  siteId: number,
  contact: string,
  permission: string,
): Promise<InvitationCreated> {
  return apiFetch<InvitationCreated>(`/burial-sites/${siteId}/members`, {
    method: "POST",
    body: { contact, permission },
  });
}

export function revokeMember(siteId: number, memberId: number): Promise<{ revoked: boolean }> {
  return apiFetch<{ revoked: boolean }>(`/burial-sites/${siteId}/members/${memberId}`, {
    method: "DELETE",
  });
}

export function acceptInvitation(token: string): Promise<BurialSite> {
  return apiFetch<BurialSite>("/burial-sites/invitations/accept", {
    method: "POST",
    body: { token },
  });
}
