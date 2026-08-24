import type { ElementType, ReactNode } from "react";
import { cn } from "@/lib/ui/cn";

export type EyebrowTone = "muted" | "accent";

export interface SectionEyebrowProps {
  /** `h2` where the eyebrow is the section's real heading, `p` where it is not. */
  as?: ElementType;
  tone?: EyebrowTone;
  /** Set when the eyebrow is a heading referenced by `aria-labelledby`. */
  id?: string;
  className?: string;
  children: ReactNode;
}

/**
 * Uppercase letterspaced overline (guidelines/type-caps.html).
 *
 * The guideline colours these `--ink-3`, which measures 3.0:1 on `--paper` and
 * fails WCAG AA for text, so the muted tone uses `--ink-2` (5.9:1) instead —
 * still clearly secondary, but readable. The accent tone is straight from the
 * guideline (6.1:1).
 */
const TONES: Record<EyebrowTone, string> = {
  muted: "text-ink-2",
  accent: "text-accent",
};

export function SectionEyebrow({
  as: Tag = "p",
  tone = "muted",
  id,
  className,
  children,
}: SectionEyebrowProps) {
  return (
    <Tag id={id} className={cn("text-xs uppercase tracking-caps", TONES[tone], className)}>
      {children}
    </Tag>
  );
}
