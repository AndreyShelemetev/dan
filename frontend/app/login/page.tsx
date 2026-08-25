"use client";

// UI copy is hardcoded in Russian for now, since RU is the primary language
// while the product is being built. Wiring up next-intl (or similar) for
// RU/EN switching is a follow-up task, not part of this scaffolding.

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { requestOtp, verifyOtp } from "@/lib/api/auth";
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

  async function handleRequestCode(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await requestOtp(email);
      setStep("code");
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
      const user = await verifyOtp(email, code);
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
