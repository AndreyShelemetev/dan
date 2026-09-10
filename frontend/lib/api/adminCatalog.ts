import { apiFetch } from "./client";

export interface ChecklistItemInput {
  key?: string | null;
  title: string;
  /** An optional item may be skipped without a reason; a required one may not. */
  optional: boolean;
}

export interface RequiredMediaInput {
  /** "before" | "process" | "after" */
  phase: string;
  minCount: number;
  description?: string | null;
}

export interface AdminPackage {
  id: number;
  code: string;
  version: string;
  title: string;
  summary: string;
  includes: string[];
  limits: string[];
  priceFromRub: number;
  warrantyDays: number;
  visitsLabel: string | null;
  sortOrder: number;
  /** "draft" | "published" | "archived" */
  status: string;
  publishedAt: string | null;
  /** False once published — the server refuses edits, so the form goes read-only rather than
   *  letting someone type into fields that cannot be saved. */
  editable: boolean;
  checklistItems: ChecklistItemInput[];
  requiredMedia: RequiredMediaInput[];
}

export interface SavePackage {
  code: string;
  version: string;
  title: string;
  summary?: string;
  includes?: string[];
  limits?: string[];
  priceFromRub: number;
  warrantyDays: number;
  visitsLabel?: string | null;
  sortOrder: number;
  checklistItems?: ChecklistItemInput[];
  requiredMedia?: RequiredMediaInput[];
}

export interface AdminPlan {
  id: number;
  code: string;
  version: string;
  title: string;
  summary: string;
  servicePackageCode: string;
  visitsTotal: number;
  periodMonths: number;
  priceRub: number;
  pricePerVisit: number;
  sortOrder: number;
  status: string;
  publishedAt: string | null;
  editable: boolean;
}

export interface SavePlan {
  code: string;
  version: string;
  title: string;
  summary?: string;
  servicePackageCode: string;
  visitsTotal: number;
  periodMonths: number;
  priceRub: number;
  sortOrder: number;
}

const base = "/admin/catalog";

function headers(cookieHeader?: string): HeadersInit | undefined {
  return cookieHeader ? { Cookie: cookieHeader } : undefined;
}

export const adminCatalog = {
  listPackages: (cookieHeader?: string) =>
    apiFetch<AdminPackage[]>(`${base}/packages`, { headers: headers(cookieHeader) }),

  createPackage: (body: SavePackage) =>
    apiFetch<AdminPackage>(`${base}/packages`, { method: "POST", body }),

  updatePackage: (id: number, body: SavePackage) =>
    apiFetch<AdminPackage>(`${base}/packages/${id}`, { method: "PATCH", body }),

  publishPackage: (id: number) =>
    apiFetch<AdminPackage>(`${base}/packages/${id}/publish`, { method: "POST" }),

  archivePackage: (id: number) =>
    apiFetch<AdminPackage>(`${base}/packages/${id}/archive`, { method: "POST" }),

  /** Only a draft nothing was sold under. Published versions are archived instead — an order
   *  pointing at one has to keep pointing at something. */
  deletePackage: (id: number) =>
    apiFetch<{ deleted: boolean }>(`${base}/packages/${id}`, { method: "DELETE" }),

  /** Copies a version into a new draft — the supported way to change what is already on sale. */
  newPackageVersion: (id: number, version: string) =>
    apiFetch<AdminPackage>(`${base}/packages/${id}/versions`, { method: "POST", body: { version } }),

  listPlans: (cookieHeader?: string) =>
    apiFetch<AdminPlan[]>(`${base}/plans`, { headers: headers(cookieHeader) }),

  createPlan: (body: SavePlan) => apiFetch<AdminPlan>(`${base}/plans`, { method: "POST", body }),

  updatePlan: (id: number, body: SavePlan) =>
    apiFetch<AdminPlan>(`${base}/plans/${id}`, { method: "PATCH", body }),

  publishPlan: (id: number) => apiFetch<AdminPlan>(`${base}/plans/${id}/publish`, { method: "POST" }),

  archivePlan: (id: number) => apiFetch<AdminPlan>(`${base}/plans/${id}/archive`, { method: "POST" }),

  deletePlan: (id: number) => apiFetch<{ deleted: boolean }>(`${base}/plans/${id}`, { method: "DELETE" }),
};

export const STATUS_LABEL: Record<string, string> = {
  draft: "Черновик",
  published: "Опубликован",
  archived: "В архиве",
};
