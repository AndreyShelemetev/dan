import { SectionEyebrow } from "@/components/ui/SectionEyebrow";
import { StatusStepper, type StatusStep } from "@/components/ui/StatusStepper";

/**
 * «Статус вашего заказа» — the public explanation of the order lifecycle,
 * per the stepper in direction-a.dc.html.
 *
 * Labels are the five step labels from the mockup plus its terminal «Завершено»
 * node, and match the public status vocabulary in guidelines/statuses.html.
 * States mirror the mockup's illustration: two steps done, «Работа
 * выполняется» current, the rest ahead. Wire this to a real order once the
 * orders API exists — the component takes its steps as a prop for exactly that.
 */
const DEMO_STEPS: readonly StatusStep[] = [
  { label: "Проверяем данные", state: "done" },
  { label: "Смета готова", state: "done" },
  { label: "Работа выполняется", state: "current" },
  { label: "Проверяем качество", state: "upcoming" },
  { label: "Отчёт готов", state: "upcoming" },
  { label: "Завершено", state: "upcoming" },
];

export function OrderStatusSection() {
  return (
    <section
      id="how-it-works"
      aria-labelledby="how-it-works-heading"
      className="scroll-mt-6 border-t border-border bg-surface"
    >
      <div className="mx-auto w-full max-w-content px-6 py-16 lg:px-14">
        <SectionEyebrow as="h2" tone="accent" id="how-it-works-heading">
          Статус вашего заказа
        </SectionEyebrow>

        <div className="mt-8">
          <StatusStepper steps={DEMO_STEPS} ariaLabel="Этапы работы по заказу" />
        </div>
      </div>
    </section>
  );
}
