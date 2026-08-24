import { SectionEyebrow } from "@/components/ui/SectionEyebrow";

/**
 * «Почему нам доверяют» — copy verbatim from `ui_kits/public-site/index.html`.
 * The claims here are the ones the design system signed off on; do not add
 * unverified promises (see the kit's README).
 */
const TRUST_ITEMS = [
  {
    title: "Проверенные исполнители",
    body: "Каждый исполнитель проходит анкету, идентификацию и тестовое задание перед допуском.",
  },
  {
    title: "Фотоотчёт «до и после»",
    body: "Обязательные ракурсы и чек-лист по каждому визиту. Фото приватны и видны только вам.",
  },
  {
    title: "Контроль качества",
    body: "Отчёт публикуется после проверки. Если что-то не так — доработка или возврат.",
  },
] as const;

export function TrustSection() {
  return (
    <section
      id="trust"
      aria-labelledby="trust-heading"
      className="scroll-mt-6 border-t border-border"
    >
      <div className="mx-auto w-full max-w-content px-6 py-16 lg:px-14">
        <SectionEyebrow as="h2" id="trust-heading">
          Почему нам доверяют
        </SectionEyebrow>

        <ul className="mt-8 grid gap-8 md:grid-cols-3 md:gap-10">
          {TRUST_ITEMS.map((item) => (
            <li key={item.title}>
              <h3 className="font-display text-lg font-normal text-ink-1">{item.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-ink-2">{item.body}</p>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
