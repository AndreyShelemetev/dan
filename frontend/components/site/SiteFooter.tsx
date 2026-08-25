import Link from "next/link";

/**
 * Public site footer, per `ui_kits/public-site/index.html`.
 *
 * Real links now that the pages exist. «Поддержка» stays an address rather than a route:
 * there is no support page to send anyone to, and a mailto is the honest destination.
 */
const LEGAL_LINKS = [
  { label: "Конфиденциальность", href: "/legal/privacy/" },
  { label: "Согласие на обработку", href: "/legal/consent/" },
  { label: "Cookie", href: "/legal/cookies/" },
] as const;

export function SiteFooter() {
  return (
    <footer
      id="support"
      className="mt-auto border-t border-border scroll-mt-6"
      aria-label="Служебная информация"
    >
      <div className="mx-auto flex w-full max-w-content flex-col gap-4 px-6 py-7 text-sm text-ink-2 sm:flex-row sm:items-center sm:justify-between lg:px-14">
        <p>© 2026 Память рядом</p>
        <ul className="flex flex-wrap gap-x-6 gap-y-2">
          {LEGAL_LINKS.map((item) => (
            <li key={item.href}>
              <Link
                href={item.href}
                className="inline-flex min-h-hit items-center text-ink-2 no-underline hover:text-accent-deep"
              >
                {item.label}
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </footer>
  );
}
