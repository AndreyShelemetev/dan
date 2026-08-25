import { cn } from "@/lib/ui/cn";
import { LOCATION_QUALITY } from "@/lib/api/burialSites";

/**
 * How findable this grave is, in the client's language.
 *
 * The wording matters more than the styling here. "Insufficient" is not a
 * complaint about the client's data entry — it is the service saying it will
 * not send someone to wander a cemetery and bill for it, and offering the
 * inspection instead. The tone stays calm and the next step is always named.
 */
const VARIANTS: Record<string, { label: string; hint: string; className: string }> = {
  [LOCATION_QUALITY.sufficient]: {
    label: "Место можно найти",
    hint: "Данных достаточно, чтобы направить исполнителя.",
    className: "border-success bg-success-soft text-success",
  },
  [LOCATION_QUALITY.insufficient]: {
    label: "Нужен осмотр",
    hint: "По описанию место найти сложно. Мы предложим отдельный выезд на поиск, прежде чем браться за уход.",
    className: "border-warning bg-warning-soft text-warning",
  },
  [LOCATION_QUALITY.unverified]: {
    label: "Проверяем данные",
    hint: "Мы уточним описание перед первым визитом.",
    className: "border-border-strong bg-surface text-ink-2",
  },
};

export function LocationQualityBadge({ quality, className }: { quality: string; className?: string }) {
  const variant = VARIANTS[quality] ?? VARIANTS[LOCATION_QUALITY.unverified];

  return (
    <span
      className={cn(
        "inline-flex items-center rounded-pill border px-3 py-1 text-xs font-semibold",
        variant.className,
        className,
      )}
    >
      {variant.label}
    </span>
  );
}

export function LocationQualityHint({ quality }: { quality: string }) {
  const variant = VARIANTS[quality] ?? VARIANTS[LOCATION_QUALITY.unverified];
  return <p className="text-sm text-ink-2">{variant.hint}</p>;
}
