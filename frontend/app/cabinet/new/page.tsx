import Link from "next/link";
import { NewBurialSiteForm } from "@/components/cabinet/NewBurialSiteForm";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";
import { listCemeteries } from "@/lib/api/burialSites";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

export const metadata = {
  title: "Новое место памяти · Память рядом",
};

export default async function NewBurialSitePage() {
  // Fetched on the server so the form renders with its cemetery list already
  // populated — a select that arrives empty and fills in a moment later is the
  // kind of small jolt this product should not have.
  const cemeteries = await listCemeteries(getSessionCookieHeader());

  return (
    <div className="mx-auto flex w-full max-w-hero flex-col gap-8">
      <div>
        <SectionEyebrow>Новое место памяти</SectionEyebrow>
        <h1 className="mt-3 font-display text-3xl font-normal text-ink-1">
          Расскажите, за каким местом ухаживать
        </h1>
        <p className="mt-3 max-w-measure text-sm text-ink-2">
          Достаточно того, что вы помните. Если данных окажется мало, мы предложим
          отдельный выезд на поиск — и не начнём работы, пока не убедимся, что нашли
          нужное место.
        </p>
      </div>

      <NewBurialSiteForm cemeteries={cemeteries} />

      <p className="text-sm">
        <Link href="/cabinet" className="text-ink-2 hover:text-accent-deep">
          ← Вернуться к списку
        </Link>
      </p>
    </div>
  );
}
