import { apiFetch } from "./client";

/**
 * Mirrors the backend's AuthUserDto
 * (backend/src/PamyatRyadom.Api/Dtos/Auth/AuthUserDto.cs).
 *
 * `role` and `status` stay `string` rather than a union: the backend keeps
 * them as text + CHECK constraint (see CONVENTIONS.md §3), and new values are
 * added there without a frontend change.
 */
export interface AuthUser {
  id: number;
  role: string;
  status: string;
  displayName: string | null;
  email: string | null;
  phone: string | null;
  mfaEnabled: boolean;
}

interface OtpVerifyResponse {
  user: AuthUser;
  isNewUser: boolean;
}

/**
 * Requests a one-time login code sent to `destination` over `channel`.
 * Only "email" is supported by the backend today (an "sms" request returns
 * 501); the parameter is still explicit so adding SMS later is a
 * non-breaking change at the call sites.
 */
export function requestOtp(
  destination: string,
  channel: "email" = "email",
): Promise<void> {
  return apiFetch<void>("/auth/otp/request", {
    method: "POST",
    body: { destination, channel },
  });
}

/**
 * Verifies a one-time code. On success the backend sets the HttpOnly session
 * cookie as a side effect of this response and returns the now-authenticated
 * user.
 */
export function verifyOtp(destination: string, code: string): Promise<AuthUser> {
  return apiFetch<OtpVerifyResponse>("/auth/otp/verify", {
    method: "POST",
    body: { destination, code, channel: "email" },
  }).then((res) => res.user);
}

export function logout(): Promise<void> {
  return apiFetch<void>("/auth/logout", { method: "POST" });
}

/**
 * Fetches the current session's user. Throws an ApiError with status 401
 * when there is no valid session — callers that want "logged out" instead
 * of a thrown error should catch that themselves (see
 * lib/auth/server.ts#getServerUser for the Server Component case).
 *
 * `cookieHeader`, when passed, is forwarded as a literal `Cookie` header.
 * It's only needed for server-side calls (Server Components/layouts), where
 * there is no browser cookie jar to rely on `credentials: "include"` for —
 * see lib/auth/server.ts.
 */
export function getMe(cookieHeader?: string): Promise<AuthUser> {
  return apiFetch<AuthUser>("/auth/me", {
    headers: cookieHeader ? { Cookie: cookieHeader } : undefined,
  });
}

/**
 * Development helper: the last login code sent to `destination`.
 *
 * Backed by an endpoint that Program.cs only maps when the API runs in Development — in any
 * other environment the route does not exist and this resolves to null. Callers must treat a
 * null as the normal case and render nothing, never as an error worth showing.
 */
export function getDevOtp(destination: string): Promise<string | null> {
  return apiFetch<{ destination: string; code: string }>(
    `/dev/last-otp?destination=${encodeURIComponent(destination)}`,
  )
    .then((res) => res.code)
    .catch(() => null);
}
