"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { qa, type Visit } from "@/lib/api/visits";
import type { MediaAsset } from "@/lib/api/media";

/**
 * Quality control on one report, before the client is allowed to see it.
 *
 * "Before" and "after" sit side by side because that comparison is the review — a reviewer
 * scrolling between two separate lists is not comparing anything. Sending work back demands a
 * reason: a rejection with none is not review, it is an obstacle the executor has to guess at.
 */
export function QaReview({
  visit,
  before,
  after,
}: {
  visit: Visit;
  before: MediaAsset[];
  after: MediaAsset[];
}) {
  const router = useRouter();
  const [note, setNote] = useState("");
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

  return (
    <Card as="article" className="flex flex-col gap-5">
      <div className="flex flex-wrap items-baseline justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-normal text-ink-1">{visit.deceasedFullName}</h2>
          <p className="mt-1 text-sm text-ink-2">
            {visit.orderNumber}
            {visit.cemeteryName ? ` · ${visit.cemeteryName}` : ""}
            {visit.executorName ? ` · ${visit.executorName}` : ""}
          </p>
        </div>
        {visit.submittedAt ? (
          <time dateTime={visit.submittedAt} className="text-sm tabular-nums text-ink-2">
            {new Date(visit.submittedAt).toLocaleString("ru-RU")}
          </time>
        ) : null}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <PhotoColumn title="До" photos={before} />
        <PhotoColumn title="После" photos={after} />
      </div>

      {visit.executorNote ? (
        <div>
          <h3 className="text-sm font-semibold text-ink-1">Комментарий исполнителя</h3>
          <p className="mt-1 whitespace-pre-line text-sm text-ink-1">{visit.executorNote}</p>
        </div>
      ) : null}

      {visit.checklist.length > 0 ? (
        <div>
          <h3 className="text-sm font-semibold text-ink-1">Чек-лист</h3>
          <ul className="mt-2 flex list-none flex-col gap-1 p-0">
            {visit.checklist.map((item) => (
              <li key={item.key} className="flex flex-wrap items-baseline gap-x-3 text-sm">
                <span className="text-ink-1">{item.title}</span>
                <span className="text-ink-2">{item.resultLabel}</span>
                {item.note ? <span className="basis-full text-ink-2">{item.note}</span> : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {error ? (
        <p role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </p>
      ) : null}

      <div className="flex flex-col gap-3 border-t border-border pt-5">
        <label htmlFor={`rework-${visit.id}`} className="text-sm font-semibold text-ink-1">
          Что переделать
        </label>
        <textarea
          id={`rework-${visit.id}`}
          rows={2}
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder="На фото «после» не видно всей плиты — переснимите с ракурса «до»"
          aria-describedby={`rework-hint-${visit.id}`}
          className="rounded-input border border-border-strong bg-surface-raised px-3 py-2 text-base text-ink-1 outline-none focus-visible:shadow-focus"
        />
        <p id={`rework-hint-${visit.id}`} className="text-sm text-ink-2">
          Нужно только для возврата на доработку. Клиент это не увидит.
        </p>

        <div className="flex flex-wrap gap-3">
          <Button disabled={busy} onClick={() => void run(() => qa.approve(visit.id))}>
            Принять и показать клиенту
          </Button>
          <Button
            variant="secondary"
            disabled={busy || note.trim().length === 0}
            onClick={() => void run(() => qa.sendBack(visit.id, note.trim()))}
          >
            Вернуть на доработку
          </Button>
        </div>
      </div>
    </Card>
  );
}

function PhotoColumn({ title, photos }: { title: string; photos: MediaAsset[] }) {
  return (
    <section aria-label={`Фотографии «${title.toLowerCase()}»`}>
      <h3 className="text-sm font-semibold text-ink-1">
        {title} <span className="tabular-nums font-normal text-ink-2">({photos.length})</span>
      </h3>
      {photos.length === 0 ? (
        <p className="mt-2 text-sm text-ink-2">Нет снимков.</p>
      ) : (
        <ul className="mt-2 grid list-none grid-cols-2 gap-2 p-0">
          {photos.map((photo, index) => (
            <li key={photo.id}>
              <a
                href={photo.url ?? "#"}
                target="_blank"
                rel="noreferrer"
                className="block overflow-hidden rounded-card bg-surface-raised outline outline-1 -outline-offset-1 outline-black/10"
              >
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={photo.thumbnailUrl ?? photo.url ?? ""}
                  alt={`${title}: снимок ${index + 1} из ${photos.length}`}
                  loading="lazy"
                  className="aspect-square w-full object-cover"
                />
              </a>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
