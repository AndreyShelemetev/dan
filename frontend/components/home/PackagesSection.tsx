import { Card } from "@/components/ui/Card";

/**
 * The three care packages, per direction-a.dc.html — names, descriptions,
 * visit counts and «от …» prices are verbatim from the design.
 *
 * Money follows guidelines/formatting.html: a thin space (U+2009) between
 * thousands and a non-breaking space (U+00A0) before ₽, so a price never wraps
 * away from its unit. These are public "from" prices only — no executor payout
 * or margin data appears on any client-facing surface.
 */
const PACKAGES = [
  {
    name: "Базовый уход",
    description:
      "Уборка участка, очистка памятника, вынос мусора и фотоотчёт по обязательным ракурсам.",
    term: "1 визит",
    price: "от 4 900 ₽",
  },
  {
    name: "Сезонный уход",
    description: "Подготовка к сезону: прополка, подсыпка, мытьё, мелкий ремонт по согласованию.",
    term: "1–2 визита",
    price: "от 8 400 ₽",
  },
  {
    name: "Уход с цветами",
    description: "Базовый уход и живые цветы или композиция к памятной дате.",
    term: "1 визит",
    price: "от 6 200 ₽",
  },
] as const;

export function PackagesSection() {
  return (
    <section
      id="services"
      aria-labelledby="services-heading"
      className="mx-auto w-full max-w-content scroll-mt-6 px-6 pb-16 lg:px-14"
    >
      <h2 id="services-heading" className="sr-only">
        Услуги
      </h2>

      <ul className="grid gap-6 md:grid-cols-3">
        {PACKAGES.map((pkg) => (
          <Card key={pkg.name} as="li" className="flex flex-col">
            <h3 className="font-display text-lg font-normal text-ink-1">{pkg.name}</h3>
            <p className="mb-6 mt-2 text-sm leading-relaxed text-ink-2">{pkg.description}</p>
            {/*
              flex-wrap + nowrap on both cells: when the card is too narrow for
              term and price side by side (3 columns at ~768px), the price drops
              to its own line instead of breaking «от 8 400 ₽» across lines.
            */}
            <div className="mt-auto flex flex-wrap items-baseline justify-between gap-x-4 gap-y-2 border-t border-border pt-5">
              <span className="whitespace-nowrap text-sm text-ink-2">{pkg.term}</span>
              <span className="whitespace-nowrap font-display text-lg text-ink-1">{pkg.price}</span>
            </div>
          </Card>
        ))}
      </ul>
    </section>
  );
}
