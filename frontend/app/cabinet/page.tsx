import Link from "next/link";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { Card } from "@/components/ui/Card";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";
import { LocationQualityBadge } from "@/components/cabinet/LocationQualityNote";
import { listBurialSites, type BurialSite } from "@/lib/api/burialSites";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

/** "1934 — 12 марта 1998", or whichever half the family actually knows. */
function lifeSpan(site: BurialSite): string | null {
  const { birthDateText: born, deathDateText: died } = site;
  if (born && died) return `${born} — ${died}`;
  if (died) return `† ${died}`;
  if (born) return `род. ${born}`;
  return null;
}

function EmptyState() {
  return (
    <Card className="text-center">
      <h2 className="mb-3 font-display text-xl font-normal text-ink-1">
        Здесь будут ваши места памяти
      </h2>
      <p className="mx-auto mb-7 max-w-measure text-sm text-ink-2">
        Добавьте захоронение — мы сохраним точное описание места, фотографии и историю
        визитов, чтобы забота не зависела от памяти одного человека.
      </p>
      <ButtonLink href="/cabinet/new">Добавить место памяти</ButtonLink>
    </Card>
  );
}

function SiteCard({ site }: { site: BurialSite }) {
  const span = lifeSpan(site);

  return (
    <Card as="li" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-xl font-normal text-ink-1">
            <Link href={`/cabinet/${site.id}`} className="text-ink-1 no-underline hover:text-accent-deep">
              {site.deceasedFullName}
            </Link>
          </h2>
          {span ? <p className="mt-1 text-sm text-ink-2">{span}</p> : null}
        </div>
        <LocationQualityBadge quality={site.locationQuality} />
      </div>

      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
        {site.cemeteryName ? (
          <div>
            <dt className="text-ink-2">Кладбище</dt>
            <dd className="text-ink-1">{site.cemeteryName}</dd>
          </div>
        ) : null}
        {site.plotSection ? (
          <div>
            <dt className="text-ink-2">Участок</dt>
            <dd className="text-ink-1">{site.plotSection}</dd>
          </div>
        ) : null}
      </dl>

      <div className="flex flex-wrap items-center gap-3 border-t border-border pt-4">
        <ButtonLink href={`/cabinet/${site.id}`} variant="secondary" size="sm">
          Открыть
        </ButtonLink>
        <ButtonLink href={`/cabinet/${site.id}`} variant="ghost" size="sm">
          Заказать уход
        </ButtonLink>
      </div>
    </Card>
  );
}

export default async function CabinetHomePage() {
  const cookieHeader = getSessionCookieHeader();
  const sites = await listBurialSites(cookieHeader);

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <SectionEyebrow>Ваш кабинет</SectionEyebrow>
          <h1 className="mt-3 font-display text-3xl font-normal text-ink-1">Мои места памяти</h1>
        </div>
        {sites.length > 0 ? <ButtonLink href="/cabinet/new">Добавить место</ButtonLink> : null}
      </div>

      {sites.length === 0 ? (
        <EmptyState />
      ) : (
        <ul className="grid list-none grid-cols-1 gap-4 p-0 lg:grid-cols-2">
          {sites.map((site) => (
            <SiteCard key={site.id} site={site} />
          ))}
        </ul>
      )}
    </div>
  );
}
