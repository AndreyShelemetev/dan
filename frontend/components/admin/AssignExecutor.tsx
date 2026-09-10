"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { formatRub } from "@/lib/api/catalog";
import { qa, type ExecutorOption, type Visit } from "@/lib/api/visits";

const inputClass =
  "min-h-hit rounded-input border border-border-strong bg-surface-raised px-3 py-2 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds focus-visible:shadow-focus";

/**
 * Sending a paid order to an executor.
 *
 * The payout is entered rather than derived. The margin between what the client pays and what the
 * executor receives is a commercial decision — a formula in code would quietly make it a policy,
 * and the wrong one on the day a job turns out to be three hours away.
 */
export function AssignExecutor({
  orderId,
  executors,
  visits,
}: {
  orderId: number;
  executors: ExecutorOption[];
  visits: Visit[];
}) {
  const router = useRouter();
  const [executorId, setExecutorId] = useState<string>("");
  const [payout, setPayout] = useState("");
  const [scheduledFor, setScheduledFor] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const live = visits.find((v) =>
    ["offered", "accepted", "in_progress", "submitted", "rework"].includes(v.status),
  );

  async function assign() {
    setError(null);
    setBusy(true);
    try {
      await qa.assign(orderId, {
        executorUserId: Number(executorId),
        payoutRub: payout.trim() ? Number(payout.replace(",", ".")) : null,
        scheduledFor: scheduledFor ? new Date(scheduledFor).toISOString() : null,
      });
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось назначить исполнителя.");
    } finally {
      setBusy(false);
    }
  }

  if (live) {
    return (
      <Card as="section" aria-labelledby="visit-heading" className="flex flex-col gap-3">
        <h2 id="visit-heading" className="font-display text-lg font-normal text-ink-1">
          Визит
        </h2>
        <dl className="grid gap-3 sm:grid-cols-3">
          <div>
            <dt className="text-sm text-ink-2">Исполнитель</dt>
            <dd className="mt-0.5 text-base text-ink-1">{live.executorName ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-sm text-ink-2">Статус</dt>
            <dd className="mt-0.5 text-base text-ink-1">{live.statusLabel}</dd>
          </div>
          <div>
            <dt className="text-sm text-ink-2">Вознаграждение</dt>
            <dd className="mt-0.5 text-base tabular-nums text-ink-1">
              {live.payoutRub === null ? "—" : `${formatRub(live.payoutRub)} ₽`}
            </dd>
          </div>
        </dl>
      </Card>
    );
  }

  return (
    <Card as="section" aria-labelledby="assign-heading" className="flex flex-col gap-4">
      <div>
        <h2 id="assign-heading" className="font-display text-lg font-normal text-ink-1">
          Назначить исполнителя
        </h2>
        <p className="mt-2 text-sm text-ink-2">
          Клиент исполнителя не выбирает и не видит — это наша работа.
        </p>
      </div>

      {executors.length === 0 ? (
        <p className="text-sm text-ink-2">
          Нет активных исполнителей. Заведите аккаунт с ролью «исполнитель», иначе назначать
          некого.
        </p>
      ) : (
        <>
          <div className="flex flex-wrap items-end gap-3">
            <label className="flex flex-1 basis-56 flex-col gap-1">
              <span className="text-sm text-ink-2">Исполнитель</span>
              <select
                value={executorId}
                onChange={(e) => setExecutorId(e.target.value)}
                className={inputClass}
              >
                <option value="">— выберите —</option>
                {executors.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.displayName}
                  </option>
                ))}
              </select>
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-sm text-ink-2">Вознаграждение, ₽</span>
              <input
                inputMode="decimal"
                value={payout}
                onChange={(e) => setPayout(e.target.value)}
                placeholder="1500"
                className={`${inputClass} w-32 tabular-nums`}
              />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-sm text-ink-2">Дата визита</span>
              <input
                type="date"
                value={scheduledFor}
                onChange={(e) => setScheduledFor(e.target.value)}
                className={inputClass}
              />
            </label>
          </div>

          <p className="text-sm text-ink-2">
            Вознаграждение — коммерческая тайна. Клиент его не увидит ни в заказе, ни в отчёте.
          </p>

          {error ? (
            <p role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
              {error}
            </p>
          ) : null}

          <div>
            <Button disabled={busy || !executorId} onClick={() => void assign()}>
              {busy ? "Назначаем…" : "Предложить визит"}
            </Button>
          </div>
        </>
      )}
    </Card>
  );
}
