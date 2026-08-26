"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui/Button";
import { cn } from "@/lib/ui/cn";

const TABS = [
  { href: "/admin/catalog", label: "Пакеты услуг" },
  { href: "/admin/plans", label: "Подписки" },
] as const;

/**
 * Admin chrome. Deliberately plainer than the client cabinet: this is a working tool, and the
 * calm-and-spacious treatment that suits a grieving relative gets in the way of someone editing
 * a price list.
 */
export function AdminNav() {
  const pathname = usePathname();
  const { user, logout } = useAuth();

  return (
    <header className="border-b border-border bg-surface">
      <div className="mx-auto flex w-full max-w-content flex-col gap-3 px-6 py-4 lg:px-14">
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-baseline gap-3">
            <Link
              href="/"
              className="font-display text-lg font-medium tracking-wordmark text-ink-1 no-underline hover:text-ink-1"
            >
              Память рядом
            </Link>
            <span className="rounded-pill border border-border-strong px-2.5 py-0.5 text-xs font-semibold uppercase tracking-caps text-ink-2">
              Админка
            </span>
          </div>

          <div className="flex items-center gap-3">
            {user?.email ? (
              <span className="hidden text-sm text-ink-2 sm:inline">{user.email}</span>
            ) : null}
            <Button variant="ghost" size="sm" onClick={() => void logout()}>
              Выйти
            </Button>
          </div>
        </div>

        <nav aria-label="Разделы админки">
          <ul className="flex list-none flex-wrap gap-1 p-0">
            {TABS.map((tab) => {
              const active = pathname.startsWith(tab.href);
              return (
                <li key={tab.href}>
                  <Link
                    href={tab.href}
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "inline-flex min-h-hit items-center rounded-pill px-4 text-sm no-underline transition-colors duration-ds ease-ds",
                      active
                        ? "bg-accent-soft font-semibold text-accent-deep"
                        : "text-ink-2 hover:bg-accent-soft hover:text-accent-deep",
                    )}
                  >
                    {tab.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
      </div>
    </header>
  );
}
