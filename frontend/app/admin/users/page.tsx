import { UsersManager } from "@/components/admin/UsersManager";
import { adminUsers } from "@/lib/api/adminUsers";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";
import { getServerUser } from "@/lib/auth/server";
import { notFound } from "next/navigation";

export const dynamic = "force-dynamic";

/** Same admin-only gate as `/admin/catalog`: who can sign in as what is not a dispatcher's call.
 *  The API refuses anyone else anyway; this turns that refusal into a 404 rather than a broken
 *  page. */
async function requireAdmin() {
  const user = await getServerUser();
  if (!user || !["admin", "superadmin"].includes(user.role)) notFound();
}

export default async function AdminUsersPage() {
  await requireAdmin();
  const users = await adminUsers.list(undefined, getSessionCookieHeader());
  return <UsersManager users={users} />;
}
