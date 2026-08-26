import { Card } from "@/components/ui/Card";
import { formatRub, type ServicePackage } from "@/lib/api/catalog";

/**
 * The care packages, read from the catalogue rather than hardcoded.
 *
 * Money follows guidelines/formatting.html: a thin space (U+2009) between thousands and a
 * non-breaking space (U+00A0) before ₽, so a price never wraps away from its unit. These are
 * public "from" prices only — no executor payout or margin data appears on any client-facing
 * surface.
 *
 * Limits are rendered on the card, not tucked behind a tooltip: the product promise is that a
 * client learns what is *not* covered before paying, and the business concept names hidden
 * extras as the single biggest fear this service has to answer.
 */
function price(value: number): string {
  return `от ${formatRub(value).replace(/\s/g, " ")} ₽`;
}

export function PackagesSection({ packages }: { packages: ServicePackage[] }) {
  if (packages.length === 0) {
    // The catalogue is reference data and should never be empty, but a failed fetch must not
    // leave a headless section on the page.
    return null;
  }

  return (
    <section
      id="services"
      aria-labelledby="services-heading"
      className="mx-auto w-full max-w-content scroll-mt-6 px-6 pb-16 lg:px-14"
    >
      <h2 id="services-heading" className="sr-only">
        Услуги
      </h2>

      <ul className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
        {packages.map((pkg) => (
          <Card key={pkg.code} as="li" className="flex flex-col">
            <h3 className="font-display text-lg font-normal text-ink-1">{pkg.title}</h3>
            <p className="mb-5 mt-2 text-sm leading-relaxed text-ink-2">{pkg.summary}</p>

            {pkg.limits.length > 0 ? (
              <ul className="mb-5 flex list-none flex-col gap-1 p-0 text-xs text-ink-2">
                {pkg.limits.map((limit) => (
                  <li key={limit} className="flex gap-2">
                    {/* Decorative: the meaning is in the text beside it, and a screen reader
                        reading "bullet" before every limit adds nothing. */}
                    <span aria-hidden="true">·</span>
                    <span>{limit}</span>
                  </li>
                ))}
              </ul>
            ) : null}

            {/*
              flex-wrap + nowrap on both cells: when the card is too narrow for term and price
              side by side, the price drops to its own line instead of breaking «от 8 400 ₽».
            */}
            <div className="mt-auto flex flex-wrap items-baseline justify-between gap-x-4 gap-y-2 border-t border-border pt-5">
              <span className="whitespace-nowrap text-sm text-ink-2">{pkg.visitsLabel}</span>
              <span className="whitespace-nowrap font-display text-lg text-ink-1">
                {price(pkg.priceFromRub)}
              </span>
            </div>
          </Card>
        ))}
      </ul>
    </section>
  );
}
