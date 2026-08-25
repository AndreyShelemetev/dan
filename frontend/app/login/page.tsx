"use client";

// UI copy is hardcoded in Russian for now, since RU is the primary language
// while the product is being built. Wiring up next-intl (or similar) for
// RU/EN switching is a follow-up task, not part of this scaffolding.

import { useState, type FormEvent } from "react";
import type { ChangeEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { getDevOtp, requestOtp, verifyOtp } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/client";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";

type Step = "email" | "code";

function errorMessage(err: unknown): string {
  if (err instanceof ApiError) {
    return err.message || "Что-то пошло не так. Попробуйте ещё раз.";
  }
  return "Что-то пошло не так. Попробуйте ещё раз.";
}

/**
 * Where to land after a successful sign-in.
 *
 * middleware.ts puts the page the visitor was heading for into `?redirect=`.
 * Only same-site paths are honoured: an absolute URL here would turn the login
 * screen into an open redirect, which is exactly the shape a phishing link
 * wants. Anything that is not a plain "/path" falls back to the cabinet.
 */
function safeRedirect(target: string | null): string {
  if (!target || !target.startsWith("/") || target.startsWith("//")) {
    return "/cabinet";
  }
  return target;
}

export default function LoginPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { setUser } = useAuth();

  const [step, setStep] = useState<Step>("email");
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Only ever set when the API runs in Development, where it exposes the last code it "sent".
  // Everywhere else the endpoint is absent and this stays null, so the hint never renders.
  const [devCode, setDevCode] = useState<string | null>(null);

  // Never pre-ticked: a consent the person did not actively give is not a consent. Held on the
  // first step because that is where the flow is entered, and the same form both signs in and
  // registers — the browser cannot know which until the code is verified.
  const [acceptedLegal, setAcceptedLegal] = useState(false);
  const [consentError, setConsentError] = useState<string | null>(null);

  async function handleRequestCode(event: FormEvent) {
    event.preventDefault();
    setError(null);

    if (!acceptedLegal) {
      // Validated on submit rather than by disabling the button: a disabled control gives no
      // reason, and the reason is the useful part.
      setConsentError("Отметьте согласие, чтобы продолжить.");
      document.getElementById("accept-legal")?.focus();
      return;
    }

    setConsentError(null);
    setIsSubmitting(true);
    try {
      await requestOtp(email);
      setStep("code");
      setDevCode(await getDevOtp(email));
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleVerifyCode(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const user = await verifyOtp(email, code, acceptedLegal);
      setUser(user);
      router.push(safeRedirect(searchParams.get("redirect")));
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main
      id="main"
      className="flex flex-1 flex-col items-center justify-center gap-8 px-6 py-14 md:py-22"
    >
      <Link
        href="/"
        className="inline-flex min-h-hit items-center font-display text-lg font-medium tracking-wordmark text-ink-1 no-underline hover:text-ink-1"
      >
        Память рядом
      </Link>

      <div className="w-full max-w-sm rounded-modal border border-border bg-surface-raised p-6 shadow-raised sm:p-8">
        <div className="text-center">
          <h1 className="font-display text-xl font-normal text-ink-1">Вход</h1>
          <p className="mt-2 text-sm text-ink-2">
            {step === "email"
              ? "Введите email — мы пришлём одноразовый код"
              : `Код отправлен на ${email}`}
          </p>
        </div>

        {step === "email" ? (
          <form onSubmit={handleRequestCode} className="mt-8 flex flex-col gap-5">
            <Field
              id="email"
              name="email"
              type="email"
              label="Email"
              required
              autoFocus
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="you@example.com"
            />

            <LegalConsentCheckbox
              checked={acceptedLegal}
              error={consentError}
              onChange={(event) => {
                setAcceptedLegal(event.target.checked);
                if (event.target.checked) setConsentError(null);
              }}
            />

            {error && <FormError message={error} />}

            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Отправляем…" : "Получить код"}
            </Button>
          </form>
        ) : (
          <form onSubmit={handleVerifyCode} className="mt-8 flex flex-col gap-5">
            <Field
              id="code"
              name="code"
              type="text"
              label="Код из письма"
              inputMode="numeric"
              required
              autoFocus
              autoComplete="one-time-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder="123456"
            />

            {devCode && (
              <DevCodeHint code={devCode} onUse={() => setCode(devCode)} />
            )}

            {error && <FormError message={error} />}

            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Проверяем…" : "Подтвердить"}
            </Button>

            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => {
                setStep("email");
                setCode("");
                setError(null);
              }}
            >
              Изменить email
            </Button>
          </form>
        )}
      </div>
    </main>
  );
}

/**
 * The consent gate on registration.
 *
 * The label wraps the control so the whole line is one hit target with no dead zone between the
 * box and its text. The two documents are separate instruments under 152-ФЗ — a policy the
 * operator publishes, and a consent the person gives — so both are named and both are reachable
 * before agreeing, rather than folded into one "я согласен со всем".
 */
function LegalConsentCheckbox({
  checked,
  error,
  onChange,
}: {
  checked: boolean;
  error: string | null;
  onChange: (event: ChangeEvent<HTMLInputElement>) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor="accept-legal" className="flex cursor-pointer items-start gap-3 text-sm text-ink-2">
        <input
          id="accept-legal"
          name="acceptLegal"
          type="checkbox"
          checked={checked}
          onChange={onChange}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? "accept-legal-error" : undefined}
          // 24px, the WCAG 2.5.8 baseline. The wrapping label already makes the whole line a
          // target, but a box that measures under the minimum invites the question every time.
          className="mt-0.5 size-6 shrink-0 accent-[color:var(--accent)]"
        />
        <span>
          Я даю{" "}
          <Link href="/legal/consent/" className="text-accent hover:text-accent-deep">
            согласие на обработку персональных данных
          </Link>{" "}
          и принимаю{" "}
          <Link href="/legal/privacy/" className="text-accent hover:text-accent-deep">
            политику обработки персональных данных
          </Link>
          .
        </span>
      </label>
      {error ? (
        <p id="accept-legal-error" role="alert" className="text-xs text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}

/**
 * The code the dev API just "sent", shown so local testing does not mean reading container logs.
 *
 * Rendered only when the API handed one back, which only a Development API ever does. Labelled
 * explicitly rather than styled to blend in: it should read as scaffolding, so nobody mistakes it
 * for a feature or wonders why it is missing on a real deployment.
 */
function DevCodeHint({ code, onUse }: { code: string; onUse: () => void }) {
  return (
    <div className="flex flex-wrap items-center gap-3 rounded-card border border-dashed border-border-strong bg-surface px-4 py-3">
      <span className="text-xs uppercase tracking-caps text-ink-2">Режим разработки</span>
      <code className="font-mono text-base font-semibold tracking-widest text-ink-1">{code}</code>
      <button
        type="button"
        onClick={onUse}
        className="min-h-hit text-sm text-accent underline underline-offset-2 hover:text-accent-deep"
      >
        Подставить
      </button>
    </div>
  );
}

/**
 * Submission errors are surfaced as a muted terracotta notice rather than an
 * alarm red — see guidelines/colors-semantic.html.
 */
function FormError({ message }: { message: string }) {
  return (
    <p role="alert" className="rounded-card bg-danger-soft px-4 py-3 text-sm text-danger">
      {message}
    </p>
  );
}
