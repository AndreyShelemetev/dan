"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field } from "@/components/ui/Field";
import { ApiError } from "@/lib/api/client";
import { createBurialSite, type Cemetery } from "@/lib/api/burialSites";

/** Cemeteries grouped by region, preserving the order the API returned them in. */
function groupByRegion(cemeteries: Cemetery[]): [string, Cemetery[]][] {
  const groups = new Map<string, Cemetery[]>();
  for (const cemetery of cemeteries) {
    const key = cemetery.region?.trim() || "Другие города";
    const bucket = groups.get(key);
    if (bucket) bucket.push(cemetery);
    else groups.set(key, [cemetery]);
  }
  return [...groups.entries()];
}

/**
 * Create form for a burial site.
 *
 * Only two fields are required — the cemetery and the name — because that is
 * genuinely the minimum the service can act on, and because the people filling
 * this in are often doing it in a difficult frame of mind. Everything else is
 * offered as "if you know it", and the hints say plainly that partial answers
 * are fine.
 */
export function NewBurialSiteForm({ cemeteries }: { cemeteries: Cemetery[] }) {
  const router = useRouter();
  const [isSubmitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setFieldErrors({});

    const form = new FormData(event.currentTarget);
    const cemeteryId = Number(form.get("cemeteryId"));
    const deceasedFullName = String(form.get("deceasedFullName") ?? "").trim();

    if (!cemeteryId) {
      setFieldErrors({ cemeteryId: "Выберите кладбище." });
      return;
    }
    if (deceasedFullName.length < 2) {
      setFieldErrors({ deceasedFullName: "Укажите имя." });
      return;
    }

    const optional = (key: string) => {
      const value = String(form.get(key) ?? "").trim();
      return value.length > 0 ? value : null;
    };

    setSubmitting(true);
    try {
      const site = await createBurialSite({
        cemeteryId,
        deceasedFullName,
        birthDateText: optional("birthDateText"),
        deathDateText: optional("deathDateText"),
        plotSection: optional("plotSection"),
        landmarks: optional("landmarks"),
        notes: optional("notes"),
      });

      // refresh() so the list page, which is a Server Component, re-fetches
      // instead of showing a cached render without the new record.
      router.refresh();
      router.push(`/cabinet/${site.id}`);
    } catch (err) {
      if (err instanceof ApiError) {
        const field =
          err.details && typeof err.details === "object" && "field" in err.details
            ? String((err.details as { field: unknown }).field)
            : null;

        if (field) {
          setFieldErrors({ [field]: err.message });
        } else {
          setError(err.message);
        }
      } else {
        setError("Не удалось сохранить. Попробуйте ещё раз.");
      }
      setSubmitting(false);
    }
  }

  if (cemeteries.length === 0) {
    return (
      <Card>
        <p className="text-sm text-ink-2">
          Пока мы работаем не на всех кладбищах. Напишите в поддержку — подскажем, что
          можно сделать для вашего случая.
        </p>
      </Card>
    );
  }

  return (
    <Card>
      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-6">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="cemeteryId" className="text-sm font-semibold text-ink-1">
            Кладбище
          </label>
          <select
            id="cemeteryId"
            name="cemeteryId"
            defaultValue=""
            aria-invalid={fieldErrors.cemeteryId ? true : undefined}
            aria-describedby={fieldErrors.cemeteryId ? "cemeteryId-error" : undefined}
            className={`min-h-hit rounded-input border bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds focus-visible:shadow-focus ${
              fieldErrors.cemeteryId ? "border-danger" : "border-border-strong"
            }`}
          >
            <option value="" disabled>
              Выберите из списка
            </option>
            {/* Grouped by region rather than listed flat: the service works across the country,
                so a flat list stops being scannable as soon as more than one city is served.
                <optgroup> is the browser's own grouping — it reads correctly to a screen reader
                and needs no custom component. */}
            {groupByRegion(cemeteries).map(([region, items]) => (
              <optgroup key={region} label={region}>
                {items.map((cemetery) => (
                  <option key={cemetery.id} value={cemetery.id}>
                    {cemetery.name}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
          {fieldErrors.cemeteryId ? (
            <p id="cemeteryId-error" role="alert" className="text-xs text-danger">
              {fieldErrors.cemeteryId}
            </p>
          ) : null}
        </div>

        <Field
          id="deceasedFullName"
          name="deceasedFullName"
          label="Имя"
          placeholder="Иванов Иван Иванович"
          autoComplete="off"
          error={fieldErrors.deceasedFullName}
        />

        <div className="grid gap-6 sm:grid-cols-2">
          <Field
            id="birthDateText"
            name="birthDateText"
            label="Дата рождения"
            placeholder="1934"
            hint="Можно неполно — например, только год"
            autoComplete="off"
          />
          <Field
            id="deathDateText"
            name="deathDateText"
            label="Дата смерти"
            placeholder="12 марта 1998"
            hint="Если помните приблизительно — так и напишите"
            autoComplete="off"
          />
        </div>

        <Field
          id="plotSection"
          name="plotSection"
          label="Участок, ряд"
          placeholder="уч. 12, ряд 3"
          hint="Как указано в документах или на схеме кладбища"
          autoComplete="off"
        />

        <div className="flex flex-col gap-1.5">
          <label htmlFor="landmarks" className="text-sm font-semibold text-ink-1">
            Ориентиры
          </label>
          <textarea
            id="landmarks"
            name="landmarks"
            rows={3}
            placeholder="Третий ряд от часовни, рядом высокая берёза, синяя ограда"
            aria-describedby="landmarks-hint"
            className="rounded-input border border-border-strong bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds placeholder:text-ink-2 focus-visible:shadow-focus"
          />
          <p id="landmarks-hint" className="text-xs text-ink-2">
            Самое полезное поле: чаще всего именно ориентиры помогают найти место.
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="notes" className="text-sm font-semibold text-ink-1">
            Особые указания
          </label>
          <textarea
            id="notes"
            name="notes"
            rows={3}
            placeholder="Не трогать посаженные кусты. Без искусственных цветов."
            aria-describedby="notes-hint"
            className="rounded-input border border-border-strong bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds placeholder:text-ink-2 focus-visible:shadow-focus"
          />
          <p id="notes-hint" className="text-xs text-ink-2">
            Религиозные или семейные правила, запреты — исполнитель увидит их до начала работ.
          </p>
        </div>

        {error ? (
          <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        ) : null}

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Сохраняем…" : "Сохранить"}
          </Button>
        </div>
      </form>
    </Card>
  );
}
