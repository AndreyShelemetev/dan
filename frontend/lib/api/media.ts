import { apiFetch } from "./client";

export interface MediaAsset {
  id: number;
  phase: string;
  status: string;
  width: number | null;
  height: number | null;
  /** Short-lived signed link, minted per response — there is no permanent URL. */
  url: string | null;
  thumbnailUrl: string | null;
  createdAt: string;
}

interface UploadSession {
  assetId: number;
  uploadUrl: string;
  contentType: string;
  expiresInSeconds: number;
}

export const MEDIA_OWNER = { burialSite: "burial_site" } as const;

/** What the picker offers and the backend accepts. HEIC is included because it is what an
 *  iPhone shoots by default. */
export const ACCEPTED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp", "image/heic"];

export const MAX_UPLOAD_BYTES = 20 * 1024 * 1024;

export function listMedia(
  ownerType: string,
  ownerId: number,
  cookieHeader?: string,
): Promise<MediaAsset[]> {
  return apiFetch<MediaAsset[]>(
    `/media?ownerType=${encodeURIComponent(ownerType)}&ownerId=${ownerId}`,
    { headers: cookieHeader ? { Cookie: cookieHeader } : undefined },
  );
}

export function deleteMedia(assetId: number): Promise<{ deleted: boolean }> {
  return apiFetch<{ deleted: boolean }>(`/media/${assetId}`, { method: "DELETE" });
}

/**
 * Uploads one photo in three steps: ask the API for a signed target, PUT the bytes straight to
 * storage, then tell the API to check and normalise them.
 *
 * The middle step deliberately bypasses our own server — the file never passes through the API
 * process, which keeps large uploads off it entirely.
 */
export async function uploadPhoto(
  ownerType: string,
  ownerId: number,
  file: File,
  phase = "reference",
): Promise<MediaAsset> {
  const session = await apiFetch<UploadSession>("/media/upload-sessions", {
    method: "POST",
    body: {
      ownerType,
      ownerId,
      phase,
      contentType: file.type,
      sizeBytes: file.size,
    },
  });

  const put = await fetch(session.uploadUrl, {
    method: "PUT",
    // No credentials: this goes to object storage, not our API, and sending the session
    // cookie to a third-party host would be a needless leak.
    body: file,
    headers: { "Content-Type": session.contentType },
  });

  if (!put.ok) {
    throw new Error("Не удалось загрузить файл в хранилище.");
  }

  // Only after this does the file become visible: the API verifies the bytes really are an
  // image, strips EXIF by re-encoding, and builds the thumbnail.
  return apiFetch<MediaAsset>(`/media/${session.assetId}/complete`, { method: "POST" });
}
