"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { formatRub } from "@/lib/api/catalog";
import { LINE_TYPE_LABEL, orders, type Estimate } from "@/lib/api/orders";
import { cn } from "@/lib/ui/cn";

/**
 * The estimate, line by line, with the accept decision.
 *
 * Every line is shown with its quantity and unit price, including materials. A single opaque
 * "работы и материалы" figure is exactly the shape of the hidden-extras problem this product
 * exists to answer, so the breakdown is the feature rather than a detail.
 *
 * Accepting sends the version number. The client is agreeing to *these* lines, and if a
 * correction was published while this page was open the server refuses — which is the honest
 * outcome, not an inconvenience.
 */
export function EstimateCard({
  orderId,
  estimate,
  decidable,
}: {
  orderId: number;
  estimate: Estimate;
  /** False for a superseded or already-answered version, which is shown for reference only. */
  decidable: boolean;
}) {
  const router = useRouter();
  const [busy, setBusy] = useState<"accept" | "reject" | null>(null);
  const [error, setError] = useState<string | null>(null);

  const expired = estimate.validUntil !== null && new Date(estimate.validUntil) < new Date();

  async function decide(action: "accept" | "reject") {
    setError(null);
    setBusy(action);
    try {
      if (action === "accept") {
        await orders.acceptEstimate(orderId, estimate.version);
      } else {
        await orders.rejectEstimate(orderId, estimate.version);
      }
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось отправить решение.");
    } finally {
      setBusy(null);
    }
  }

  return (
    <Card className="flex flex-col gap-5">
      <div className="flex flex-wrap items-baseline justify-between gap-3">
        <h2 className="font-display text-xl font-normal text-ink-1">
          Смета{estimate.version > 1 ? ` · версия ${estimate.version}` : ""}
        </h2>
        {estimate.status === "accepted" ? (
          <span className="rounded-pill border border-success bg-success-soft px-3 py-1 text-xs font-semibold text-success">
            Вы приняли
          </span>
        ) : estimate.status === "superseded" ? (
          <span className="rounded-pill border border-border-strong px-3 py-1 text-xs font-semibold text-ink-2">
            Заменена
          </span>
        ) : estimate.status === "rejected" ? (
          <span className="rounded-pill border border-border-strong px-3 py-1 text-xs font-semibold text-ink-2">
            Вы отклонили
          </span>
        ) : null}
      </div>

      {estimate.note ? <p className="text-sm text-ink-2">{estimate.note}</p> : null}

      <div className="-mx-1 overflow-x-auto">
        <table className="w-full min-w-[30rem] border-collapse text-sm">
          <caption className="sr-only">Состав сметы</caption>
          <thead>
            <tr className="border-b border-border text-left text-xs uppercase tracking-caps text-ink-2">
              <th scope="col" className="px-1 py-2 font-semibold">Позиция</th>
              <th scope="col" className="px-1 py-2 font-semibold">Тип</th>
              <th scope="col" className="px-1 py-2 text-right font-semibold">Кол-во</th>
              <th scope="col" className="px-1 py-2 text-right font-semibold">Цена</th>
              <th scope="col" className="px-1 py-2 text-right font-semibold">Сумма</th>
            </tr>
          </thead>
          <tbody>
            {estimate.lines.map((line, index) => (
              <tr key={index} className="border-b border-border last:border-0">
                <td className="px-1 py-2.5 text-ink-1">{line.title}</td>
                <td className="px-1 py-2.5 text-ink-2">{LINE_TYPE_LABEL[line.type] ?? line.type}</td>
                <td className="px-1 py-2.5 text-right tabular-nums text-ink-2">
                  {line.quantity}
                  {line.unit ? ` ${line.unit}` : ""}
                </td>
                <td className="px-1 py-2.5 text-right tabular-nums text-ink-2">
                  {formatRub(line.unitPriceRub)} ₽
                </td>
                <td
                  className={cn(
                    "px-1 py-2.5 text-right tabular-nums",
                    line.totalRub < 0 ? "text-success" : "text-ink-1",
                  )}
                >
                  {formatRub(line.totalRub)} ₽
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t border-border-strong">
              <th scope="row" colSpan={4} className="px-1 pt-3 text-left font-semibold text-ink-1">
                Итого
              </th>
              <td className="px-1 pt-3 text-right font-display text-lg tabular-nums text-ink-1">
                {formatRub(estimate.totalRub)} ₽
              </td>
            </tr>
          </tfoot>
        </table>
      </div>

      {estimate.validUntil ? (
        <p className={cn("text-xs", expired ? "text-danger" : "text-ink-2")}>
          {expired
            ? "Срок действия сметы истёк — мы подготовим новую."
            : `Смета действует до ${new Date(estimate.validUntil).toLocaleDateString("ru-RU")}.`}
        </p>
      ) : null}

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      {decidable && !expired ? (
        <div className="flex flex-col gap-3 border-t border-border pt-5">
          <p className="text-sm text-ink-2">
            Приняв смету, вы соглашаетесь именно с этим составом работ и суммой. Всё, что мы найдём
            на месте сверх этого, будет отдельно согласовано с вами — без вашего ответа ничего не
            выполняется и не списывается.
          </p>
          <div className="flex flex-wrap gap-3">
            <Button disabled={busy !== null} onClick={() => void decide("accept")}>
              {busy === "accept" ? "Отправляем…" : "Принять смету"}
            </Button>
            <Button
              variant="secondary"
              disabled={busy !== null}
              onClick={() => void decide("reject")}
            >
              {busy === "reject" ? "Отправляем…" : "Отклонить"}
            </Button>
          </div>
        </div>
      ) : null}
    </Card>
  );
}
