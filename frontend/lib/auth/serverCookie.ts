import { cookies } from "next/headers";
import { AUTH_COOKIE_NAME } from "./constants";

/**
 * The session cookie formatted as a literal `Cookie` header, for Server
 * Component data fetching.
 *
 * Server-side calls run in Node, where there is no browser cookie jar for
 * `credentials: "include"` to draw on, so the cookie has to be forwarded by
 * hand — the same reason `getMe()` takes a `cookieHeader` argument.
 *
 * Returns undefined when there is no session, which lets the caller decide
 * between rendering an anonymous state and redirecting.
 */
export function getSessionCookieHeader(): string | undefined {
  const cookie = cookies().get(AUTH_COOKIE_NAME);
  return cookie ? `${AUTH_COOKIE_NAME}=${cookie.value}` : undefined;
}
