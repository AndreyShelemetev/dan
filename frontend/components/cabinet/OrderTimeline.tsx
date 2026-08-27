import { cn } from "@/lib/ui/cn";
import type { OrderHistoryEntry } from "@/lib/api/orders";

/**
 * What has happened to the order, in the client's language.
 *
 * The current step is marked with a word as well as a colour, because a state carried only by
 * colour is invisible to anyone who cannot distinguish it — the same rule the homepage stepper
 * follows.
 */
export function OrderTimeline({ history }: { history: OrderHistoryEntry[] }) {
  if (history.length === 0) return null;

  return (
    <ol className="flex list-none flex-col gap-0 p-0">
      {history.map((entry, index) => {
        const current = index === history.length - 1;
        return (
          <li key={`${entry.toStatus}-${entry.createdAt}`} className="flex gap-3">
            <div className="flex flex-col items-center">
              <span
                aria-hidden="true"
                className={cn(
                  "mt-1.5 size-3 shrink-0 rounded-full border",
                  current ? "border-accent bg-surface" : "border-accent bg-accent",
                )}
              />
              {index < history.length - 1 ? (
                <span aria-hidden="true" className="w-px flex-1 bg-border-strong" />
              ) : null}
            </div>
            <div className={cn("pb-5", current && "font-semibold")}>
              <p className="text-sm text-ink-1">
                {entry.label}
                {current ? (
                  <span className="ml-2 text-xs uppercase tracking-caps text-ink-2">сейчас</span>
                ) : null}
              </p>
              <p className="text-xs text-ink-2">
                {new Date(entry.createdAt).toLocaleString("ru-RU", {
                  day: "numeric",
                  month: "long",
                  hour: "2-digit",
                  minute: "2-digit",
                })}
                {entry.reason ? ` · ${entry.reason}` : ""}
              </p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
