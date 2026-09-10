"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { PhotoGallery } from "@/components/cabinet/PhotoGallery";
import { ApiError } from "@/lib/api/client";
import { formatRub } from "@/lib/api/catalog";
import { MEDIA_OWNER, type MediaAsset } from "@/lib/api/media";
import { executorVisits, type Visit } from "@/lib/api/visits";
import { cn } from "@/lib/ui/cn";

const RESULTS = [
  { value: "done", label: "Сделано" },
  { value: "impossible", label: "Не удалось" },
  { value: "not_required", label: "Не потребовалось" },
] as const;

/** Anything but "done" has to say why. The server refuses the report otherwise, and finding that
 *  out after filling in the whole form is a poor way to learn it. */
const NEEDS_NOTE = new Set(["impossible", "not_required"]);

interface Answer {
  result: string;
  note: string;
}

/**
 * Where an executor does the job: the checklist, the photographs, and filing the report.
 *
 * The two photo sets are separate and both required. "Before" and "after" only mean anything as a
 * pair — one picture of a tidy grave proves nothing about who tidied it.
 */
export function VisitWorkspace({
  visit,
  beforePhotos,
  afterPhotos,
}: {
  visit: Visit;
  beforePhotos: MediaAsset[];
  afterPhotos: MediaAsset[];
}) {
  const router = useRouter();
  const [answers, setAnswers] = useState<Record<string, Answer>>(() =>
    Object.fromEntries(
      visit.checklist.map((item) => [
        item.key,
        { result: item.result === "pending" ? "done" : item.result, note: item.note ?? "" },
      ]),
    ),
  );
  const [note, setNote] = useState(visit.executorNote ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  const missingNote = visit.checklist.find(
    (item) => NEEDS_NOTE.has(answers[item.key]?.result ?? "") && !answers[item.key]?.note.trim(),
  );

  const canSubmit = !missingNote && beforePhotos.length > 0 && afterPhotos.length > 0;
  const working = visit.status === "in_progress" || visit.status === "rework";

  return (
    <div className="flex flex-col gap-6">
      {visit.status === "rework" && visit.reviewNote ? (
        <Card as="section" aria-labelledby="rework-heading" className="border-danger bg-danger-soft">
          <h2 id="rework-heading" className="font-display text-lg font-normal text-danger">
            Отчёт вернули на доработку
          </h2>
          <p className="mt-2 whitespace-pre-line text-sm text-ink-1">{visit.reviewNote}</p>
        </Card>
      ) : null}

      <Card as="section" aria-labelledby="where-heading" className="flex flex-col gap-4">
        <h2 id="where-heading" className="font-display text-xl font-normal text-ink-1">
          Куда ехать
        </h2>
        <dl className="grid gap-4 sm:grid-cols-2">
          <div>
            <dt className="text-sm text-ink-2">Кладбище</dt>
            <dd className="mt-0.5 text-base text-ink-1">{visit.cemeteryName ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-sm text-ink-2">Участок</dt>
            <dd className="mt-0.5 text-base text-ink-1">{visit.plotSection ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-sm text-ink-2">Захоронение</dt>
            <dd className="mt-0.5 text-base text-ink-1">{visit.deceasedFullName}</dd>
          </div>
          {visit.payoutRub !== null ? (
            <div>
              <dt className="text-sm text-ink-2">Ваше вознаграждение</dt>
              <dd className="mt-0.5 text-base tabular-nums text-ink-1">
                {formatRub(visit.payoutRub)} ₽
              </dd>
            </div>
          ) : null}
          {visit.landmarks ? (
            <div className="sm:col-span-2">
              <dt className="text-sm text-ink-2">Ориентиры</dt>
              <dd className="mt-0.5 whitespace-pre-line text-base text-ink-1">{visit.landmarks}</dd>
            </div>
          ) : null}
        </dl>
      </Card>

      {error ? (
        <p role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </p>
      ) : null}

      {visit.status === "offered" ? (
        <Card className="flex flex-wrap gap-3">
          <Button disabled={busy} onClick={() => void run(() => executorVisits.accept(visit.id))}>
            Принять визит
          </Button>
          <Button
            variant="ghost"
            disabled={busy}
            onClick={() => {
              const reason = window.prompt("Почему не сможете взять?");
              if (reason === null) return;
              void run(() => executorVisits.decline(visit.id, reason));
            }}
          >
            Отказаться
          </Button>
        </Card>
      ) : null}

      {visit.status === "accepted" ? (
        <Card className="flex flex-col gap-3">
          <p className="text-sm text-ink-2">
            Нажмите, когда будете на месте — после этого можно снимать «до».
          </p>
          <div>
            <Button disabled={busy} onClick={() => void run(() => executorVisits.start(visit.id))}>
              Я на месте
            </Button>
          </div>
        </Card>
      ) : null}

      {working ? (
        <>
          <PhotoGallery
            siteId={visit.id}
            ownerType={MEDIA_OWNER.visit}
            phase="before"
            initialPhotos={beforePhotos}
            canManage
            title="Фотографии «до»"
            hint="Снимите участок до работ. Эти кадры клиент увидит рядом с «после» — снимайте так, чтобы их можно было сравнить."
            emptyHint="Без снимка «до» отчёт не примут."
          />

          <PhotoGallery
            siteId={visit.id}
            ownerType={MEDIA_OWNER.visit}
            phase="after"
            initialPhotos={afterPhotos}
            canManage
            title="Фотографии «после»"
            hint="Тот же ракурс, что и «до». Сравнение — это и есть результат, за который платит клиент."
            emptyHint="Без снимка «после» отчёт не примут."
          />

          <Card as="section" aria-labelledby="checklist-heading" className="flex flex-col gap-5">
            <div>
              <h2 id="checklist-heading" className="font-display text-xl font-normal text-ink-1">
                Чек-лист
              </h2>
              <p className="mt-2 text-sm text-ink-2">
                Отметьте каждый пункт. Если что-то не удалось — напишите почему; это увидит клиент.
              </p>
            </div>

            <ul className="flex list-none flex-col gap-5 p-0">
              {visit.checklist.map((item) => {
                const answer = answers[item.key] ?? { result: "done", note: "" };
                const needsNote = NEEDS_NOTE.has(answer.result);

                return (
                  <li
                    key={item.key}
                    className="flex flex-col gap-2 border-b border-border pb-5 last:border-0 last:pb-0"
                  >
                    <fieldset>
                      <legend className="text-base text-ink-1">
                        {item.title}
                        {item.optional ? (
                          <span className="ml-2 text-sm text-ink-2">необязательно</span>
                        ) : null}
                      </legend>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {RESULTS.map((option) => (
                          <label
                            key={option.value}
                            className={cn(
                              "inline-flex min-h-hit cursor-pointer items-center rounded-pill border px-4 text-sm transition-colors duration-ds",
                              "focus-within:shadow-focus",
                              answer.result === option.value
                                ? "border-accent bg-accent-soft font-semibold text-accent-deep"
                                : "border-border-strong text-ink-2 hover:border-accent",
                            )}
                          >
                            <input
                              type="radio"
                              name={`result-${item.key}`}
                              value={option.value}
                              checked={answer.result === option.value}
                              onChange={() =>
                                setAnswers((c) => ({
                                  ...c,
                                  [item.key]: { ...answer, result: option.value },
                                }))
                              }
                              className="sr-only"
                            />
                            {option.label}
                          </label>
                        ))}
                      </div>
                    </fieldset>

                    {needsNote ? (
                      <>
                        <label htmlFor={`note-${item.key}`} className="text-sm text-ink-2">
                          Почему
                        </label>
                        <input
                          id={`note-${item.key}`}
                          value={answer.note}
                          onChange={(e) =>
                            setAnswers((c) => ({
                              ...c,
                              [item.key]: { ...answer, note: e.target.value },
                            }))
                          }
                          placeholder="Кран перекрыт на зиму"
                          className="min-h-hit rounded-input border border-border-strong bg-surface-raised px-3 py-2 text-base text-ink-1 outline-none focus-visible:shadow-focus"
                        />
                      </>
                    ) : null}
                  </li>
                );
              })}
            </ul>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="executor-note" className="text-sm font-semibold text-ink-1">
                Комментарий клиенту
              </label>
              <textarea
                id="executor-note"
                rows={3}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Что нашли на месте, что сделали"
                aria-describedby="executor-note-hint"
                className="rounded-input border border-border-strong bg-surface-raised px-3 py-2 text-base text-ink-1 outline-none focus-visible:shadow-focus"
              />
              <p id="executor-note-hint" className="text-sm text-ink-2">
                Это увидит клиент. Суммы вознаграждения сюда не пишем.
              </p>
            </div>

            {!canSubmit ? (
              <p id="submit-blockers" className="text-sm text-ink-2">
                {missingNote
                  ? `Напишите, почему не удалось: «${missingNote.title}».`
                  : "Нужны фотографии «до» и «после»."}
              </p>
            ) : null}

            <div>
              <Button
                disabled={busy || !canSubmit}
                aria-describedby={canSubmit ? undefined : "submit-blockers"}
                onClick={() =>
                  void run(() =>
                    executorVisits.submit(visit.id, {
                      note: note.trim() || null,
                      checklist: visit.checklist.map((item) => ({
                        key: item.key,
                        result: answers[item.key]?.result ?? "done",
                        note: answers[item.key]?.note.trim() || null,
                      })),
                    }),
                  )
                }
              >
                {busy ? "Отправляем…" : "Отправить отчёт"}
              </Button>
            </div>
          </Card>
        </>
      ) : null}
    </div>
  );
}
