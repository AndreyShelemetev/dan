"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field } from "@/components/ui/Field";
import { ApiError } from "@/lib/api/client";
import {
  STATUS_LABEL,
  adminCatalog,
  type AdminPackage,
  type ChecklistItemInput,
  type RequiredMediaInput,
} from "@/lib/api/adminCatalog";
import { formatRub } from "@/lib/api/catalog";
import { cn } from "@/lib/ui/cn";

const PHASES = [
  { value: "before", label: "До работ" },
  { value: "process", label: "В процессе" },
  { value: "after", label: "После работ" },
] as const;

const inputClass =
  "min-h-hit w-full rounded-input border border-border-strong bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds placeholder:text-ink-2 focus-visible:shadow-focus disabled:opacity-60";

function StatusBadge({ status }: { status: string }) {
  // The draft badge keeps its semantic colour in the border and fill, but takes its text from
  // --ink-1: --warning on --warning-soft measures 4.13:1, under the 4.5 this size needs. The
  // meaning is carried by the word anyway, so nothing is lost by making it readable.
  const tone =
    status === "published"
      ? "border-success bg-success-soft text-success"
      : status === "archived"
        ? "border-border-strong bg-surface text-ink-2"
        : "border-warning bg-warning-soft text-ink-1";

  return (
    <span className={cn("inline-flex items-center rounded-pill border px-3 py-1 text-xs font-semibold", tone)}>
      {STATUS_LABEL[status] ?? status}
    </span>
  );
}

/** Multi-line text edited as one textarea, one item per line — far less fiddly than a row of
 *  inputs with add/remove buttons for what is genuinely just a list of sentences. */
function LineList({
  id,
  label,
  hint,
  value,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  hint: string;
  value: string[];
  disabled: boolean;
  onChange: (lines: string[]) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-ink-1">
        {label}
      </label>
      <textarea
        id={id}
        rows={4}
        disabled={disabled}
        value={value.join("\n")}
        onChange={(e) => onChange(e.target.value.split("\n"))}
        aria-describedby={`${id}-hint`}
        className={cn(inputClass, "min-h-0")}
      />
      <p id={`${id}-hint`} className="text-xs text-ink-2">
        {hint}
      </p>
    </div>
  );
}

export function PackageEditor({
  initial,
  onDone,
  onCancel,
}: {
  /** Null when creating from scratch. */
  initial: AdminPackage | null;
  onDone: () => void;
  onCancel: () => void;
}) {
  const router = useRouter();
  const editable = initial ? initial.editable : true;

  const [code, setCode] = useState(initial?.code ?? "");
  const [version, setVersion] = useState(initial?.version ?? "1.0");
  const [title, setTitle] = useState(initial?.title ?? "");
  const [summary, setSummary] = useState(initial?.summary ?? "");
  const [priceFromRub, setPrice] = useState(String(initial?.priceFromRub ?? ""));
  const [warrantyDays, setWarranty] = useState(String(initial?.warrantyDays ?? 14));
  const [visitsLabel, setVisits] = useState(initial?.visitsLabel ?? "");
  const [sortOrder, setSort] = useState(String(initial?.sortOrder ?? 0));
  const [includes, setIncludes] = useState<string[]>(initial?.includes ?? [""]);
  const [limits, setLimits] = useState<string[]>(initial?.limits ?? [""]);
  const [items, setItems] = useState<ChecklistItemInput[]>(
    initial?.checklistItems?.length ? initial.checklistItems : [{ title: "", optional: false }],
  );
  const [media, setMedia] = useState<RequiredMediaInput[]>(
    initial?.requiredMedia?.length ? initial.requiredMedia : [],
  );

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<Record<string, string>>({});

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setFieldError({});
    setBusy(true);

    const body = {
      code: code.trim(),
      version: version.trim(),
      title: title.trim(),
      summary: summary.trim(),
      includes: includes.map((l) => l.trim()).filter(Boolean),
      limits: limits.map((l) => l.trim()).filter(Boolean),
      priceFromRub: Number(priceFromRub) || 0,
      warrantyDays: Number(warrantyDays) || 0,
      visitsLabel: visitsLabel.trim() || null,
      sortOrder: Number(sortOrder) || 0,
      checklistItems: items.filter((i) => i.title.trim()),
      requiredMedia: media.filter((m) => m.phase && m.minCount > 0),
    };

    try {
      if (initial) {
        await adminCatalog.updatePackage(initial.id, body);
      } else {
        await adminCatalog.createPackage(body);
      }
      router.refresh();
      onDone();
    } catch (err) {
      if (err instanceof ApiError) {
        const field =
          err.details && typeof err.details === "object" && "field" in err.details
            ? String((err.details as { field: unknown }).field)
            : null;
        if (field) setFieldError({ [field]: err.message });
        else setError(err.message);
      } else {
        setError("Не удалось сохранить.");
      }
      setBusy(false);
    }
  }

  return (
    <Card className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-xl font-normal text-ink-1">
          {initial ? `${initial.title} · v${initial.version}` : "Новый пакет"}
        </h2>
        {initial ? <StatusBadge status={initial.status} /> : null}
      </div>

      {!editable ? (
        <p className="rounded-card border border-border-strong bg-surface px-4 py-3 text-sm text-ink-2">
          Опубликованную версию изменить нельзя — заказы, проданные по ней, должны сохранить свои
          условия. Создайте новую версию, чтобы что-то поменять.
        </p>
      ) : null}

      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-6">
        <div className="grid gap-5 sm:grid-cols-2">
          <Field
            id="pkg-code"
            label="Код"
            value={code}
            disabled={!editable || Boolean(initial)}
            onChange={(e) => setCode(e.target.value)}
            hint="Латиницей, без пробелов. Не меняется между версиями."
            error={fieldError.code}
          />
          <Field
            id="pkg-version"
            label="Версия"
            value={version}
            disabled={!editable || Boolean(initial)}
            onChange={(e) => setVersion(e.target.value)}
            hint="Например 1.0"
            error={fieldError.version}
          />
        </div>

        <Field
          id="pkg-title"
          label="Название"
          value={title}
          disabled={!editable}
          onChange={(e) => setTitle(e.target.value)}
          error={fieldError.title}
        />

        <div className="flex flex-col gap-1.5">
          <label htmlFor="pkg-summary" className="text-sm font-semibold text-ink-1">
            Описание
          </label>
          <textarea
            id="pkg-summary"
            rows={3}
            disabled={!editable}
            value={summary}
            onChange={(e) => setSummary(e.target.value)}
            className={cn(inputClass, "min-h-0")}
          />
        </div>

        <div className="grid gap-5 sm:grid-cols-4">
          <Field
            id="pkg-price"
            label="Цена от, ₽"
            inputMode="numeric"
            value={priceFromRub}
            disabled={!editable}
            onChange={(e) => setPrice(e.target.value)}
            error={fieldError.priceFromRub}
          />
          <Field
            id="pkg-warranty"
            label="Гарантия, дней"
            inputMode="numeric"
            value={warrantyDays}
            disabled={!editable}
            onChange={(e) => setWarranty(e.target.value)}
            error={fieldError.warrantyDays}
          />
          <Field
            id="pkg-visits"
            label="Визитов"
            value={visitsLabel}
            disabled={!editable}
            onChange={(e) => setVisits(e.target.value)}
            hint="«1 визит»"
          />
          <Field
            id="pkg-sort"
            label="Порядок"
            inputMode="numeric"
            value={sortOrder}
            disabled={!editable}
            onChange={(e) => setSort(e.target.value)}
          />
        </div>

        <LineList
          id="pkg-includes"
          label="Что входит"
          hint="По одному пункту на строку. Показывается на карточке пакета."
          value={includes}
          disabled={!editable}
          onChange={setIncludes}
        />

        <LineList
          id="pkg-limits"
          label="Ограничения"
          hint="Что НЕ входит. Видно клиенту до оплаты — скрытые доплаты главный страх аудитории."
          value={limits}
          disabled={!editable}
          onChange={setLimits}
        />

        <fieldset className="flex flex-col gap-3 border-t border-border pt-5">
          <legend className="text-sm font-semibold text-ink-1">Чек-лист исполнителя</legend>
          <p className="text-xs text-ink-2">
            Определение «сделано», по которому QA проверяет визит. Без него пакет нельзя
            опубликовать.
          </p>

          {items.map((item, index) => (
            <div key={index} className="flex flex-wrap items-center gap-3">
              <input
                aria-label={`Пункт ${index + 1}`}
                value={item.title}
                disabled={!editable}
                onChange={(e) => {
                  const next = [...items];
                  next[index] = { ...item, title: e.target.value };
                  setItems(next);
                }}
                className={cn(inputClass, "flex-1 basis-64")}
              />
              <label className="flex min-h-hit cursor-pointer items-center gap-2 text-sm text-ink-2">
                <input
                  type="checkbox"
                  checked={item.optional}
                  disabled={!editable}
                  onChange={(e) => {
                    const next = [...items];
                    next[index] = { ...item, optional: e.target.checked };
                    setItems(next);
                  }}
                  className="size-6 accent-[color:var(--accent)]"
                />
                необязательный
              </label>
              {editable ? (
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`Удалить пункт ${index + 1}`}
                  onClick={() => setItems(items.filter((_, i) => i !== index))}
                >
                  Удалить
                </Button>
              ) : null}
            </div>
          ))}

          {editable ? (
            <div>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setItems([...items, { title: "", optional: false }])}
              >
                Добавить пункт
              </Button>
            </div>
          ) : null}
        </fieldset>

        <fieldset className="flex flex-col gap-3 border-t border-border pt-5">
          <legend className="text-sm font-semibold text-ink-1">Обязательные фотографии</legend>
          <p className="text-xs text-ink-2">
            Минимум кадров по этапам. Неполный отчёт не попадёт в QA.
          </p>

          {media.map((rule, index) => (
            <div key={index} className="flex flex-wrap items-center gap-3">
              <select
                aria-label={`Этап правила ${index + 1}`}
                value={rule.phase}
                disabled={!editable}
                onChange={(e) => {
                  const next = [...media];
                  next[index] = { ...rule, phase: e.target.value };
                  setMedia(next);
                }}
                className={cn(inputClass, "w-40")}
              >
                {PHASES.map((p) => (
                  <option key={p.value} value={p.value}>
                    {p.label}
                  </option>
                ))}
              </select>
              <input
                aria-label={`Минимум кадров правила ${index + 1}`}
                inputMode="numeric"
                value={rule.minCount}
                disabled={!editable}
                onChange={(e) => {
                  const next = [...media];
                  next[index] = { ...rule, minCount: Number(e.target.value) || 0 };
                  setMedia(next);
                }}
                className={cn(inputClass, "w-24")}
              />
              <input
                aria-label={`Описание правила ${index + 1}`}
                value={rule.description ?? ""}
                disabled={!editable}
                onChange={(e) => {
                  const next = [...media];
                  next[index] = { ...rule, description: e.target.value };
                  setMedia(next);
                }}
                placeholder="Какие ракурсы"
                className={cn(inputClass, "flex-1 basis-56")}
              />
              {editable ? (
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`Удалить правило ${index + 1}`}
                  onClick={() => setMedia(media.filter((_, i) => i !== index))}
                >
                  Удалить
                </Button>
              ) : null}
            </div>
          ))}

          {editable ? (
            <div>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setMedia([...media, { phase: "before", minCount: 4, description: "" }])}
              >
                Добавить правило
              </Button>
            </div>
          ) : null}
        </fieldset>

        {error ? (
          <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        ) : null}

        <div className="flex flex-wrap items-center gap-3 border-t border-border pt-5">
          {editable ? (
            <Button type="submit" disabled={busy}>
              {busy ? "Сохраняем…" : "Сохранить"}
            </Button>
          ) : null}
          <Button type="button" variant="secondary" onClick={onCancel}>
            {editable ? "Отмена" : "Закрыть"}
          </Button>
          {initial ? (
            <span className="text-xs text-ink-2">
              от {formatRub(initial.priceFromRub)} ₽ · гарантия {initial.warrantyDays} дн.
            </span>
          ) : null}
        </div>
      </form>
    </Card>
  );
}

export { StatusBadge };
