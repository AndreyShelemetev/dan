import { Card } from "@/components/ui/Card";
import { formatRub } from "@/lib/api/catalog";
import type { Dispute } from "@/lib/api/disputes";

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString("ru-RU", { day: "numeric", month: "long", year: "numeric" });
}

/**
 * The client's own view of a dispute: what they said was wrong, where the case stands, and once
 * support has decided, what was decided. No mention of the executor's payout or margin anywhere
 * here — a refund amount is the client's own money, everything else about the money is not theirs
 * to see.
 */
export function DisputePanel({ dispute }: { dispute: Dispute }) {
  const decided = dispute.resolutionType !== null;

  return (
    <Card as="section" aria-labelledby="dispute-heading" className="flex flex-col gap-5">
      <div>
        <h2 id="dispute-heading" className="font-display text-xl font-normal text-ink-1">
          Обращение
        </h2>
        <p className="mt-2 text-sm text-ink-2">
          Открыто <time dateTime={dispute.createdAt}>{formatDate(dispute.createdAt)}</time>
        </p>
      </div>

      <div>
        <h3 className="text-sm font-semibold text-ink-1">Что вы указали</h3>
        <p className="mt-2 whitespace-pre-line text-base text-ink-1">{dispute.reason}</p>
      </div>

      <div className="border-t border-border pt-5">
        <span className="rounded-pill border border-accent bg-accent-soft px-4 py-1.5 text-sm font-semibold text-accent-deep">
          {dispute.statusLabel}
        </span>
      </div>

      {decided ? (
        <div className="flex flex-col gap-2 border-t border-border pt-5">
          <h3 className="text-sm font-semibold text-ink-1">Решение</h3>
          <p className="text-base text-ink-1">{dispute.resolutionTypeLabel}</p>
          {dispute.resolutionText ? (
            <p className="whitespace-pre-line text-base text-ink-1">{dispute.resolutionText}</p>
          ) : null}
          {dispute.refundAmountRub !== null ? (
            <p className="text-base text-ink-1">Сумма возврата: {formatRub(dispute.refundAmountRub)} ₽</p>
          ) : null}
          {dispute.resolvedAt ? (
            <p className="text-sm text-ink-2">
              Решено <time dateTime={dispute.resolvedAt}>{formatDate(dispute.resolvedAt)}</time>
            </p>
          ) : null}
        </div>
      ) : (
        <p className="text-sm text-ink-2">Мы разбираемся и вернёмся с решением.</p>
      )}
    </Card>
  );
}
