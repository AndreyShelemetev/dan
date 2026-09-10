import Link from "next/link";
import { Card } from "@/components/ui/Card";
import { executorVisits } from "@/lib/api/visits";
import { formatRub } from "@/lib/api/catalog";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

/** Work still to do comes first. A finished visit is a record; an offer expiring in four hours
 *  is the thing that needs looking at. */
const ACTIVE = ["offered", "accepted", "in_progress", "rework"];

export default async function ExecutorVisitsPage() {
  const visits = await executorVisits.list(getSessionCookieHeader());
  const active = visits.filter((v) => ACTIVE.includes(v.status));
  const done = visits.filter((v) => !ACTIVE.includes(v.status));

  return (
    <div className="flex flex-col gap-8">
      <div>
        <p className="text-sm text-ink-2">Ваша работа</p>
        <h1 className="mt-1 font-display text-2xl font-normal text-ink-1">Мои визиты</h1>
      </div>

      <section className="flex flex-col gap-3">
        <h2 className="font-display text-lg font-normal text-ink-1">
          В работе <span className="tabular-nums text-ink-2">({active.length})</span>
        </h2>
        {active.length === 0 ? (
          <Card>
            <p className="text-sm text-ink-2">
              Сейчас заданий нет. Когда диспетчер предложит визит, он появится здесь.
            </p>
          </Card>
        ) : (
          <ul className="flex list-none flex-col gap-3 p-0">
            {active.map((visit) => (
              <li key={visit.id}>
                <Card as="article" className="flex flex-wrap items-baseline justify-between gap-3">
                  <div>
                    <h3 className="font-display text-lg font-normal text-ink-1">
                      <Link href={`/executor/${visit.id}`} className="text-ink-1 no-underline hover:text-accent-deep">
                        {visit.cemeteryName ?? "Кладбище не указано"}
                      </Link>
                    </h3>
                    <p className="mt-1 text-sm text-ink-2">
                      {visit.deceasedFullName}
                      {visit.plotSection ? ` · ${visit.plotSection}` : ""} · {visit.orderNumber}
                    </p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm text-ink-1">{visit.statusLabel}</p>
                    {visit.payoutRub !== null ? (
                      <p className="mt-0.5 text-sm tabular-nums text-ink-2">
                        {formatRub(visit.payoutRub)} ₽
                      </p>
                    ) : null}
                  </div>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </section>

      {done.length > 0 ? (
        <section className="flex flex-col gap-3">
          <h2 className="font-display text-lg font-normal text-ink-1">Завершённые</h2>
          <ul className="flex list-none flex-col gap-2 p-0">
            {done.map((visit) => (
              <li key={visit.id} className="flex flex-wrap items-baseline justify-between gap-3 border-b border-border py-2">
                <Link href={`/executor/${visit.id}`} className="text-base text-ink-1 no-underline hover:text-accent-deep">
                  {visit.deceasedFullName} · {visit.orderNumber}
                </Link>
                <span className="text-sm text-ink-2">{visit.statusLabel}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}
