"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { apiFetch } from "@/lib/api/client";

const STORAGE_KEY = "pamyat_cookie_choice";

/**
 * Cookie consent.
 *
 * Two things make this more than decoration. The choice is recorded server-side, because a
 * consent the operator cannot demonstrate is worth nothing under 152-ФЗ and a value in the
 * visitor's own browser storage proves nothing. And declining is recorded too — "asked and
 * declined" is a different fact from "never asked".
 *
 * Local storage still holds the answer, but only so the banner stops reappearing on this device.
 * It is a convenience, never the record.
 *
 * There is nothing to gate yet: no analytics script is loaded today. The consent is collected
 * before it is needed on purpose, so adding analytics later is a one-line read of a decision the
 * visitor already made, rather than a reason to start collecting retroactively.
 */
type Choice = "accepted" | "necessary-only";

export function CookieBanner() {
  const [visible, setVisible] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    // Read after mount, not during render: the server has no localStorage, and reading it during
    // render would make the markup differ between server and client.
    try {
      if (!window.localStorage.getItem(STORAGE_KEY)) {
        setVisible(true);
      }
    } catch {
      // Private mode or blocked storage. Showing the banner is the safe side of that failure.
      setVisible(true);
    }
  }, []);

  async function choose(choice: Choice) {
    setBusy(true);
    try {
      await apiFetch("/legal/cookie-consent", {
        method: "POST",
        body: { analytics: choice === "accepted" },
      });
    } catch {
      // A failed record must not trap the visitor behind the banner. The local note still hides
      // it; the server simply has no row, which is the truthful outcome of a failed request.
    }

    try {
      window.localStorage.setItem(STORAGE_KEY, choice);
    } catch {
      // Nothing to do — the banner will ask again next visit, which is not harmful.
    }

    setBusy(false);
    setVisible(false);
  }

  if (!visible) return null;

  return (
    <div
      // `region` + a label rather than `dialog`: it does not trap focus and the page stays
      // usable behind it. A visitor reading the cookie policy must be able to reach it.
      role="region"
      aria-label="Использование cookie"
      className="fixed inset-x-0 bottom-0 z-50 border-t border-border bg-surface-raised shadow-modal"
    >
      <div className="mx-auto flex w-full max-w-content flex-col gap-4 px-6 py-5 lg:flex-row lg:items-center lg:justify-between lg:px-14">
        <p className="max-w-measure text-sm text-ink-2">
          Мы используем cookie: часть из них необходима для входа в кабинет, остальные —
          аналитические, и они включаются только с вашего согласия. Подробнее — в{" "}
          <Link href="/legal/cookies/" className="text-accent hover:text-accent-deep">
            политике использования cookie
          </Link>
          .
        </p>

        <div className="flex flex-wrap items-center gap-3">
          <Button size="sm" disabled={busy} onClick={() => void choose("accepted")}>
            Принять
          </Button>
          <Button
            variant="secondary"
            size="sm"
            disabled={busy}
            onClick={() => void choose("necessary-only")}
          >
            Только необходимые
          </Button>
        </div>
      </div>
    </div>
  );
}
