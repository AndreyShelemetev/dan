import { ButtonLink } from "@/components/ui/ButtonLink";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";

/**
 * Hero, per direction-a.dc.html. Copy is verbatim from the design — it was
 * chosen deliberately and is not placeholder text.
 *
 * Both CTAs currently route to /login: creating an order requires a session,
 * and the order-intake routes are not built yet.
 */
export function Hero() {
  return (
    <section className="mx-auto w-full max-w-hero px-6 pb-12 pt-14 text-center md:pb-16 md:pt-22 lg:px-14">
      <SectionEyebrow className="text-balance tracking-caps-wide">
        дистанционный уход · проверенные исполнители · подтверждённый результат
      </SectionEyebrow>

      <h1 className="mt-7 text-balance font-display text-2xl font-normal leading-tight text-ink-1 sm:text-3xl lg:text-4xl">
        Забота о месте памяти, когда вы не можете быть рядом
      </h1>

      <p className="mx-auto mt-6 max-w-measure text-md leading-relaxed text-ink-2">
        Уход за захоронениями в Санкт-Петербурге: проверенный исполнитель, фотоотчёт «до и после» и
        контроль качества по каждому заказу.
      </p>

      <div className="mt-10 flex flex-col items-stretch justify-center gap-4 sm:flex-row sm:items-center">
        <ButtonLink href="/login" size="lg">
          Создать заявку
        </ButtonLink>
        <ButtonLink href="/login" variant="secondary" size="lg">
          Заказать осмотр
        </ButtonLink>
      </div>
    </section>
  );
}
