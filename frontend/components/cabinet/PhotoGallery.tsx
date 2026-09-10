"use client";

import { useState, type ChangeEvent } from "react";
import { buttonClasses } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import {
  ACCEPTED_IMAGE_TYPES,
  MAX_UPLOAD_BYTES,
  MEDIA_OWNER,
  deleteMedia,
  uploadPhoto,
  type MediaAsset,
} from "@/lib/api/media";

/**
 * Photographs attached to one burial site.
 *
 * The copy stresses privacy rather than features, because that is the actual question in a
 * client's mind here: these are pictures of a family grave, and the product's promise is that
 * nobody outside the invited circle can see them.
 */
export function PhotoGallery({
  siteId,
  ownerType = MEDIA_OWNER.burialSite,
  phase = "reference",
  initialPhotos,
  canManage,
  title = "Фотографии",
  hint = "Снимки видны только вам и тем, кого вы пригласили. Ссылки на них временные, а публичного адреса у файлов нет.",
  emptyHint,
}: {
  /** Id of the owning record — a burial site or an order, per `ownerType`. */
  siteId: number;
  ownerType?: string;
  /** Which set these belong to — "reference", "before", "after". A visit report needs its two
   *  sets kept apart, so the phase is part of the upload rather than a property of the file. */
  phase?: string;
  initialPhotos: MediaAsset[];
  canManage: boolean;
  title?: string;
  hint?: string;
  emptyHint?: string;
}) {
  const [photos, setPhotos] = useState(initialPhotos);
  const [pending, setPending] = useState(0);
  const [error, setError] = useState<string | null>(null);

  async function handleFiles(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    // Reset immediately so picking the same file twice in a row still fires a change event.
    event.target.value = "";
    if (files.length === 0) return;

    setError(null);

    const tooBig = files.find((file) => file.size > MAX_UPLOAD_BYTES);
    if (tooBig) {
      setError(`«${tooBig.name}» больше ${MAX_UPLOAD_BYTES / (1024 * 1024)} МБ.`);
      return;
    }

    setPending((count) => count + files.length);

    // Sequential, not parallel: a phone on mobile data uploading six photos at once mostly
    // starves each of them, and one visible failure is easier to explain than six partial ones.
    for (const file of files) {
      try {
        const asset = await uploadPhoto(ownerType, siteId, file, phase);
        setPhotos((current) => [...current, asset]);
      } catch (err) {
        setError(
          err instanceof ApiError
            ? err.message
            : err instanceof Error
              ? err.message
              : "Не удалось загрузить фотографию.",
        );
      } finally {
        setPending((count) => count - 1);
      }
    }
  }

  async function handleDelete(assetId: number) {
    setError(null);
    const previous = photos;
    setPhotos((current) => current.filter((p) => p.id !== assetId));
    try {
      await deleteMedia(assetId);
    } catch (err) {
      setPhotos(previous);
      setError(err instanceof ApiError ? err.message : "Не удалось удалить фотографию.");
    }
  }

  // A labelled region: the gallery is a distinct area of a long page, and naming it lets a
  // screen-reader user jump straight to the photos instead of walking the whole order.
  const headingId = `photo-gallery-${ownerType}-${siteId}-${phase}`;

  return (
    <Card as="section" aria-labelledby={headingId} className="flex flex-col gap-5">
      <div>
        <h2 id={headingId} className="font-display text-xl font-normal text-ink-1">
          {title}
        </h2>
        <p className="mt-2 text-sm text-ink-2">{hint}</p>
      </div>

      {photos.length > 0 ? (
        <ul className="grid list-none grid-cols-2 gap-3 p-0 sm:grid-cols-3 lg:grid-cols-4">
          {photos.map((photo, index) => (
            <li key={photo.id} className="group relative">
              <a
                href={photo.url ?? "#"}
                target="_blank"
                rel="noreferrer"
                className="block overflow-hidden rounded-card bg-surface-raised outline outline-1 -outline-offset-1 outline-black/10"
              >
                {/* Plain <img>: the source is a signed URL that expires, so Next's image
                    optimiser would cache a link that stops working. */}
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={photo.thumbnailUrl ?? photo.url ?? ""}
                  alt={`Фотография места памяти ${index + 1} из ${photos.length}`}
                  loading="lazy"
                  className="aspect-square w-full object-cover"
                />
              </a>
              {canManage ? (
                <button
                  type="button"
                  onClick={() => void handleDelete(photo.id)}
                  aria-label="Удалить фотографию"
                  className="absolute right-2 top-2 min-h-hit min-w-hit rounded-pill border border-border-strong bg-surface-raised px-3 text-sm text-ink-2 opacity-0 transition-opacity duration-ds focus-visible:opacity-100 group-hover:opacity-100"
                >
                  Удалить
                </button>
              ) : null}
            </li>
          ))}
        </ul>
      ) : pending === 0 ? (
        <p className="text-sm text-ink-2">
          {canManage
            ? (emptyHint ??
              "Пока фотографий нет. Добавьте снимки места — они помогут исполнителю найти его и убедиться, что он на месте.")
            : "Пока фотографий нет."}
        </p>
      ) : null}

      {pending > 0 ? (
        <p role="status" className="text-sm text-ink-2">
          Загружаем и обрабатываем… осталось: {pending}
        </p>
      ) : null}

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      {canManage ? (
        <div className="flex flex-wrap items-center gap-3 border-t border-border pt-4">
          {/* The file input is the real control and stays in the tab order; the styled <label>
              is its visible face. Driving a visually hidden input from a separate button left it
              focusable with no accessible name — a screen reader announced "file upload" and
              nothing else. `peer-focus-visible` moves the focus ring onto the label, since the
              input itself has no visible box to draw one around. */}
          <input
            type="file"
            accept={ACCEPTED_IMAGE_TYPES.join(",")}
            multiple
            onChange={handleFiles}
            disabled={pending > 0}
            className="peer sr-only"
            id={`photo-input-${headingId}`}
          />
          <label
            htmlFor={`photo-input-${headingId}`}
            className={buttonClasses({
              variant: "secondary",
              size: "sm",
              className:
                "cursor-pointer peer-focus-visible:outline peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-accent peer-disabled:pointer-events-none peer-disabled:opacity-45",
            })}
          >
            {pending > 0 ? "Загружаем…" : "Добавить фотографии"}
          </label>
          <span className="text-xs text-ink-2">
            JPEG, PNG, WebP или HEIC, до {MAX_UPLOAD_BYTES / (1024 * 1024)} МБ
          </span>
        </div>
      ) : null}
    </Card>
  );
}
