import { PlansManager } from "@/components/admin/PlansManager";
import { adminCatalog } from "@/lib/api/adminCatalog";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

export default async function AdminPlansPage() {
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
