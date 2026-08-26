import { apiFetch } from "./client";

/** Mirrors ServicePackageDto (backend/src/PamyatRyadom.Api/Dtos/Catalog). */
export interface ServicePackage {
  code: string;
  /** The version an order created now binds to. */
  version: string;
  title: string;
  summary: string;
  includes: string[];
  /** Shown next to the price, never behind a tooltip — a limit discovered after payment is the
   *  complaint this product exists to avoid. */
  limits: string[];
  priceFromRub: number;
  warrantyDays: number;
  visitsLabel: string | null;
}

export function listServicePackages(cookieHeader?: string): Promise<ServicePackage[]> {
  return apiFetch<ServicePackage[]>("/service-packages", {
    headers: cookieHeader ? { Cookie: cookieHeader } : undefined,
  });
}

/** Roubles with a non-breaking thousands space, as the design renders prices. */
export function formatRub(value: number): string {
  return new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 }).format(value);
}
