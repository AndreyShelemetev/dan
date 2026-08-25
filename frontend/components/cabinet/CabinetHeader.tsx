"use client";

import Link from "next/link";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui/Button";

/**
 * Header for the signed-in area. Deliberately quieter than the public
 * SiteHeader: someone already inside their account is here to do one thing,
 * so the marketing nav is dropped and only the wordmark, the account it
 * belongs to and the way out remain.
 */
export function CabinetHeader() {
  const { user, logout } = useAuth();

  return (
    <header className="border-b border-border bg-surface">
      <div className="mx-auto flex w-full max-w-content items-center justify-between gap-4 px-6 py-4 lg:px-14">
        <Link
          href="/cabinet"
          className="inline-flex min-h-hit items-center font-display text-lg font-medium tracking-wordmark text-ink-1 no-underline hover:text-ink-1"
        >
          Память рядом
        </Link>

        <div className="flex items-center gap-3">
          {user?.email ? (
            <span className="hidden text-sm text-ink-2 sm:inline" title={user.email}>
              {user.email}
            </span>
          ) : null}
          <Button variant="secondary" size="sm" onClick={() => void logout()}>
            Выйти
          </Button>
        </div>
      </div>
    </header>
  );
}
