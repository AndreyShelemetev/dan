"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field } from "@/components/ui/Field";
import { StatusBadge } from "@/components/admin/PackageEditor";
import { ApiError } from "@/lib/api/client";
import { adminCatalog, type AdminPlan } from "@/lib/api/adminCatalog";
import { formatRub } from "@/lib/api/catalog";
import { cn } from "@/lib/ui/cn";

const inputClass =
  "min-h-hit w-full rounded-input border border-border-strong bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none transition-colors duration-ds ease-ds placeholder:text-ink-2 focus-visible:shadow-focus disabled:opacity-60";

/**
 * Subscription plans.
 *
 * A plan is a longer commitment than a single order: it runs for months, so the version a client
 * bought has to survive every price change in that window. Same rule as packages — published is
 * immutable, changes mean a new version.
 */
export function PlansManager({
  plans,
  packageCodes,
}: {
  plans: AdminPlan[];
  /** Published package codes a plan may point at — a plan whose package is not on sale would
   *  create visits with no checklist. */
  packageCodes: string[];
}) {
  const router = useRouter();
  const [editing, setEditing] = useState<AdminPlan | null>(null);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  async function run(id: number, action: () => Promise<unknown>) {
    setError(null);
    setBusyId(id);
    try {
      await action();
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не удалось выполнить действие.");
    } finally {
      setBusyId(null);
    }
  }

  if (creating || editing) {
    return (
      <PlanForm
        initial={editing}
        packageCodes={packageCodes}
        onDone={() => {
          setEditing(null);
          setCreating(false);
          router.refresh();
        }}
        onCancel={() => {
          setEditing(null);
          setCreating(false);
        }}
      />
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-normal text-ink-1">Подписки</h1>
          <p className="mt-1 text-sm text-ink-2">
            План визитов на период. Опубликованная версия неизменяема — подписка идёт месяцами и
            должна сохранить условия покупки.
          </p>
        </div>
        <Button onClick={() => setCreating(true)} disabled={packageCodes.length === 0}>
          Новый план
        </Button>
      </div>

      {packageCodes.length === 0 ? (
        <Card>
          <p className="text-sm text-ink-2">
            Сначала опубликуйте хотя бы один пакет: подписка выполняется по нему, и без пакета
            визит не с чем сверять.
          </p>
        </Card>
      ) : null}

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      {plans.length === 0 ? (
        packageCodes.length > 0 ? (
          <Card>
            <p className="text-sm text-ink-2">
              Планов пока нет. Подписка — самый дешёвый канал повторных продаж по бизнес-модели,
              но она не обязательна для запуска.
            </p>
          </Card>
        ) : null
      ) : (
        <Card className="flex flex-col gap-4">
          <div className="-mx-2 overflow-x-auto">
            <table className="w-full min-w-[44rem] border-collapse text-sm">
              <thead>
                <tr className="border-b border-border text-left text-xs uppercase tracking-caps text-ink-2">
                  <th className="px-2 py-2 font-semibold">План</th>
                  <th className="px-2 py-2 font-semibold">Версия</th>
                  <th className="px-2 py-2 font-semibold">Статус</th>
                  <th className="px-2 py-2 font-semibold">Пакет</th>
                  <th className="px-2 py-2 font-semibold tabular-nums">Визитов</th>
                  <th className="px-2 py-2 font-semibold tabular-nums">Период</th>
                  <th className="px-2 py-2 font-semibold tabular-nums">Цена</th>
                  <th className="px-2 py-2 font-semibold tabular-nums">За визит</th>
                  <th className="px-2 py-2 font-semibold">Действия</th>
                </tr>
              </thead>
              <tbody>
                {plans.map((plan) => (
                  <tr key={plan.id} className="border-b border-border last:border-0">
                    <td className="px-2 py-3 text-ink-1">{plan.title}</td>
                    <td className="px-2 py-3 tabular-nums text-ink-2">{plan.version}</td>
                    <td className="px-2 py-3"><StatusBadge status={plan.status} /></td>
                    <td className="px-2 py-3 text-ink-2">{plan.servicePackageCode}</td>
                    <td className="px-2 py-3 tabular-nums text-ink-1">{plan.visitsTotal}</td>
                    <td className="px-2 py-3 tabular-nums text-ink-2">{plan.periodMonths} мес.</td>
                    <td className="px-2 py-3 tabular-nums text-ink-1">{formatRub(plan.priceRub)} ₽</td>
                    <td className="px-2 py-3 tabular-nums text-ink-2">
                      {formatRub(plan.pricePerVisit)} ₽
                    </td>
                    <td className="px-2 py-3">
                      <div className="flex flex-wrap gap-1">
                        <Button variant="ghost" size="sm" onClick={() => setEditing(plan)}>
                          {plan.editable ? "Изменить" : "Смотреть"}
                        </Button>
                        {plan.status === "draft" ? (
                          <>
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={busyId === plan.id}
                              onClick={() => void run(plan.id, () => adminCatalog.publishPlan(plan.id))}
                            >
                              Опубликовать
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={busyId === plan.id}
                              onClick={() => {
                                if (!window.confirm(`Удалить черновик плана «${plan.title}»? Это необратимо.`)) return;
                                void run(plan.id, () => adminCatalog.deletePlan(plan.id));
                              }}
                              className="text-danger hover:bg-danger-soft hover:text-danger"
                            >
                              Удалить
                            </Button>
                          </>
                        ) : null}
                        {plan.status === "published" ? (
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={busyId === plan.id}
                            onClick={() => void run(plan.id, () => adminCatalog.archivePlan(plan.id))}
                          >
                            В архив
                          </Button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </div>
  );
}

function PlanForm({
  initial,
  packageCodes,
  onDone,
  onCancel,
}: {
  initial: AdminPlan | null;
  packageCodes: string[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const editable = initial ? initial.editable : true;

  const [code, setCode] = useState(initial?.code ?? "");
  const [version, setVersion] = useState(initial?.version ?? "1.0");
  const [title, setTitle] = useState(initial?.title ?? "");
  const [summary, setSummary] = useState(initial?.summary ?? "");
  const [pkg, setPkg] = useState(initial?.servicePackageCode ?? packageCodes[0] ?? "");
  const [visitsTotal, setVisits] = useState(String(initial?.visitsTotal ?? 4));
  const [periodMonths, setPeriod] = useState(String(initial?.periodMonths ?? 12));
  const [priceRub, setPrice] = useState(String(initial?.priceRub ?? ""));
  const [sortOrder, setSort] = useState(String(initial?.sortOrder ?? 0));

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<Record<string, string>>({});

  const visits = Number(visitsTotal) || 0;
  const price = Number(priceRub) || 0;
  const perVisit = visits > 0 ? Math.round(price / visits) : 0;

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
      servicePackageCode: pkg,
      visitsTotal: visits,
      periodMonths: Number(periodMonths) || 0,
      priceRub: price,
      sortOrder: Number(sortOrder) || 0,
    };

    try {
      if (initial) await adminCatalog.updatePlan(initial.id, body);
      else await adminCatalog.createPlan(body);
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
          {initial ? `${initial.title} · v${initial.version}` : "Новый план подписки"}
        </h2>
        {initial ? <StatusBadge status={initial.status} /> : null}
      </div>

      {!editable ? (
        <p className="rounded-card border border-border-strong bg-surface px-4 py-3 text-sm text-ink-2">
          Опубликованный план изменить нельзя — подписки, купленные по нему, идут месяцами и
          должны сохранить свои условия.
        </p>
      ) : null}

      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-6">
        <div className="grid gap-5 sm:grid-cols-2">
          <Field
            id="plan-code"
            label="Код"
            value={code}
            disabled={!editable || Boolean(initial)}
            onChange={(e) => setCode(e.target.value)}
            hint="Например care-4"
            error={fieldError.code}
          />
          <Field
            id="plan-version"
            label="Версия"
            value={version}
            disabled={!editable || Boolean(initial)}
            onChange={(e) => setVersion(e.target.value)}
            error={fieldError.version}
          />
        </div>

        <Field
          id="plan-title"
          label="Название"
          value={title}
          disabled={!editable}
          onChange={(e) => setTitle(e.target.value)}
          error={fieldError.title}
        />

        <div className="flex flex-col gap-1.5">
          <label htmlFor="plan-summary" className="text-sm font-semibold text-ink-1">
            Описание
          </label>
          <textarea
            id="plan-summary"
            rows={2}
            disabled={!editable}
            value={summary}
            onChange={(e) => setSummary(e.target.value)}
            className={cn(inputClass, "min-h-0")}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="plan-package" className="text-sm font-semibold text-ink-1">
            Пакет для каждого визита
          </label>
          <select
            id="plan-package"
            value={pkg}
            disabled={!editable}
            onChange={(e) => setPkg(e.target.value)}
            className={inputClass}
          >
            {packageCodes.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>

        <div className="grid gap-5 sm:grid-cols-4">
          <Field
            id="plan-visits"
            label="Визитов"
            inputMode="numeric"
            value={visitsTotal}
            disabled={!editable}
            onChange={(e) => setVisits(e.target.value)}
            error={fieldError.visitsTotal}
          />
          <Field
            id="plan-period"
            label="Период, мес."
            inputMode="numeric"
            value={periodMonths}
            disabled={!editable}
            onChange={(e) => setPeriod(e.target.value)}
            error={fieldError.periodMonths}
          />
          <Field
            id="plan-price"
            label="Цена, ₽"
            inputMode="numeric"
            value={priceRub}
            disabled={!editable}
            onChange={(e) => setPrice(e.target.value)}
            error={fieldError.priceRub}
          />
          <Field
            id="plan-sort"
            label="Порядок"
            inputMode="numeric"
            value={sortOrder}
            disabled={!editable}
            onChange={(e) => setSort(e.target.value)}
          />
        </div>

        {perVisit > 0 ? (
          <p className="text-sm text-ink-2">
            Выходит <strong className="tabular-nums text-ink-1">{formatRub(perVisit)} ₽</strong> за
            визит.
          </p>
        ) : null}

        {error ? (
          <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        ) : null}

        <div className="flex flex-wrap gap-3 border-t border-border pt-5">
          {editable ? (
            <Button type="submit" disabled={busy}>
              {busy ? "Сохраняем…" : "Сохранить"}
            </Button>
          ) : null}
          <Button type="button" variant="secondary" onClick={onCancel}>
            {editable ? "Отмена" : "Закрыть"}
          </Button>
        </div>
      </form>
    </Card>
  );
}
