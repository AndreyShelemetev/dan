import { cookies } from "next/headers";
import { ApiError } from "@/lib/api/client";
import { getMe, type AuthUser } from "@/lib/api/auth";
import { AUTH_COOKIE_NAME } from "./constants";

/**
 * Server-only helper for Server Components/layouts: reads the session
 * cookie via next/headers and asks the backend who it belongs to.
 *
 * Import only from server-side modules (Server Components, layouts, route
 * handlers) — it uses next/headers' `cookies()`, which throws if evaluated
 * on the client.
 *
 * Returns `null` for a missing/invalid/expired session or any request
 * failure rather than throwing, so callers (e.g. the root layout) can render
 * an anonymous state without wrapping every call site in try/catch.
 */
export async function getServerUser(): Promise<AuthUser | null> {
  const sessionCookie = cookies().get(AUTH_COOKIE_NAME);
  if (!sessionCookie) {
    return null;
  }

  try {
    return await getMe(`${AUTH_COOKIE_NAME}=${sessionCookie.value}`);
  } catch (err) {
    if (err instanceof ApiError && err.isUnauthorized) {
      return null;
    }
    // Network error, 5xx, backend not up yet, etc. — fail closed (render as
    // logged out) rather than throwing inside a layout render.
    return null;
  }
}
