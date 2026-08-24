import type { ElementType, ReactNode } from "react";
import { cn } from "@/lib/ui/cn";

export interface CardProps {
  /** Element to render — `article`/`li` etc. where the surrounding markup needs it. */
  as?: ElementType;
  className?: string;
  children: ReactNode;
}

/**
 * Surface card: `--surface` on `--paper`, hairline `--border`, `--radius-card`
 * (4px) and `--shadow-card` (guidelines/radius-shadow.html). Padding matches
 * the package cards in direction-a.dc.html (32px / 28px).
 */
export function Card({ as: Tag = "div", className, children }: CardProps) {
  return (
    <Tag
      className={cn(
        "rounded-card border border-border bg-surface px-6 py-8 shadow-card sm:px-7",
        className,
      )}
    >
      {children}
    </Tag>
  );
}
