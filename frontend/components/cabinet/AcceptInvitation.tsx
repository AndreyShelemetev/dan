"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { Card } from "@/components/ui/Card";
import { ApiError } from "@/lib/api/client";
import { acceptInvitation, type BurialSite } from "@/lib/api/burialSites";

/**
 * Accepting is an explicit action, not something that happens on page load.
 *
 * Two reasons: the token burns on first use, so a link preview fetcher or a
 * mis-click would consume it; and joining someone's family record is a decision
 * worth confirming rather than performing silently on navigation.
 */
export function AcceptInvitation({ token }: { token: string }) {
  const router = useRouter();
  const [state, setState] = useState<"idle" | "working" | "done">("idle");
  const [site, setSite] = useState<BurialSite | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleAccept() {
    setError(null);
    setState("working");
    try {
      const accepted = await acceptInvitation(token);
      setSite(accepted);
      setState("done");
      router.refresh();
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : "Не удалось принять приглашение. Попробуйте позже.",
      );
      setState("idle");
    }
  }

  if (state === "done" && site) {
    return (
      <Card className="flex flex-col gap-4">
        <p className="text-base text-ink-1">
          Готово. Теперь у вас есть доступ к месту памяти{" "}
          <strong className="font-semibold">{site.deceasedFullName}</strong>.
        </p>
        <div className="flex flex-wrap gap-3">
          {/* ButtonLink rather than a hand-styled Link: the shared variants carry the hover
              colours that keep a filled label readable, which duplicated classes here did not. */}
          <ButtonLink href={`/cabinet/${site.id}`}>Открыть карточку</ButtonLink>
          <ButtonLink href="/cabinet" variant="secondary">
            Все места памяти
          </ButtonLink>
        </div>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col gap-5">
      <p className="text-sm text-ink-2">
        Приняв приглашение, вы увидите описание места, фотографии и историю визитов.
        Ссылка действует один раз.
      </p>

      {error ? (
        <div role="alert" className="rounded-card border border-danger bg-danger-soft px-4 py-3 text-sm text-danger">
          {error}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Button onClick={() => void handleAccept()} disabled={state === "working"}>
          {state === "working" ? "Принимаем…" : "Принять приглашение"}
        </Button>
        <Link href="/cabinet" className="text-sm text-ink-2 hover:text-accent-deep">
          Не сейчас
        </Link>
      </div>
    </Card>
  );
}
