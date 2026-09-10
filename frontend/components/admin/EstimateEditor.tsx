"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { formatRub } from "@/lib/api/catalog";
import { adminOrders, type SaveEstimateLine } from "@/lib/api/adminOrders";
import { LINE_TYPE_LABEL, type Order } from "@/lib/api/orders";
import { cn } from "@/lib/ui/cn";

const TYPES = ["work", "material", "delivery", "discount"] as const;

const inputClass =
  "min-h-hit rounded-input border border-border-strong bg-surface-raised px-3 py-2 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds placeholder:text-ink-2 focus-visible:shadow-focus";

/** What the fields hold while being typed. Quantity and price stay strings on purpose: coercing
 *  to a number on every keystroke turns a cleared field into "0", which a dispatcher then has to
 *  select and overwrite to correct a price. */
interface DraftLine {
  type: string;
  title: string;
  quantity: string;
  unit: string;
  unitPriceRub: string;
}

const emptyLine = (): DraftLine => ({
  type: "work",
  title: "",
  quantity: "1",
  unit: "",
  unitPriceRub: "",
});

/** Accepts a comma as the decimal separator — it is what a Russian keyboard offers first. */
function num(value: string): number {
  const parsed = Number(value.replace(",", ".").trim());
  return Number.isFinite(parsed) ? parsed : 0;
}

/**
 * The dispatcher's estimate editor.
 *
 * The running total is shown while typing, because the number the client will see is the point
 * of the exercise and finding it out only after saving is how mistakes get published.
 *
 * A draft is created first and published as a separate, deliberate action. Publishing is what
 * the client sees and what supersedes any previous agreement, so it is never a side effect of
 * saving.
 */
export function EstimateEditor({ order }: { order: Order }) {
  const router = useRouter();

  const draft = order.estimates.find((e) => e.status === "draft");
  const [lines, setLines] = useState<DraftLine[]>(
    draft?.lines.length
      ? draft.lines.map((l) => ({
          type: l.type,
          title: l.title,
          quantity: String(l.quantity),
          unit: l.unit ?? "",
          unitPriceRub: String(l.unitPriceRub),
        }))
      : [emptyLine()],
  );
  const [note, setNote] = useState(draft?.note ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const total = lines.reduce((sum, l) => sum + num(l.quantity) * num(l.unitPriceRub), 0);
  const alreadyPaid = ["paid", "assigning", "assigned", "in_progress", "qa_review", "customer_review",
    "completed", "disputed"].includes(order.status);

  function patch(index: number, changes: Partial<DraftLine>) {
    setLines((current) => current.map((l, i) => (i === index ? { ...l, ...changes } : l)));
  }

  async function save(thenPublish: boolean) {
    setError(null);
    setBusy(true);

    const body = {
      note: note.trim() || null,
      lines: lines
        .filter((l) => l.title.trim())
        .map<SaveEstimateLine>((l) => ({
          type: l.type,
          title: l.title.trim(),
          quantity: num(l.quantity),
          unit: l.unit.trim() || null,
          unitPriceRub: num(l.unitPriceRub),
        })),
    };

    try {
      if (draft) {
        await adminOrders.updateDraft(order.id, draft.version, body);
      } else {
        await adminOrders.createDraft(order.id, body);
      }

      if (thenPublish) {
        // Re-read: a freshly created draft's version number comes from the server, and guessing
        // it here would break the moment two dispatchers work the same order.
        const reloaded = await adminOrders.get(order.id);
        const toPublish = reloaded.estimates.find((e) => e.status === "draft");
        if (toPublish) {
          await adminOrders.publish(order.id, toPublish.version);
        }
      }

      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось сохранить смету.");
    } finally {
      setBusy(false);
    }
  }

  if (alreadyPaid) {
    return (
      <Card>
        <h2 className="font-display text-xl font-normal text-ink-1">Смета</h2>
        <p className="mt-2 text-sm text-ink-2">
          Заказ уже оплачен. Дополнительные работы оформляются отдельным согласованием с клиентом,
          а не новой сметой поверх оплаченной.
        </p>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col gap-5">
      <div>
        <h2 className="font-display text-xl font-normal text-ink-1">
          {draft ? `Черновик сметы · версия ${draft.version}` : "Новая смета"}
        </h2>
        <p className="mt-2 text-sm text-ink-2">
          Клиент видит смету только после публикации. Публикация отменяет его согласие с прежней
          версией — он должен подтвердить заново.
        </p>
      </div>

      <div className="flex flex-col gap-3">
        {lines.map((line, index) => (
          <div key={index} className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-2">Тип</span>
              <select
                aria-label={`Тип строки ${index + 1}`}
                value={line.type}
                onChange={(e) => patch(index, { type: e.target.value })}
                className={cn(inputClass, "w-36")}
              >
                {TYPES.map((t) => (
                  <option key={t} value={t}>
                    {LINE_TYPE_LABEL[t]}
                  </option>
                ))}
              </select>
            </label>

            <label className="flex flex-1 basis-56 flex-col gap-1">
              <span className="text-xs text-ink-2">Позиция</span>
              <input
                aria-label={`Название строки ${index + 1}`}
                value={line.title}
                onChange={(e) => patch(index, { title: e.target.value })}
                placeholder="Уборка участка"
                className={cn(inputClass, "w-full")}
              />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-2">Кол-во</span>
              <input
                aria-label={`Количество строки ${index + 1}`}
                inputMode="decimal"
                value={line.quantity}
                onChange={(e) => patch(index, { quantity: e.target.value })}
                className={cn(inputClass, "w-20 tabular-nums")}
              />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-2">Ед.</span>
              <input
                aria-label={`Единица строки ${index + 1}`}
                value={line.unit}
                onChange={(e) => patch(index, { unit: e.target.value })}
                placeholder="шт"
                className={cn(inputClass, "w-20")}
              />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-2">Цена, ₽</span>
              <input
                aria-label={`Цена строки ${index + 1}`}
                inputMode="decimal"
                value={line.unitPriceRub}
                onChange={(e) => patch(index, { unitPriceRub: e.target.value })}
                className={cn(inputClass, "w-28 tabular-nums")}
              />
            </label>

            <span className="min-w-24 pb-2 text-right text-sm tabular-nums text-ink-1">
              {formatRub(num(line.quantity) * num(line.unitPriceRub))} ₽
            </span>

            <Button
              variant="ghost"
              size="sm"
              aria-label={`Удалить строку ${index + 1}`}
              onClick={() => setLines(lines.filter((_, i) => i !== index))}
            >
              ✕
            </Button>
          </div>
        ))}

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-3">
          <Button variant="secondary" size="sm" onClick={() => setLines([...lines, emptyLine()])}>
            Добавить строку
          </Button>
          <span className="font-display text-lg tabular-nums text-ink-1">
            Итого: {formatRub(total)} ₽
          </span>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="estimate-note" className="text-sm font-semibold text-ink-1">
          Комментарий клиенту
        </label>
        <textarea
          id="estimate-note"
          rows={2}
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder="Что нашли на месте, почему такой объём"
          aria-describedby="estimate-note-hint"
          className={cn(inputClass, "min-h-0 w-full")}
        />
        <p id="estimate-note-hint" className="text-xs text-ink-2">
          Виден клиенту. Суммы вознаграждения исполнителя сюда не пишем.
        </p>
      </div>

      {total < 0 ? (
        <p role="alert" className="text-sm text-danger">
          Итог отрицательный — скидка больше суммы работ. Сервер такую смету не примет.
        </p>
      ) : null}

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      <div className="flex flex-wrap gap-3 border-t border-border pt-5">
        <Button variant="secondary" disabled={busy} onClick={() => void save(false)}>
          {busy ? "Сохраняем…" : "Сохранить черновик"}
        </Button>
        <Button disabled={busy || total < 0} onClick={() => void save(true)}>
          Опубликовать клиенту
        </Button>
      </div>
    </Card>
  );
}
