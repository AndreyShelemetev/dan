import type { ReactNode } from "react";
import Link from "next/link";
import { SiteHeader } from "@/components/site/SiteHeader";
import { SiteFooter } from "@/components/site/SiteFooter";

/**
 * Shell for a legal page.
 *
 * The version and effective date sit under the title rather than buried at the foot: what a
 * person accepted is identified by its version, so the version has to be visible where they
 * read it. `measure` caps the line length — these are the longest passages of prose in the
 * product and the only ones anyone reads end to end.
 */
export function LegalArticle({
  title,
  version,
  effectiveAt,
  children,
}: {
  title: string;
  version: string;
  effectiveAt: string;
  children: ReactNode;
}) {
  return (
    <>
      <SiteHeader />
      <main id="main" className="flex-1">
        <div className="mx-auto w-full max-w-hero px-6 py-12 lg:px-14">
          <p className="text-sm">
            <Link href="/" className="inline-flex min-h-hit items-center text-ink-2 hover:text-accent-deep">
              ← На главную
            </Link>
          </p>

          <h1 className="mt-4 text-balance font-display text-3xl font-normal leading-tight text-ink-1">
            {title}
          </h1>
          <p className="mt-3 text-sm text-ink-2">
            Версия {version} · действует с {effectiveAt}
          </p>

          <div className="legal-prose mt-10">{children}</div>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}
