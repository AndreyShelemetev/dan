import { cn } from "@/lib/ui/cn";

export type StatusStepState = "done" | "current" | "upcoming";

export interface StatusStep {
  label: string;
  state: StatusStepState;
}

export interface StatusStepperProps {
  steps: readonly StatusStep[];
  /** Accessible name for the list, e.g. the section heading it sits under. */
  ariaLabel: string;
  className?: string;
}

/**
 * Order progress, per the stepper in direction-a.dc.html.
 *
 * State is never carried by colour alone (guidelines/statuses.html: «Статус
 * никогда не передаётся только цветом — всегда текст»):
 *  - the dot changes *fill and weight*, not just hue — solid, ringed, hollow;
 *  - the current step carries a visible «Сейчас» label and heavier type;
 *  - every step exposes its state as text to assistive tech, and the current
 *    step is marked `aria-current="step"`.
 *
 * Layout is vertical up to `lg`, then horizontal inside its own scroll
 * container so long Russian labels can never force the page sideways.
 */
const DOT: Record<StatusStepState, string> = {
  done: "border-accent bg-accent",
  current: "border-2 border-accent bg-surface ring-4 ring-accent-soft",
  upcoming: "border-border-strong bg-surface",
};

const LABEL: Record<StatusStepState, string> = {
  done: "text-ink-1",
  current: "font-semibold text-ink-1",
  upcoming: "text-ink-2",
};

const STATE_TEXT: Record<StatusStepState, string> = {
  done: "шаг выполнен",
  current: "текущий шаг",
  upcoming: "шаг ожидается",
};

export function StatusStepper({ steps, ariaLabel, className }: StatusStepperProps) {
  return (
    <div className={cn("-m-2 overflow-x-auto p-2", className)}>
      <ol aria-label={ariaLabel} className="flex flex-col lg:flex-row lg:items-start">
        {steps.map((step, index) => {
          const isLast = index === steps.length - 1;

          return (
            <li
              key={step.label}
              aria-current={step.state === "current" ? "step" : undefined}
              className={cn(
                "flex gap-4 lg:flex-col lg:gap-3",
                isLast ? "lg:flex-none" : "lg:flex-1",
              )}
            >
              <div className="flex shrink-0 flex-col items-center lg:w-full lg:flex-row lg:items-center">
                <span
                  aria-hidden="true"
                  className={cn(
                    "mt-1 block h-3 w-3 shrink-0 rounded-full border lg:mt-0",
                    DOT[step.state],
                  )}
                />
                {!isLast && (
                  <span
                    aria-hidden="true"
                    className="mt-2 w-px flex-1 bg-border-strong lg:mx-4 lg:mt-0 lg:h-px lg:w-auto"
                  />
                )}
              </div>

              <div className={cn("min-w-0", isLast ? "pb-0" : "pb-6", "lg:pb-0")}>
                <p className={cn("text-sm lg:whitespace-nowrap", LABEL[step.state])}>
                  {step.label}
                  <span className="sr-only"> — {STATE_TEXT[step.state]}</span>
                </p>
                {step.state === "current" && (
                  <p aria-hidden="true" className="mt-1 text-xs uppercase tracking-caps text-accent">
                    Сейчас
                  </p>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
