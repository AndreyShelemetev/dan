"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { ApiError } from "@/lib/api/client";
import { orders, type Order } from "@/lib/api/orders";

/**
 * The actions available on an order right now.
 *
 * Deliberately sparse: most of the time the ball is on our side and the honest interface says
 * so rather than inventing a button. Cancelling a paid order is not offered at all — that is a
 * refund calculation, and the server refuses it, so a button here would only produce an error.
 */
export function OrderActions({ order }: { order: Order }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = order.status === "draft";
  const canCancel = ["draft", "submitted", "location_review", "estimate_ready", "awaiting_payment"]
    .includes(order.status);

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

  if (!canSubmit && !canCancel && !error) {
    return null;
  }

  return (
    <div className="flex flex-col gap-3 border-t border-border pt-5">
      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        {canSubmit ? (
          <Button disabled={busy} onClick={() => void run(() => orders.submit(order.id))}>
            {busy ? "Отправляем…" : "Отправить заявку"}
          </Button>
        ) : null}

        {canCancel ? (
          <Button
            variant="secondary"
            disabled={busy}
            onClick={() => {
              const reason = window.prompt("Почему отменяете заказ? Можно не указывать.") ?? undefined;
              void run(() => orders.cancel(order.id, reason));
            }}
          >
            Отменить заказ
          </Button>
        ) : null}
      </div>
    </div>
  );
}
