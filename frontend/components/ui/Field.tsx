import type { InputHTMLAttributes } from "react";
import { cn } from "@/lib/ui/cn";

export interface FieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id" | "className"> {
  id: string;
  label: string;
  hint?: string;
  /** Rendered under the input and wired to the control via aria-describedby. */
  error?: string | null;
  className?: string;
}

/**
 * Labelled text input, per `three wariants design/components/forms/Input.jsx`:
 * raised surface, 4px radius, strong hairline border, soft focus ring on top
 * of the global accent focus outline.
 *
 * The 15px font size is deliberate — anything under 16px makes iOS Safari zoom
 * on focus, and 15px is the design's body size, so the input is given the
 * 44px minimum height instead of a larger type size.
 */
export function Field({ id, label, hint, error, className, ...rest }: FieldProps) {
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = [error ? errorId : null, hint ? hintId : null].filter(Boolean).join(" ");

  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <label htmlFor={id} className="text-sm font-semibold text-ink-1">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy.length > 0 ? describedBy : undefined}
        className={cn(
          "min-h-hit rounded-input border bg-surface-raised px-3.5 py-3 text-base text-ink-1 outline-none",
          "placeholder:text-ink-2 focus-visible:shadow-focus",
          "transition-colors duration-ds ease-ds",
          error ? "border-danger" : "border-border-strong",
        )}
        {...rest}
      />
      {error ? (
        <p id={errorId} role="alert" className="text-xs text-danger">
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className="text-xs text-ink-2">
          {hint}
        </p>
      ) : null}
    </div>
  );
}
