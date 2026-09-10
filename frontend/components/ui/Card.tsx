import type { ComponentPropsWithoutRef, ElementType, ReactNode } from "react";
import { cn } from "@/lib/ui/cn";

export interface CardProps extends Omit<ComponentPropsWithoutRef<"div">, "children"> {
  /** Element to render — `article`/`li`/`section` etc. where the surrounding markup needs it. */
  as?: ElementType;
  className?: string;
  children: ReactNode;
}

/**
 * Surface card: `--surface` on `--paper`, hairline `--border`, `--radius-card`
 * (4px) and `--shadow-card` (guidelines/radius-shadow.html). Padding matches
 * the package cards in direction-a.dc.html (32px / 28px).
 */
export function Card({ as: Tag = "div", className, children, ...rest }: CardProps) {
  return (
    <Tag
      {...rest}
      className={cn(
        "rounded-card border border-border bg-surface px-6 py-8 shadow-card sm:px-7",
        className,
      )}
    >
      {children}
    </Tag>
  );
}
