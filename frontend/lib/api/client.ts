/**
 * Thin typed fetch wrapper for the backend API.
 *
 * - Base URL comes from NEXT_PUBLIC_API_URL (expected to include the
 *   `/api/v1` prefix, e.g. "http://localhost:5100/api/v1"), so callers pass
 *   paths like "/auth/me".
 * - Always sends `credentials: "include"` so the HttpOnly session cookie set
 *   by the backend flows on every request made from the browser.
 * - JSON in, JSON out.
 * - The backend wraps every controller response in an envelope
 *   `{ data, meta, errors }` (Dtos/Common/ApiResponse.cs). `apiFetch` returns
 *   the unwrapped `data` so call sites never see the envelope, and turns a
 *   non-2xx into a typed ApiError built from `errors[0]`.
 */

export interface ApiErrorDetail {
  code: string;
  message: string;
  details?: unknown;
}

/** Backend envelope: Dtos/Common/ApiResponse.cs. */
interface ApiEnvelope<T> {
  data?: T | null;
  meta?: unknown;
  errors?: ApiErrorDetail[] | null;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details: unknown;
  /** Every error the backend returned, not just the first one. */
  readonly errors: ApiErrorDetail[];

  constructor(status: number, errors: ApiErrorDetail[]) {
    const first = errors[0];
    super(first?.message || `API request failed (${status})`);
    this.name = "ApiError";
    this.status = status;
    this.code = first?.code ?? "http_error";
    this.details = first?.details;
    this.errors = errors;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isErrorDetail(value: unknown): value is ApiErrorDetail {
  return isRecord(value) && typeof value.code === "string" && typeof value.message === "string";
}

/**
 * Pulls errors out of whatever the backend produced. Two shapes exist:
 * the ApiResponse envelope (`errors: [{code, message, details}]`), and the
 * ValidationProblemDetails ASP.NET emits itself when [ApiController] model
 * binding rejects a body before the action runs (`errors` is a
 * field -> messages map, plus a `title`).
 */
function extractErrors(payload: unknown, status: number): ApiErrorDetail[] {
  if (isRecord(payload)) {
    const { errors } = payload;

    if (Array.isArray(errors)) {
      const parsed = errors.filter(isErrorDetail);
      if (parsed.length > 0) {
        return parsed;
      }
    }

    if (isRecord(errors)) {
      const parsed = Object.entries(errors).flatMap(([field, messages]) =>
        (Array.isArray(messages) ? messages : [messages])
          .filter((message): message is string => typeof message === "string")
          .map((message) => ({ code: "validation_error", message, details: { field } })),
      );
      if (parsed.length > 0) {
        return parsed;
      }
    }

    if (typeof payload.title === "string") {
      return [{ code: "validation_error", message: payload.title }];
    }
  }

  return [{ code: "http_error", message: `HTTP ${status}` }];
}

function safeJsonParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function getBaseUrl(): string {
  // Two different addresses reach the same API:
  //   - the browser uses NEXT_PUBLIC_API_URL (public host:port, baked into the
  //     client bundle at build time from PUBLIC_API_URL in .env);
  //   - server-side rendering runs inside the frontend container, where that
  //     public URL (e.g. http://localhost:5100) points at the container itself.
  //     API_INTERNAL_URL is the on-network address (http://api:8080/api/v1).
  // Without the second one every Server Component call — getServerUser() above
  // all — fails with ECONNREFUSED and silently renders as logged out.
  if (typeof window === "undefined" && process.env.API_INTERNAL_URL) {
    return process.env.API_INTERNAL_URL;
  }

  return process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5100/api/v1";
}

export interface ApiFetchOptions {
  method?: "GET" | "POST" | "PATCH" | "PUT" | "DELETE";
  body?: unknown;
  headers?: HeadersInit;
  cache?: RequestCache;
  signal?: AbortSignal;
}

export async function apiFetch<T>(
  path: string,
  options: ApiFetchOptions = {},
): Promise<T> {
  const base = getBaseUrl().replace(/\/+$/, "");
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  const url = `${base}${normalizedPath}`;

  const headers = new Headers(options.headers);
  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }

  let body: BodyInit | undefined;
  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
    body = JSON.stringify(options.body);
  }

  let response: Response;
  try {
    response = await fetch(url, {
      method: options.method ?? "GET",
      headers,
      body,
      // Always include cookies: the session cookie is HttpOnly and this is
      // how it reaches the backend from browser-side calls. Server-side
      // callers (lib/auth/server.ts) forward it explicitly via a Cookie
      // header instead, since there is no browser cookie jar in Node.
      credentials: "include",
      cache: options.cache ?? "no-store",
      signal: options.signal,
    });
  } catch (err) {
    throw new ApiError(0, [
      {
        code: "network_error",
        message:
          err instanceof Error ? err.message : "Не удалось связаться с сервером.",
      },
    ]);
  }

  const text = await response.text();
  const payload = text.length > 0 ? safeJsonParse(text) : null;

  if (!response.ok) {
    throw new ApiError(response.status, extractErrors(payload, response.status));
  }

  const envelope = (payload ?? {}) as ApiEnvelope<T>;
  return envelope.data as T;
}
