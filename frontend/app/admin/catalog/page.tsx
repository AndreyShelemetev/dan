import { CatalogManager } from "@/components/admin/CatalogManager";
import { adminCatalog } from "@/lib/api/adminCatalog";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

export default async function AdminCatalogPage() {
  const packages = await adminCatalog.listPackages(getSessionCookieHeader());
  return <CatalogManager packages={packages} />;
}
