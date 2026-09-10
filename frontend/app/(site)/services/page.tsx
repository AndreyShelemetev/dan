import type { Metadata } from "next";
import Link from "next/link";
import { Card } from "@/components/ui/Card";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { PageHeader } from "@/components/site/PageHeader";
import { formatRub, listServicePackages } from "@/lib/api/catalog";

export const metadata: Metadata = {
  title: "Услуги · Память рядом",
  description:
    "Уход за захоронением в городах России: осмотр, разовая уборка, сезонное обслуживание, цветы к дате. Цены и состав работ.",
};

export const revalidate = 300;

export default async function ServicesPage() {
  // A catalogue that cannot be read is not a reason to show a broken page — the rest of the copy
  // still answers what the service is.
  const packages = await listServicePackages().catch(() => []);

  return (
    <>
      <PageHeader
        eyebrow="Услуги"
        title="Что мы делаем на кладбище"
        lead={
          <>
            Регулярный и разовый уход за захоронением, когда приехать самому не получается.
            Цена в каждом пакете — стартовая: точную сумму мы называем сметой, после того как
            поймём, что именно нужно на конкретном участке.
          </>
        }
      />

      {packages.length > 0 ? (
        <section aria-labelledby="packages" className="flex flex-col gap-4">
          <h2 id="packages" className="font-display text-2xl font-normal text-ink-1">
            Пакеты
          </h2>
          <ul className="grid list-none gap-4 p-0 md:grid-cols-2">
            {packages.map((pkg) => (
              <li key={pkg.code}>
                <Card as="article" className="flex h-full flex-col gap-3">
                  <div>
                    <h3 className="font-display text-xl font-normal text-ink-1">{pkg.title}</h3>
                    <p className="mt-1 font-display text-lg tabular-nums text-accent-deep">
                      от {formatRub(pkg.priceFromRub)} ₽
                    </p>
                  </div>
                  <p className="text-base leading-relaxed text-ink-2">{pkg.summary}</p>
                  {pkg.includes && pkg.includes.length > 0 ? (
                    <ul className="flex list-none flex-col gap-1 p-0">
                      {pkg.includes.map((line) => (
                        <li key={line} className="flex gap-2 text-sm text-ink-2">
                          <span aria-hidden="true" className="text-accent-deep">
                            ·
                          </span>
                          {line}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                  <p className="mt-auto text-sm text-ink-2">
                    Гарантия {pkg.warrantyDays} дн.
                    {pkg.visitsLabel ? ` · ${pkg.visitsLabel}` : ""}
                  </p>
                </Card>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section aria-labelledby="pricing" className="flex flex-col gap-4">
        <h2 id="pricing" className="font-display text-2xl font-normal text-ink-1">
          Почему цена «от»
        </h2>
        <Card className="flex flex-col gap-3">
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Два участка одного размера могут отличаться в работе вдвое. На одном — подстричь траву,
            на другом — вывезти мешок листьев, отмыть плиту от подтёков и поправить осевшую
            плитку. Назвать точную цену, не увидев места, честно нельзя.
          </p>
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Поэтому мы называем стартовую цену пакета, а потом присылаем смету построчно: работы,
            материалы, доставка. Вы видите, за что платите, и решаете до оплаты, а не после.
          </p>
        </Card>
      </section>

      <section aria-labelledby="not-included" className="flex flex-col gap-4">
        <h2 id="not-included" className="font-display text-2xl font-normal text-ink-1">
          Чего в пакетах нет
        </h2>
        <Card className="flex flex-col gap-3">
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Реставрации и установки памятников, гравировки, работ с фундаментом. Это отдельная
            квалификация и отдельная ответственность — браться за них между делом было бы
            нечестно.
          </p>
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Если нужно что-то за пределами пакета, напишите в заявке: если мы сможем это
            организовать, включим отдельной строкой в смету. Если нет — скажем прямо.
          </p>
        </Card>
      </section>

      <section aria-labelledby="unknown" className="flex flex-col gap-4">
        <h2 id="unknown" className="font-display text-2xl font-normal text-ink-1">
          Если вы не знаете, где участок
        </h2>
        <Card className="flex flex-col gap-3">
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Так бывает чаще, чем кажется: человек помнит кладбище и примерное направление, но не
            номер участка. Возьмите «Осмотр и цифровой паспорт» — исполнитель найдёт место по
            приметам, снимет его со всех сторон и запишет ориентиры. Дальше любой следующий заказ
            уже опирается на это описание.
          </p>
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Если по вашему описанию найти участок нельзя, мы скажем об этом до оплаты, а не после
            выезда.
          </p>
        </Card>
      </section>

      <div className="flex flex-wrap gap-3">
        <ButtonLink href="/login">Оставить заявку</ButtonLink>
        <ButtonLink href="/how-it-works" variant="secondary">
          Как это работает
        </ButtonLink>
      </div>

      <p className="text-sm text-ink-2">
        Остались вопросы —{" "}
        <Link href="/faq" className="text-accent hover:text-accent-deep">
          посмотрите ответы
        </Link>
        .
      </p>
    </>
  );
}
