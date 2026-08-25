/**
 * Public site footer, per `ui_kits/public-site/index.html`.
 *
 * «Оферта» / «Конфиденциальность» / «Поддержка» are rendered as plain text,
 * not links: the design points them at `#` and the corresponding routes do not
 * exist yet, and a link that 404s is worse than a label. Swap each to a
 * `<Link>` as those pages land.
 */
const LEGAL_ITEMS = ["Оферта", "Конфиденциальность", "Поддержка"] as const;

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
          {LEGAL_ITEMS.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      </div>
    </footer>
  );
}
