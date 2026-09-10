"use client";

import Link from "next/link";
import { AccountMenu } from "@/components/site/AccountMenu";

/**
 * Header for the signed-in area. Deliberately quieter than the public
 * SiteHeader: someone already inside their account is here to do one thing,
 * so the marketing nav is dropped and only the wordmark, the account it
 * belongs to and the way out remain.
 *
 * The account control is the same AccountMenu the public header uses. Two headers with two
 * different ways to sign out is how one of them ends up behaving differently — and this is where
 * a staff member actually lands after logging in, so it is also where they need the way into the
 * admin panel.
 */
export function CabinetHeader() {
  return (
    <header className="border-b border-border bg-surface">
      <div className="mx-auto flex w-full max-w-content items-center justify-between gap-4 px-6 py-4 lg:px-14">
        <Link
          href="/cabinet"
          className="inline-flex min-h-hit items-center font-display text-lg font-medium tracking-wordmark text-ink-1 no-underline hover:text-ink-1"
        >
          Память рядом
        </Link>

        <nav aria-label="Разделы кабинета" className="hidden sm:block">
          <ul className="flex list-none gap-1 p-0">
            {[
              { href: "/cabinet", label: "Места памяти" },
              { href: "/cabinet/orders", label: "Заказы" },
            ].map((item) => (
              <li key={item.href}>
                <Link
                  href={item.href}
                  className="inline-flex min-h-hit items-center rounded-pill px-4 text-sm text-ink-2 no-underline transition-colors duration-ds ease-ds hover:bg-accent-soft hover:text-accent-deep"
                >
                  {item.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>

        <AccountMenu />
      </div>
    </header>
  );
}
