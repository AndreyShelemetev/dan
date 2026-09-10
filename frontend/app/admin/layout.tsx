import type { ReactNode } from "react";
import { notFound, redirect } from "next/navigation";
import { AdminNav } from "@/components/admin/AdminNav";
import { getServerUser } from "@/lib/auth/server";

export const metadata = { title: "Админка · Память рядом" };

const ADMIN_ROLES = ["admin", "superadmin"];

/** Everyone who works the operational side. The queue is a dispatcher's day job; the catalogue
 *  is not, and each page narrows further from here. */
const STAFF_ROLES = ["dispatcher", "qa", "support", "finance", ...ADMIN_ROLES];

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
    // The queue, not the catalogue: it is the one admin page every staff role can open, so
    // signing in from here lands on a page rather than on a 404.
    redirect("/login?redirect=/admin/queue");
  }

  if (!STAFF_ROLES.includes(user.role)) {
    notFound();
  }

  return (
    <div className="flex min-h-screen flex-col">
      <AdminNav isAdmin={ADMIN_ROLES.includes(user.role)} />
      <main id="main" className="mx-auto w-full max-w-content flex-1 px-6 py-8 lg:px-14">
        {children}
      </main>
    </div>
  );
}
