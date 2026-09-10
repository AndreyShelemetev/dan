import { CatalogManager } from "@/components/admin/CatalogManager";
import { adminCatalog } from "@/lib/api/adminCatalog";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";
import { getServerUser } from "@/lib/auth/server";
import { notFound } from "next/navigation";

export const dynamic = "force-dynamic";

/** Catalogue editing is admin-only: prices and package composition are the commercial terms of
 *  the contract with every client. The API refuses anyone else anyway; this turns that refusal
 *  into a 404 rather than a broken page. */
async function requireAdmin() {
  const user = await getServerUser();
  if (!user || !["admin", "superadmin"].includes(user.role)) notFound();
}

export default async function AdminCatalogPage() {
  await requireAdmin();
  const packages = await adminCatalog.listPackages(getSessionCookieHeader());
  return <CatalogManager packages={packages} />;
}
