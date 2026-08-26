import type { ReactNode } from "react";
import { notFound, redirect } from "next/navigation";
import { AdminNav } from "@/components/admin/AdminNav";
import { getServerUser } from "@/lib/auth/server";

export const metadata = { title: "Админка · Память рядом" };

const ADMIN_ROLES = ["admin", "superadmin"];

/**
 * Role gate for the admin area.
 *
 * Signed-out visitors go to the sign-in page; signed-in accounts without the role get a 404
 * rather than a "forbidden" screen. A 403 would confirm the admin area exists and that this
 * account is simply not in it — an invitation to go looking. Every endpoint behind it is
 * independently role-checked, so this is the convenience layer, not the protection.
 */
export default async function AdminLayout({ children }: { children: ReactNode }) {
  const user = await getServerUser();

  if (!user) {
    redirect("/login?redirect=/admin/catalog");
  }

  if (!ADMIN_ROLES.includes(user.role)) {
    notFound();
  }

  return (
    <div className="flex min-h-screen flex-col">
      <AdminNav />
      <main id="main" className="mx-auto w-full max-w-content flex-1 px-6 py-8 lg:px-14">
        {children}
      </main>
    </div>
  );
}
