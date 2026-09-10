import type { ReactNode } from "react";
import { notFound, redirect } from "next/navigation";
import { CabinetHeader } from "@/components/cabinet/CabinetHeader";
import { getServerUser } from "@/lib/auth/server";

export const metadata = { title: "Мои визиты · Память рядом" };

/**
 * The executor's area.
 *
 * Executors are not staff: they see their own assigned work and nothing else — no queue, no
 * catalogue, no other executor's visits. The API scopes every read to the caller, so this gate is
 * the convenience layer rather than the protection.
 */
export default async function ExecutorLayout({ children }: { children: ReactNode }) {
  const user = await getServerUser();

  if (!user) {
    redirect("/login?redirect=/executor");
  }

  if (user.role !== "executor") {
    notFound();
  }

  return (
    <div className="flex min-h-screen flex-col bg-paper">
      <CabinetHeader />
      <main id="main" className="mx-auto w-full max-w-content flex-1 px-6 py-8 lg:px-14">
        {children}
      </main>
    </div>
  );
}
