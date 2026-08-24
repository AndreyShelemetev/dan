/**
 * Name of the HttpOnly session cookie the backend sets on
 * `POST /api/v1/auth/otp/verify`.
 *
 * Must stay in sync with `AuthOptions.CookieName`
 * (backend/src/PamyatRyadom.Api/Services/Auth/AuthOptions.cs).
 *
 * A plain constant on purpose, not an env lookup: middleware.ts reads it and
 * runs in the Edge runtime, where `process.env` is inlined at build time — an
 * env-var knob here would silently be ignored in exactly the place that most
 * needs it.
 */
export const AUTH_COOKIE_NAME = "pamyat_ryadom_auth";
