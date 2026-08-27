"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { formatRub, type ServicePackage } from "@/lib/api/catalog";
import { orders } from "@/lib/api/orders";
import { LOCATION_QUALITY } from "@/lib/api/burialSites";
import { cn } from "@/lib/ui/cn";

/**
 * Ordering care for one burial site.
 *
 * The package limits are shown next to the price rather than behind a link, because the promise
 * this product is built on is that a client learns what is *not* covered before they commit.
 *
 * When the location is too thin to dispatch against, the form says so up front instead of taking
 * an order it cannot fulfil — the same judgement the server makes on submit, surfaced early so
 * nobody is surprised after the fact.
 */
export function OrderCareForm({
  siteId,
  packages,
  locationQuality,
  canOrder,
}: {
  siteId: number;
  packages: ServicePackage[];
  locationQuality: string;
  /** Viewing a record and spending money on it are different rights. */
  canOrder: boolean;
}) {
  const router = useRouter();
  const [selected, setSelected] = useState<string | null>(null);
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const insufficient = locationQuality === LOCATION_QUALITY.insufficient;

  if (!canOrder) {
    return (
      <Card>
        <h2 className="font-display text-xl font-normal text-ink-1">Заказать уход</h2>
        <p className="mt-2 text-sm text-ink-2">
          У вас доступ на просмотр. Заказывать уход может владелец карточки или участник, которому
          он выдал такое право.
        </p>
      </Card>
    );
  }

  async function submit() {
    if (!selected) {
      setError("Выберите пакет.");
      return;
    }

    setError(null);
    setBusy(true);
    try {
      const order = await orders.create({
        burialSiteId: siteId,
        packageCode: selected,
        comment: comment.trim() || null,
      });
      router.refresh();
      router.push(`/cabinet/orders/${order.id}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось создать заказ.");
      setBusy(false);
    }
  }

  return (
    <Card className="flex flex-col gap-5">
      <div>
        <h2 className="font-display text-xl font-normal text-ink-1">Заказать уход</h2>
        <p className="mt-2 text-sm text-ink-2">
          Цена «от» — стартовая. Точная сумма появится в смете после того, как мы оценим состояние
          места, и вы сможете принять её или отказаться.
        </p>
      </div>

      {insufficient ? (
        <p className="rounded-card border border-warning bg-warning-soft px-4 py-3 text-sm text-ink-1">
          По текущему описанию место найти сложно. Начните с осмотра — исполнитель найдёт место,
          сфотографирует состояние и составит смету. Его стоимость засчитывается в заказ ухода.
        </p>
      ) : null}

      <fieldset className="flex flex-col gap-3">
        <legend className="sr-only">Выберите пакет</legend>
        {packages.map((pkg) => {
          const active = selected === pkg.code;
          return (
            <label
              key={pkg.code}
              className={cn(
                "flex cursor-pointer gap-3 rounded-card border p-4 transition-colors duration-ds ease-ds",
                active ? "border-accent bg-accent-soft" : "border-border bg-surface hover:border-border-strong",
              )}
            >
              <input
                type="radio"
                name="package"
                value={pkg.code}
                checked={active}
                onChange={() => setSelected(pkg.code)}
                className="mt-1 size-6 shrink-0 accent-[color:var(--accent)]"
              />
              <span className="flex flex-1 flex-col gap-1">
                <span className="flex flex-wrap items-baseline justify-between gap-2">
                  <span className="font-display text-lg text-ink-1">{pkg.title}</span>
                  <span className="whitespace-nowrap font-display text-base tabular-nums text-ink-1">
                    от {formatRub(pkg.priceFromRub)} ₽
                  </span>
                </span>
                <span className="text-sm text-ink-2">{pkg.summary}</span>
                {pkg.limits.length > 0 ? (
                  <span className="mt-1 flex flex-col gap-0.5 text-xs text-ink-2">
                    {pkg.limits.map((limit) => (
                      <span key={limit}>· {limit}</span>
                    ))}
                  </span>
                ) : null}
              </span>
            </label>
          );
        })}
      </fieldset>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="order-comment" className="text-sm font-semibold text-ink-1">
          Комментарий
        </label>
        <textarea
          id="order-comment"
          rows={3}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          placeholder="Например: успеть до 9 мая; на участке хрупкая ваза"
          aria-describedby="order-comment-hint"
          className="rounded-input border border-border-strong bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds placeholder:text-ink-2 focus-visible:shadow-focus"
        />
        <p id="order-comment-hint" className="text-xs text-ink-2">
          Пожелания по срокам и всё, что важно знать исполнителю.
        </p>
      </div>

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3 border-t border-border pt-5">
        <Button disabled={busy} onClick={() => void submit()}>
          {busy ? "Создаём…" : "Создать заказ"}
        </Button>
        <span className="text-xs text-ink-2">
          Оплата — только после того, как вы примете смету.
        </span>
      </div>
    </Card>
  );
}
