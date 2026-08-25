import Link from "next/link";
import { notFound } from "next/navigation";
import { Card } from "@/components/ui/Card";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";
import { MembersPanel } from "@/components/cabinet/MembersPanel";
import { LocationQualityBadge, LocationQualityHint } from "@/components/cabinet/LocationQualityNote";
import { ApiError } from "@/lib/api/client";
import { canManage, getBurialSite, listMembers } from "@/lib/api/burialSites";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

function Detail({ label, value }: { label: string; value: string | null }) {
  if (!value) return null;
  return (
    <div>
      <dt className="text-sm text-ink-2">{label}</dt>
      <dd className="mt-0.5 whitespace-pre-line text-base text-ink-1">{value}</dd>
    </div>
  );
}

export default async function BurialSitePage({ params }: { params: { id: string } }) {
  const id = Number(params.id);
  if (!Number.isFinite(id)) {
    notFound();
  }

  const cookieHeader = getSessionCookieHeader();

  let site;
  try {
    site = await getBurialSite(id, cookieHeader);
  } catch (err) {
    // The API answers 404 both for "no such record" and for "not yours" — telling
    // the two apart would confirm that a given person is buried somewhere we serve.
    if (err instanceof ApiError && (err.status === 404 || err.isUnauthorized)) {
      notFound();
    }
    throw err;
  }

  const members = await listMembers(id, cookieHeader).catch(() => []);
  const manages = canManage(site.permission);

  return (
    <div className="flex flex-col gap-8">
      <p className="text-sm">
        <Link href="/cabinet" className="text-ink-2 hover:text-accent-deep">
          ← Мои места памяти
        </Link>
      </p>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <SectionEyebrow>{site.cemeteryName ?? "Место памяти"}</SectionEyebrow>
          <h1 className="mt-3 font-display text-3xl font-normal text-ink-1">
            {site.deceasedFullName}
          </h1>
          {site.birthDateText || site.deathDateText ? (
            <p className="mt-2 text-base text-ink-2">
              {[site.birthDateText, site.deathDateText].filter(Boolean).join(" — ")}
            </p>
          ) : null}
        </div>
        <LocationQualityBadge quality={site.locationQuality} />
      </div>

      <Card className="flex flex-col gap-5">
        <LocationQualityHint quality={site.locationQuality} />
        <dl className="grid gap-5 sm:grid-cols-2">
          <Detail label="Кладбище" value={site.cemeteryName} />
          <Detail label="Участок, ряд" value={site.plotSection} />
          <Detail label="Ориентиры" value={site.landmarks} />
          <Detail label="Особые указания" value={site.notes} />
          {site.geoLat !== null && site.geoLng !== null ? (
            <Detail label="Координата" value={`${site.geoLat}, ${site.geoLng}`} />
          ) : null}
        </dl>
      </Card>

      <Card className="flex flex-col gap-3">
        <h2 className="font-display text-xl font-normal text-ink-1">Фотографии</h2>
        <p className="text-sm text-ink-2">
          {site.photoCount > 0
            ? `Загружено фотографий: ${site.photoCount}.`
            : "Пока фотографий нет. Загрузка появится в следующем обновлении — снимки будут видны только вам и приглашённым родственникам."}
        </p>
      </Card>

      <MembersPanel
        siteId={site.id}
        initialMembers={members}
        canManage={manages}
        isOwner={site.isOwner}
      />
    </div>
  );
}
