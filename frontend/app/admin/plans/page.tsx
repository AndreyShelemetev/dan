import { PlansManager } from "@/components/admin/PlansManager";
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

export default async function AdminPlansPage() {
  await requireAdmin();
  const cookieHeader = getSessionCookieHeader();
  const [plans, packages] = await Promise.all([
    adminCatalog.listPlans(cookieHeader),
    adminCatalog.listPackages(cookieHeader),
  ]);

  // Only published packages: a plan pointing at a draft would create visits with no checklist to
  // measure them against, which is exactly what the publish guard on the backend refuses.
  const packageCodes = [
    ...new Set(packages.filter((p) => p.status === "published").map((p) => p.code)),
  ];

  return <PlansManager plans={plans} packageCodes={packageCodes} />;
}
