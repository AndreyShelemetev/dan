"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { formatRub } from "@/lib/api/catalog";
import { payments, type Payment } from "@/lib/api/payments";

/**
 * Paying for an order.
 *
 * The provider is a stub until the YooKassa adapter is written, and the panel says so plainly
 * rather than pretending. A payment control that looks real and takes no money is worse than one
 * that admits what it is — the first thing a client would do is wonder whether the money went
 * somewhere.
 */
export function PaymentPanel({
  orderId,
  amountRub,
  initialPayment,
}: {
  orderId: number;
  amountRub: number;
  initialPayment: Payment | null;
}) {
  const router = useRouter();
  const [payment, setPayment] = useState<Payment | null>(initialPayment);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run(action: () => Promise<Payment>) {
    setError(null);
    setBusy(true);
    try {
      setPayment(await action());
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось выполнить действие.");
    } finally {
      setBusy(false);
    }
  }

  const paid = payment?.status === "succeeded";

  return (
    <Card as="section" aria-labelledby="payment-heading" className="flex flex-col gap-5">
      <div>
        <h2 id="payment-heading" className="font-display text-xl font-normal text-ink-1">
          Оплата
        </h2>
        <p className="mt-2 text-sm text-ink-2">
          {paid
            ? "Оплачено. Мы подбираем исполнителя и согласуем дату визита."
            : "После оплаты мы назначим исполнителя и согласуем дату визита."}
        </p>
      </div>

      <p className="font-display text-2xl tabular-nums text-ink-1">
        {formatRub(amountRub)} ₽
        {payment ? (
          <span className="ml-3 align-middle text-sm font-sans text-ink-2">
            {payment.statusLabel}
          </span>
        ) : null}
      </p>

      {error ? (
        <p role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </p>
      ) : null}

      {!paid ? (
        <>
          <div className="flex flex-wrap gap-3">
            <Button disabled={busy} onClick={() => void run(() => payments.start(orderId))}>
              {busy ? "Готовим…" : payment?.isActive ? "Продолжить оплату" : "Перейти к оплате"}
            </Button>

            {payment?.isActive ? (
              // Stands in for finishing on the provider's page. It goes the long way round —
              // the application re-reads the payment and decides from that — so the code that
              // will run against a real provider is the code being exercised here.
              <Button
                variant="secondary"
                disabled={busy}
                onClick={() => void run(() => payments.devConfirm(payment.id))}
              >
                Подтвердить оплату (тест)
              </Button>
            ) : null}
          </div>

          <p className="rounded-card border border-border bg-surface-raised px-4 py-3 text-sm text-ink-2">
            <strong className="font-semibold text-ink-1">Приём платежей ещё не подключён.</strong>{" "}
            Кнопки выше работают на тестовом провайдере и денег не списывают. Настоящая оплата
            появится вместе с подключением ЮKassa.
          </p>
        </>
      ) : null}
    </Card>
  );
}
