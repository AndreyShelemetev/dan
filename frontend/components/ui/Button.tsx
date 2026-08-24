import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/ui/cn";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
export type ButtonSize = "sm" | "md" | "lg";

/**
 * Pill buttons per `three wariants design/components/forms/Button.jsx` and
 * guidelines/radius-shadow.html. Every size clears the 44px tap target the
 * ТЗ requires for one-handed mobile use, so `min-h-hit` is unconditional.
 */
const BASE =
  "inline-flex min-h-hit items-center justify-center gap-2 whitespace-nowrap rounded-pill border font-sans font-semibold transition-colors duration-ds ease-ds disabled:pointer-events-none disabled:opacity-45";

const VARIANTS: Record<ButtonVariant, string> = {
  primary:
    "border-transparent bg-accent text-ink-inverse hover:bg-accent-hover active:bg-accent-deep",
  secondary:
    "border-border-strong bg-transparent text-ink-1 hover:border-accent hover:text-accent-deep",
  ghost: "border-transparent bg-transparent text-accent hover:bg-accent-soft hover:text-accent-deep",
  danger: "border-transparent bg-danger text-ink-inverse hover:bg-danger/90",
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

export function Button({ variant, size, className, type = "button", ...rest }: ButtonProps) {
  return <button type={type} className={buttonClasses({ variant, size, className })} {...rest} />;
}
