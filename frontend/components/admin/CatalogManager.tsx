"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { PackageEditor, StatusBadge } from "@/components/admin/PackageEditor";
import { ApiError } from "@/lib/api/client";
import { adminCatalog, type AdminPackage } from "@/lib/api/adminCatalog";
import { formatRub } from "@/lib/api/catalog";

/**
 * The package list, grouped by code so every version of one package sits together.
 *
 * Grouping matters here: the catalogue accumulates a version per price change, and a flat list
 * sorted by date turns "what is on sale right now" into a puzzle.
 */
export function CatalogManager({ packages }: { packages: AdminPackage[] }) {
  const router = useRouter();
  const [editing, setEditing] = useState<AdminPackage | null>(null);
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

  async function newVersion(pkg: AdminPackage) {
    const suggested = bumpVersion(pkg.version);
    const version = window.prompt(`Номер новой версии пакета «${pkg.title}»`, suggested);
    if (!version) return;
    await run(pkg.id, () => adminCatalog.newPackageVersion(pkg.id, version));
  }

  if (creating || editing) {
    return (
      <PackageEditor
        initial={editing}
        onDone={() => {
          setEditing(null);
          setCreating(false);
        }}
        onCancel={() => {
          setEditing(null);
          setCreating(false);
        }}
      />
    );
  }

  const groups = groupByCode(packages);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-normal text-ink-1">Пакеты услуг</h1>
          <p className="mt-1 text-sm text-ink-2">
            Опубликованная версия неизменяема: заказ хранит те условия, по которым был продан.
            Чтобы изменить цену или состав — создайте новую версию.
          </p>
        </div>
        <Button onClick={() => setCreating(true)}>Новый пакет</Button>
      </div>

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      {groups.length === 0 ? (
        <Card>
          <p className="text-sm text-ink-2">
            Пакетов пока нет. Создайте первый — без него нечего продавать и не с чем сверять
            работу исполнителя.
          </p>
        </Card>
      ) : (
        groups.map(([code, versions]) => (
          <Card key={code} className="flex flex-col gap-4">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h2 className="font-display text-lg font-normal text-ink-1">{versions[0].title}</h2>
              <code className="text-xs text-ink-2">{code}</code>
            </div>

            <div className="-mx-2 overflow-x-auto">
              <table className="w-full min-w-[38rem] border-collapse text-sm">
                <thead>
                  <tr className="border-b border-border text-left text-xs uppercase tracking-caps text-ink-2">
                    <th className="px-2 py-2 font-semibold">Версия</th>
                    <th className="px-2 py-2 font-semibold">Статус</th>
                    <th className="px-2 py-2 font-semibold tabular-nums">Цена от</th>
                    <th className="px-2 py-2 font-semibold">Гарантия</th>
                    <th className="px-2 py-2 font-semibold">Чек-лист</th>
                    <th className="px-2 py-2 font-semibold">Действия</th>
                  </tr>
                </thead>
                <tbody>
                  {versions.map((pkg) => (
                    <tr key={pkg.id} className="border-b border-border last:border-0">
                      <td className="px-2 py-3 tabular-nums text-ink-1">{pkg.version}</td>
                      <td className="px-2 py-3">
                        <StatusBadge status={pkg.status} />
                      </td>
                      <td className="px-2 py-3 tabular-nums text-ink-1">
                        {formatRub(pkg.priceFromRub)} ₽
                      </td>
                      <td className="px-2 py-3 tabular-nums text-ink-2">{pkg.warrantyDays} дн.</td>
                      <td className="px-2 py-3 tabular-nums text-ink-2">
                        {pkg.checklistItems.length} п.
                      </td>
                      <td className="px-2 py-3">
                        <div className="flex flex-wrap gap-1">
                          <Button variant="ghost" size="sm" onClick={() => setEditing(pkg)}>
                            {pkg.editable ? "Изменить" : "Смотреть"}
                          </Button>

                          {pkg.status === "draft" ? (
                            <>
                              <Button
                                variant="ghost"
                                size="sm"
                                disabled={busyId === pkg.id}
                                onClick={() => void run(pkg.id, () => adminCatalog.publishPackage(pkg.id))}
                              >
                                Опубликовать
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                disabled={busyId === pkg.id}
                                onClick={() => {
                                  // Confirmed because it is irreversible. Only drafts reach this
                                  // button, so nothing sold can be lost — but a draft someone
                                  // spent an hour writing still can.
                                  if (!window.confirm(`Удалить черновик «${pkg.title}» версии ${pkg.version}? Это необратимо.`)) return;
                                  void run(pkg.id, () => adminCatalog.deletePackage(pkg.id));
                                }}
                                className="text-danger hover:bg-danger-soft hover:text-danger"
                              >
                                Удалить
                              </Button>
                            </>
                          ) : null}

                          {pkg.status === "published" ? (
                            <>
                              <Button variant="ghost" size="sm" onClick={() => void newVersion(pkg)}>
                                Новая версия
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                disabled={busyId === pkg.id}
                                onClick={() => void run(pkg.id, () => adminCatalog.archivePackage(pkg.id))}
                              >
                                В архив
                              </Button>
                            </>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        ))
      )}
    </div>
  );
}

function groupByCode(packages: AdminPackage[]): [string, AdminPackage[]][] {
  const map = new Map<string, AdminPackage[]>();
  for (const pkg of packages) {
    const list = map.get(pkg.code);
    if (list) list.push(pkg);
    else map.set(pkg.code, [pkg]);
  }
  return [...map.entries()];
}

/** "1.0" → "1.1". Only a suggestion — the editor types whatever they mean. */
function bumpVersion(version: string): string {
  const parts = version.split(".");
  const last = Number(parts[parts.length - 1]);
  if (!Number.isFinite(last)) return `${version}.1`;
  parts[parts.length - 1] = String(last + 1);
  return parts.join(".");
}
