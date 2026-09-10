"use client";

import { useEffect, useId, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui/Button";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { cn } from "@/lib/ui/cn";

const ADMIN_ROLES = ["admin", "superadmin"];
const STAFF_ROLES = ["dispatcher", "qa", "support", "finance", ...ADMIN_ROLES];

/**
 * The signed-in account control.
 *
 * Built by hand rather than with `<details>` because a disclosure does not close on Escape or on
 * an outside click, and both are what people expect from a menu. Everything the platform does
 * give is kept: a real `<button>` with `aria-expanded`, real `<a>` links inside, and focus
 * returning to the trigger on close.
 *
 * Signed out it is not a menu at all, just the sign-in link — a menu holding one item is worse
 * than the item.
 */
export function AccountMenu({ className }: { className?: string }) {
  const { user, isAuthenticated, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuId = useId();
  const pathname = usePathname();

  // Navigating away should never leave the menu hanging open over the new page.
  useEffect(() => setOpen(false), [pathname]);

  useEffect(() => {
    if (!open) return;

    function onPointerDown(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
        // Focus goes back to what opened it, or the keyboard user is left nowhere.
        triggerRef.current?.focus();
      }
    }

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  if (!isAuthenticated) {
    return (
      <ButtonLink href="/login" variant="secondary" size="sm" className={className}>
        Войти
      </ButtonLink>
    );
  }

  const isAdmin = user !== null && ADMIN_ROLES.includes(user.role);
  const isStaff = user !== null && STAFF_ROLES.includes(user.role);
  const label = user?.displayName || user?.email || "Аккаунт";

  const itemClass =
    "flex min-h-hit items-center gap-2 px-4 text-sm text-ink-1 no-underline transition-colors duration-ds ease-ds hover:bg-accent-soft hover:text-accent-deep";

  return (
    <div ref={containerRef} className={cn("relative", className)}>
      <Button
        ref={triggerRef}
        variant="secondary"
        size="sm"
        aria-expanded={open}
        aria-haspopup="menu"
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((v) => !v)}
      >
        <span className="max-w-[14ch] truncate">{label}</span>
        <span aria-hidden="true" className={cn("transition-transform duration-ds", open && "rotate-180")}>
          ▾
        </span>
      </Button>

      {open ? (
        <div
          id={menuId}
          role="menu"
          aria-label="Меню аккаунта"
          className="absolute right-0 top-[calc(100%+0.5rem)] z-50 flex min-w-56 flex-col overflow-hidden rounded-card border border-border bg-surface-raised py-1 shadow-modal"
        >
          {user?.email ? (
            <p className="truncate px-4 py-2 text-xs text-ink-2">{user.email}</p>
          ) : null}

          <Link href="/cabinet" role="menuitem" className={itemClass}>
            Мои места памяти
          </Link>
          <Link href="/cabinet/orders" role="menuitem" className={itemClass}>
            Мои заказы
          </Link>

          {isStaff ? (
            <>
              <span aria-hidden="true" className="my-1 h-px bg-border" />
              {isAdmin ? (
                <Link href="/admin/catalog" role="menuitem" className={itemClass}>
                  Админка
                </Link>
              ) : null}
              <Link href="/admin/queue" role="menuitem" className={itemClass}>
                Очередь заказов
              </Link>
            </>
          ) : null}

          <span aria-hidden="true" className="my-1 h-px bg-border" />
          <button
            type="button"
            role="menuitem"
            onClick={() => {
              setOpen(false);
              void logout();
            }}
            className={cn(itemClass, "w-full text-left")}
          >
            Выйти
          </button>
        </div>
      ) : null}
    </div>
  );
}
