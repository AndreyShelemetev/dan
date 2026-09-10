import type { ReactNode } from "react";

/**
 * Chrome for the public content pages.
 *
 * A route group, so the URLs stay at the root — "/trust" reads as a promise, "/site/trust" reads
 * as a filing system.
 */
export default function SiteContentLayout({ children }: { children: ReactNode }) {
  return (
    <main
      id="main"
      className="mx-auto flex w-full max-w-content flex-col gap-14 px-6 py-14 md:py-20 lg:px-14"
    >
      {children}
    </main>
  );
}
