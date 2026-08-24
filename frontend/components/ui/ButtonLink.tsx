import Link from "next/link";
import type { ComponentPropsWithoutRef } from "react";
import { buttonClasses, type ButtonStyleOptions } from "./Button";

export interface ButtonLinkProps
  extends Omit<ComponentPropsWithoutRef<typeof Link>, "className">,
    ButtonStyleOptions {}

/**
 * A navigation CTA that looks like a Button. Kept separate from `Button` so
 * links stay links (right-click, middle-click, prefetch) instead of being
 * faked with onClick handlers.
 */
export function ButtonLink({ variant, size, className, ...rest }: ButtonLinkProps) {
  return <Link className={buttonClasses({ variant, size, className })} {...rest} />;
}
