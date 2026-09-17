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
 * "Перейти к оплате" leaves the page — it sends the browser to `confirmationUrl`, the same way
 * a real redirect-confirmation provider works. Coming back proves only that the client returned;
 * the order page re-asks the provider (`sync`) before this component ever renders, so what is
 * shown here is the provider's answer, not the fact of the return (BR-007).
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

  /** Starts (or resumes, or retries after a failure) an attempt and leaves for the provider's
   *  page. A full navigation, not a fetch — the client's browser has to actually be there for a
   *  real redirect-confirmation flow to work. */
  async function goToProvider() {
    setError(null);
    setBusy(true);
    try {
      const next = await payments.start(orderId);
      if (next.confirmationUrl) {
        window.location.assign(next.confirmationUrl);
        return;
      }
      setPayment(next);
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось начать оплату.");
    } finally {
      setBusy(false);
    }
  }

  async function devConfirm() {
    if (!payment) return;
    setError(null);
    setBusy(true);
    try {
      setPayment(await payments.devConfirm(payment.id));
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось выполнить действие.");
    } finally {
      setBusy(false);
    }
  }

  const paid = payment?.status === "succeeded";
  const failed = payment ? ["canceled", "failed"].includes(payment.status) : false;
  const waiting = payment ? !paid && !failed : false;

  return (
    <Card as="section" aria-labelledby="payment-heading" className="flex flex-col gap-5">
      <div>
        <h2 id="payment-heading" className="font-display text-xl font-normal text-ink-1">
          Оплата
        </h2>
        <p className="mt-2 text-sm text-ink-2">
          {paid
            ? "Оплачено. Мы подбираем исполнителя и согласуем дату визита."
            : failed
              ? "Оплата не прошла. Деньги не списаны — можно попробовать ещё раз."
              : waiting
                ? "Ждём подтверждения от платёжного сервиса. Если вы уже оплатили, обновите страницу."
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

      {waiting ? (
        <p className="rounded-card border border-warning bg-warning-soft px-4 py-3 text-sm text-ink-1">
          Страница провайдера могла ещё не закрыться, или банк ещё подтверждает платёж.
        </p>
      ) : null}

      {!paid ? (
        <>
          <div className="flex flex-wrap gap-3">
            <Button disabled={busy} onClick={() => void goToProvider()}>
              {busy
                ? "Готовим…"
                : failed
                  ? "Оплатить ещё раз"
                  : payment?.isActive
                    ? "Продолжить оплату"
                    : "Перейти к оплате"}
            </Button>

            {waiting ? (
              <Button variant="secondary" disabled={busy} onClick={() => router.refresh()}>
                Обновить статус
              </Button>
            ) : null}

            {payment?.isActive ? (
              // Stands in for finishing on the provider's page. It goes the long way round —
              // the application re-reads the payment and decides from that — so the code that
              // will run against a real provider is the code being exercised here.
              <Button variant="secondary" disabled={busy} onClick={() => void devConfirm()}>
                Подтвердить оплату (тест)
              </Button>
            ) : null}
          </div>

          <p className="rounded-card border border-border bg-surface-raised px-4 py-3 text-sm text-ink-2">
            <strong className="font-semibold text-ink-1">Приём платежей ещё не подключён.</strong>{" "}
            Кнопка «{failed ? "Оплатить ещё раз" : "Перейти к оплате"}» уводит на тестового
            провайдера и денег не списывает. Настоящая оплата появится вместе с подключением
            ЮKassa.
          </p>
        </>
      ) : null}
    </Card>
  );
}
