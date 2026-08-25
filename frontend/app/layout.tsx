import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import { Manrope, Spectral } from "next/font/google";
import "./globals.css";
import { AuthProvider } from "@/components/auth/AuthProvider";
import { CookieBanner } from "@/components/legal/CookieBanner";
import { getServerUser } from "@/lib/auth/server";

/**
 * Design system fonts (direction A «Тихий сад»):
 * - Spectral — display / headings, prices, package names.
 * - Manrope  — UI and body copy.
 *
 * Loaded through next/font so they are self-hosted at build time: no runtime
 * request to fonts.googleapis.com, no layout shift, nothing to allow in CSP.
 * Both are exposed as CSS variables that tailwind.config.js and globals.css
 * reference (`--font-spectral` / `--font-manrope`).
 */
const spectral = Spectral({
  subsets: ["latin", "cyrillic"],
  weight: ["300", "400", "500"],
  style: ["normal", "italic"],
  display: "swap",
  variable: "--font-spectral",
  fallback: ["Georgia", "Times New Roman", "serif"],
});

// Manrope is a variable font on Google Fonts, so the whole 200–800 axis ships
// in one file — the design's 400/500/600/700 are all covered without listing
// weights (listing them would emit redundant static cuts).
const manrope = Manrope({
  subsets: ["latin", "cyrillic"],
  display: "swap",
  variable: "--font-manrope",
  fallback: ["Segoe UI", "Arial", "sans-serif"],
});

export const metadata: Metadata = {
  title: "Память рядом",
  description:
    "Уход за захоронениями в городах России: проверенный исполнитель, фотоотчёт «до и после» и контроль качества по каждому заказу.",
};

export const viewport: Viewport = {
  // Literal rather than a token reference: <meta name="theme-color"> is read by
  // the browser chrome before any CSS loads, so it cannot use var(--paper).
  // Keep in sync with --paper in app/globals.css.
  themeColor: "#F5F3EE",
};

export default async function RootLayout({ children }: { children: ReactNode }) {
  // Fetched server-side so AuthProvider hydrates with the current user on
  // first paint, instead of every client component waiting on a fetch.
  const initialUser = await getServerUser();

  return (
    <html lang="ru" className={`${spectral.variable} ${manrope.variable}`}>
      <body className="flex min-h-screen flex-col bg-paper font-sans text-base text-ink-1 antialiased">
        <a
          href="#main"
          className="sr-only rounded-card focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:bg-surface-raised focus:px-4 focus:py-3 focus:text-sm focus:font-semibold focus:text-ink-1 focus:shadow-raised"
        >
          Перейти к содержимому
        </a>
        <AuthProvider initialUser={initialUser}>{children}</AuthProvider>
        <CookieBanner />
      </body>
    </html>
  );
}
