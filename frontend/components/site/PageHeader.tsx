import type { ReactNode } from "react";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";

/**
 * The top of a content page.
 *
 * The lead paragraph is capped at 60 characters per line. Someone reading about what happens to
 * their family's grave is reading carefully, not scanning, and a line that runs the full width of
 * a desktop screen loses its place on every return sweep.
 */
export function PageHeader({
  eyebrow,
  title,
  lead,
}: {
  eyebrow: string;
  title: string;
  lead: ReactNode;
}) {
  return (
    <header className="flex flex-col gap-4">
      <SectionEyebrow>{eyebrow}</SectionEyebrow>
      <h1 className="max-w-[18ch] font-display text-3xl font-normal leading-tight text-ink-1 md:text-4xl">
        {title}
      </h1>
      <p className="max-w-[60ch] text-lg leading-relaxed text-ink-2">{lead}</p>
    </header>
  );
}
