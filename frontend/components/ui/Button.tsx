import { forwardRef, type ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/ui/cn";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
export type ButtonSize = "sm" | "md" | "lg";

/**
 * Pill buttons per `three wariants design/components/forms/Button.jsx` and
 * guidelines/radius-shadow.html. Every size clears the 44px tap target the
 * ТЗ requires for one-handed mobile use, so `min-h-hit` is unconditional.
 */
// `scale-[0.96]` is the exact value better-ui prescribes for press feedback; anything lower
// reads as exaggerated. It is layered on top of the colour change rather than replacing it, so
// the state is still legible when reduced motion collapses the transition.
const BASE =
  "inline-flex min-h-hit items-center justify-center gap-2 whitespace-nowrap rounded-pill border font-sans font-semibold transition-[colors,scale] duration-ds ease-ds active:scale-[0.96] disabled:pointer-events-none disabled:opacity-45";

/**
 * Every variant states its hover colour explicitly, including the ones whose text does not
 * change. ButtonLink renders an `<a>`, and globals.css darkens links on hover
 * (`a:hover { color: var(--accent-deep) }`) — a rule with higher specificity (0,1,1) than a bare
 * Tailwind colour utility (0,1,0). Without a `hover:text-*` here, the label on a filled button
 * turned dark green on dark green and became unreadable. `hover:text-…` is class + pseudo-class
 * (0,2,0), so it wins.
 */
const VARIANTS: Record<ButtonVariant, string> = {
  primary:
    "border-transparent bg-accent text-ink-inverse hover:bg-accent-hover hover:text-ink-inverse active:bg-accent-deep",
  secondary:
    "border-border-strong bg-transparent text-ink-1 hover:border-accent hover:text-accent-deep",
  ghost: "border-transparent bg-transparent text-accent hover:bg-accent-soft hover:text-accent-deep",
  danger: "border-transparent bg-danger text-ink-inverse hover:bg-danger/90 hover:text-ink-inverse",
};

const SIZES: Record<ButtonSize, string> = {
  sm: "px-5 py-2.5 text-sm",
  md: "px-6 py-3 text-base",
  lg: "px-9 py-4 text-base",
};

export interface ButtonStyleOptions {
  variant?: ButtonVariant;
  size?: ButtonSize;
  className?: string;
}

/** Shared with ButtonLink so an anchor CTA and a real button look identical. */
export function buttonClasses({
  variant = "primary",
  size = "md",
  className,
}: ButtonStyleOptions = {}): string {
  return cn(BASE, VARIANTS[variant], SIZES[size], className);
}

export interface ButtonProps
  extends ButtonHTMLAttributes<HTMLButtonElement>,
    ButtonStyleOptions {}

/** Forwards a ref so callers that own focus — a menu returning focus to its trigger on Escape,
 *  a form focusing the first invalid control — can reach the real element. */
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant, size, className, type = "button", ...rest },
  ref,
) {
  return <button ref={ref} type={type} className={buttonClasses({ variant, size, className })} {...rest} />;
});
