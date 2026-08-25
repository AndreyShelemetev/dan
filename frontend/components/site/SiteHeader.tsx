"use client";

import { useState } from "react";
import Link from "next/link";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui/Button";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { cn } from "@/lib/ui/cn";

/**
 * Public site header, per direction-a.dc.html: wordmark, four nav items, the
 * RU/EN indicator and the «Войти» pill.
 *
 * The mockup is desktop-only, so below `lg` the nav collapses behind a
 * disclosure button — the wordmark and the sign-in action stay visible, since
 * those are the two things a one-handed mobile visitor actually reaches for.
 */
const NAV_ITEMS = [
  { label: "Услуги", href: "#services" },
  { label: "Как это работает", href: "#how-it-works" },
  { label: "Доверие и гарантии", href: "#trust" },
  { label: "Вопросы", href: "#support" },
] as const;

const NAV_LINK_CLASS =
  "flex min-h-hit items-center text-sm text-ink-2 no-underline transition-colors duration-ds ease-ds hover:text-accent-deep";

export function SiteHeader() {
  const { user, isAuthenticated, logout } = useAuth();
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  return (
    <header className="border-b border-border">
      <div className="mx-auto flex w-full max-w-content items-center justify-between gap-4 px-6 py-4 md:py-6 lg:px-14">
        <Link
          href="/"
          className="inline-flex min-h-hit items-center font-display text-lg font-medium tracking-wordmark text-ink-1 no-underline hover:text-ink-1"
        >
          Память рядом
        </Link>

        <nav aria-label="Основная навигация" className="hidden lg:block">
          <ul className="flex items-center gap-8">
            {NAV_ITEMS.map((item) => (
              <li key={item.href}>
                <a href={item.href} className={NAV_LINK_CLASS}>
                  {item.label}
                </a>
              </li>
            ))}
          </ul>
        </nav>

        <div className="flex items-center gap-3">
          <LanguageIndicator className="hidden sm:flex" />

          {isAuthenticated ? (
            <>
              {user?.displayName && (
                <span className="hidden max-w-[12ch] truncate text-sm text-ink-2 md:inline">
                  {user.displayName}
                </span>
              )}
              {/* The primary action for a signed-in visitor is getting to their
                  own records, not signing out — so the cabinet takes the filled
                  pill and «Выйти» steps back to a quiet link. */}
              <ButtonLink href="/cabinet" size="sm">
                Кабинет
              </ButtonLink>
              <Button variant="ghost" size="sm" onClick={() => void logout()}>
                Выйти
              </Button>
            </>
          ) : (
            <ButtonLink href="/login" variant="secondary" size="sm">
              Войти
            </ButtonLink>
          )}

          <button
            type="button"
            aria-expanded={isMenuOpen}
            aria-controls="site-menu"
            aria-label={isMenuOpen ? "Закрыть меню" : "Открыть меню"}
            onClick={() => setIsMenuOpen((open) => !open)}
            className="-mr-2 inline-flex h-hit w-hit items-center justify-center rounded-card text-ink-1 transition-colors duration-ds ease-ds hover:text-accent-deep lg:hidden"
          >
            <MenuIcon isOpen={isMenuOpen} />
          </button>
        </div>
      </div>

      <div
        id="site-menu"
        hidden={!isMenuOpen}
        className="border-t border-border bg-surface px-6 py-2 lg:hidden"
      >
        <nav aria-label="Основная навигация — мобильная">
          <ul className="flex flex-col">
            {NAV_ITEMS.map((item) => (
              <li key={item.href}>
                <a
                  href={item.href}
                  onClick={() => setIsMenuOpen(false)}
                  className={cn(NAV_LINK_CLASS, "w-full py-1")}
                >
                  {item.label}
                </a>
              </li>
            ))}
            <li className="sm:hidden">
              <LanguageIndicator className="min-h-hit" />
            </li>
          </ul>
        </nav>
      </div>
    </header>
  );
}

/**
 * RU/EN state indicator from the mockup. It is deliberately not a control:
 * no locale routing exists yet (see the note in app/login/page.tsx), and a
 * button that does nothing is worse than an honest status label.
 */
function LanguageIndicator({ className }: { className?: string }) {
  return (
    <p className={cn("flex items-center gap-1 text-sm text-ink-2", className)}>
      <span className="sr-only">Язык интерфейса: русский.</span>
      <span aria-hidden="true" className="font-semibold text-ink-1">
        RU
      </span>
      <span aria-hidden="true">/</span>
      <span aria-hidden="true">EN</span>
    </p>
  );
}

function MenuIcon({ isOpen }: { isOpen: boolean }) {
  return (
    <svg
      aria-hidden="true"
      width="22"
      height="22"
      viewBox="0 0 22 22"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
    >
      {isOpen ? (
        <>
          <path d="M4 4 L18 18" />
          <path d="M18 4 L4 18" />
        </>
      ) : (
        <>
          <path d="M3 6 H19" />
          <path d="M3 11 H19" />
          <path d="M3 16 H19" />
        </>
      )}
    </svg>
  );
}
