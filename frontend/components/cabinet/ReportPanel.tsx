"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { orderActions, type VisitReport } from "@/lib/api/visits";
import { cn } from "@/lib/ui/cn";

/**
 * The photo report, and the client's decision about it.
 *
 * This is what the service is sold on: someone who cannot go to the cemetery is buying evidence
 * that somebody did. So the checklist is shown in full — including the lines that could not be
 * done and why. Hiding those would make the report look better and be worth less.
 */
export function ReportPanel({
  orderId,
  report,
  decidable,
}: {
  orderId: number;
  report: VisitReport;
  decidable: boolean;
}) {
  const router = useRouter();
  const [disputing, setDisputing] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run(action: () => Promise<unknown>) {
    setError(null);
    setBusy(true);
    try {
      await action();
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось выполнить действие.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card as="section" aria-labelledby="report-heading" className="flex flex-col gap-5">
      <div>
        <h2 id="report-heading" className="font-display text-xl font-normal text-ink-1">
          Отчёт о визите
        </h2>
        {report.visitedOn ? (
          <p className="mt-2 text-sm text-ink-2">
            <time dateTime={report.visitedOn}>
              {new Date(report.visitedOn).toLocaleDateString("ru-RU", {
                day: "numeric",
                month: "long",
                year: "numeric",
              })}
            </time>
          </p>
        ) : null}
      </div>

      {report.note ? (
        <p className="whitespace-pre-line text-base text-ink-1">{report.note}</p>
      ) : null}

      {report.checklist.length > 0 ? (
        <div>
          <h3 className="text-sm font-semibold text-ink-1">Что было сделано</h3>
          <ul className="mt-3 flex list-none flex-col gap-2 p-0">
            {report.checklist.map((item) => (
              <li key={item.key} className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                {/* The mark is paired with a word: a colour or a glyph alone leaves the state
                    unreadable to anyone who cannot distinguish them. */}
                <span
                  aria-hidden="true"
                  className={cn(
                    "text-base",
                    item.result === "done" ? "text-accent-deep" : "text-ink-2",
                  )}
                >
                  {item.result === "done" ? "✓" : "—"}
                </span>
                <span className="text-base text-ink-1">{item.title}</span>
                <span className="text-sm text-ink-2">{item.resultLabel}</span>
                {item.note ? (
                  <span className="basis-full pl-7 text-sm text-ink-2">{item.note}</span>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {error ? (
        <p role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </p>
      ) : null}

      {decidable ? (
        disputing ? (
          <div className="flex flex-col gap-3 border-t border-border pt-5">
            <label htmlFor="dispute-reason" className="text-sm font-semibold text-ink-1">
              Что не так?
            </label>
            <textarea
              id="dispute-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              aria-describedby="dispute-hint"
              className="min-h-hit rounded-input border border-border-strong bg-surface-raised px-3 py-2 text-base text-ink-1 outline-none focus-visible:shadow-focus"
            />
            <p id="dispute-hint" className="text-sm text-ink-2">
              Опишите, что именно вас не устроило. Мы разберёмся и вернёмся с решением.
            </p>
            <div className="flex flex-wrap gap-3">
              <Button
                variant="danger"
                disabled={busy || reason.trim().length === 0}
                onClick={() => void run(() => orderActions.dispute(orderId, reason.trim()))}
              >
                Отправить
              </Button>
              <Button variant="ghost" disabled={busy} onClick={() => setDisputing(false)}>
                Отмена
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-wrap gap-3 border-t border-border pt-5">
            <Button disabled={busy} onClick={() => void run(() => orderActions.acceptWork(orderId))}>
              Принять работу
            </Button>
            <Button variant="ghost" disabled={busy} onClick={() => setDisputing(true)}>
              Есть замечания
            </Button>
          </div>
        )
      ) : null}
    </Card>
  );
}
