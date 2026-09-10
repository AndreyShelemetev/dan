import Link from "next/link";
import { notFound } from "next/navigation";
import { VisitWorkspace } from "@/components/executor/VisitWorkspace";
import { ApiError } from "@/lib/api/client";
import { MEDIA_OWNER, byPhase, listMedia } from "@/lib/api/media";
import { executorVisits } from "@/lib/api/visits";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

export default async function ExecutorVisitPage({ params }: { params: { id: string } }) {
  const id = Number(params.id);
  if (!Number.isFinite(id)) notFound();

  const cookieHeader = getSessionCookieHeader();

  let visit;
  try {
    visit = await executorVisits.get(id, cookieHeader);
  } catch (err) {
    // 404 covers "no such visit" and "not yours" alike — telling them apart would confirm that
    // somebody else's job exists.
    if (err instanceof ApiError && (err.status === 404 || err.isUnauthorized)) notFound();
    throw err;
  }

  const photos = await listMedia(MEDIA_OWNER.visit, id, cookieHeader).catch(() => []);

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm">
        <Link href="/executor" className="inline-flex min-h-hit items-center text-ink-2 hover:text-accent-deep">
          ← Мои визиты
        </Link>
      </p>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-caps text-ink-2">{visit.orderNumber}</p>
          <h1 className="mt-2 font-display text-2xl font-normal text-ink-1">
            {visit.deceasedFullName}
          </h1>
        </div>
        <span className="rounded-pill border border-accent bg-accent-soft px-4 py-1.5 text-sm font-semibold text-accent-deep">
          {visit.statusLabel}
        </span>
      </div>

      <VisitWorkspace
        visit={visit}
        beforePhotos={byPhase(photos, "before")}
        afterPhotos={byPhase(photos, "after")}
      />
    </div>
  );
}
