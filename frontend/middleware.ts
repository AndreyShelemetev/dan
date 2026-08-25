import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { AUTH_COOKIE_NAME } from "@/lib/auth/constants";

/**
 * Placeholder set of protected route prefixes — swap/extend once real
 * authenticated areas of the app exist. Anything not under these prefixes
 * is left untouched.
 */
const PROTECTED_PREFIXES = ["/cabinet", "/admin"];

function isProtectedPath(pathname: string): boolean {
  return PROTECTED_PREFIXES.some(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
  );
}

/**
 * Cheap, presence-only gate: redirects to /login if the session cookie is
 * flat-out missing. This is NOT full auth — it can't verify the cookie is
 * still valid (expired, revoked, wrong role, etc.), only that one exists.
 * Real validation happens server-side on every request via getServerUser()
 * (lib/auth/server.ts), which actually calls the backend. Middleware only
 * saves an obviously-anonymous visitor a round trip before anything renders.
 */
export function middleware(req: NextRequest) {
  const { pathname, search } = req.nextUrl;

  if (!isProtectedPath(pathname)) {
    return NextResponse.next();
  }

  if (req.cookies.has(AUTH_COOKIE_NAME)) {
    return NextResponse.next();
  }

  const loginUrl = req.nextUrl.clone();
  loginUrl.pathname = "/login";
  loginUrl.search = "";
  loginUrl.searchParams.set("redirect", `${pathname}${search}`);
  return NextResponse.redirect(loginUrl);
}

export const config = {
  matcher: ["/cabinet/:path*", "/admin/:path*"],
};
