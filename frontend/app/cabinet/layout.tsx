import type { ReactNode } from "react";
import { redirect } from "next/navigation";
import { CabinetHeader } from "@/components/cabinet/CabinetHeader";
import { getServerUser } from "@/lib/auth/server";

export const metadata = {
  title: "Кабинет · Память рядом",
};

/**
 * Shell for the signed-in area.
 *
 * middleware.ts already bounces anyone without a session cookie, but that check
 * is deliberately cheap — it only looks at whether a cookie is present, not at
 * whether it still resolves to a live session. A revoked or expired token would
 * sail past it, so the real check happens here, server-side, against the API.
 */
export default async function CabinetLayout({ children }: { children: ReactNode }) {
  const user = await getServerUser();

  if (!user) {
    redirect("/login?redirect=/cabinet");
  }

  return (
    <div className="flex min-h-screen flex-col">
      <CabinetHeader />
      <main id="main" className="mx-auto w-full max-w-content flex-1 px-6 py-10 lg:px-14">
        {children}
      </main>
    </div>
  );
}
