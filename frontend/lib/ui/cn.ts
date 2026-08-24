/**
 * Joins conditional class names. Deliberately tiny — no clsx/tailwind-merge
 * dependency for what is a one-liner; later classes simply win by CSS order,
 * so pass overrides last.
 */
export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(" ");
}
