import type { Metadata } from "next";
import Link from "next/link";
import { Card } from "@/components/ui/Card";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { PageHeader } from "@/components/site/PageHeader";
import { COMPANY } from "@/lib/legal/company";

export const metadata: Metadata = {
  title: "Доверие и гарантии · Память рядом",
  description:
    "Кто отвечает за работу, что происходит при отказе, как устроен гарантийный срок и что мы делаем с вашими данными.",
};

const PROMISES = [
  {
    title: "Отвечаем мы, а не исполнитель",
    body:
      "Договор у вас с компанией. Исполнителя назначаем и проверяем мы, его контакты вам не " +
      "нужны, и разбираться с ним в случае претензии вам не придётся. Претензия адресуется нам.",
  },
  {
    title: "Отчёт проверяют до того, как его увидите вы",
    body:
      "Между исполнителем и вами стоит контролёр. Он сверяет фотографии «до» и «после» с " +
      "чек-листом и возвращает работу, если что-то не сделано. Смысл в том, чтобы вы не тратили " +
      "силы на поиск недочётов в момент, когда вам меньше всего до этого.",
  },
  {
    title: "Доплат без вашего согласия не бывает",
    body:
      "Смета согласуется построчно и фиксируется. Если на месте выяснится, что нужны " +
      "дополнительные работы, исполнитель остановится и мы спросим вас. Новая смета — новое " +
      "согласие; старое при этом перестаёт действовать.",
  },
  {
    title: "Не сделано — не оплачено",
    body:
      "Если работу выполнить не удалось, вы увидите почему, и деньги вернутся. Мы не считаем " +
      "выезд сам по себе результатом, за который вы должны заплатить.",
  },
];

const FACTS = [
  { label: "Гарантийный срок", value: "от 14 дней", note: "зависит от пакета, указан в каждом" },
  { label: "Фотографии", value: "«до» и «после»", note: "одним ракурсом, чтобы их можно было сравнить" },
  { label: "Оплата", value: "после согласования сметы", note: "не раньше" },
  { label: "География", value: "города России", note: "по мере появления проверенных исполнителей" },
];

export default function TrustPage() {
  return (
    <>
      <PageHeader
        eyebrow="Доверие и гарантии"
        title="Чем мы отвечаем за результат"
        lead={
          <>
            Вы отдаёте деньги незнакомым людям за работу, которую не можете проверить лично, на
            могиле близкого человека. Это требует не обещаний, а понятных правил. Вот они.
          </>
        }
      />

      <section aria-labelledby="promises" className="flex flex-col gap-4">
        <h2 id="promises" className="font-display text-2xl font-normal text-ink-1">
          Что мы обещаем
        </h2>
        <ul className="grid list-none gap-4 p-0 md:grid-cols-2">
          {PROMISES.map((promise) => (
            <li key={promise.title}>
              <Card as="article" className="flex h-full flex-col gap-2">
                <h3 className="font-display text-lg font-normal text-ink-1">{promise.title}</h3>
                <p className="text-base leading-relaxed text-ink-2">{promise.body}</p>
              </Card>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="facts" className="flex flex-col gap-4">
        <h2 id="facts" className="font-display text-2xl font-normal text-ink-1">
          Коротко
        </h2>
        <Card>
          <dl className="grid gap-6 sm:grid-cols-2">
            {FACTS.map((fact) => (
              <div key={fact.label}>
                <dt className="text-sm text-ink-2">{fact.label}</dt>
                <dd className="mt-1 font-display text-lg text-ink-1">{fact.value}</dd>
                <dd className="mt-0.5 text-sm text-ink-2">{fact.note}</dd>
              </div>
            ))}
          </dl>
        </Card>
      </section>

      <section aria-labelledby="honest" className="flex flex-col gap-4">
        <h2 id="honest" className="font-display text-2xl font-normal text-ink-1">
          Чего мы не делаем
        </h2>
        <Card className="flex flex-col gap-3">
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Мы не реставрируем памятники и не занимаемся установкой — это отдельные работы, для
            которых нужна другая квалификация. Не оформляем документы на захоронение и не решаем
            вопросы с администрацией кладбища от вашего имени.
          </p>
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            И мы не обещаем «как было при жизни». Мы обещаем убранный участок, чистый памятник,
            свежие цветы — и фотографии, по которым это видно.
          </p>
        </Card>
      </section>

      <section aria-labelledby="data" className="flex flex-col gap-4">
        <h2 id="data" className="font-display text-2xl font-normal text-ink-1">
          Ваши данные
        </h2>
        <Card className="flex flex-col gap-3">
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Мы храним имя покойного, расположение участка, ваши контакты и фотографии. Это
            чувствительные сведения, и обращаемся мы с ними соответственно: фотографии лежат в
            закрытом хранилище и доступны по временным ссылкам, публичного адреса у них нет.
          </p>
          <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
            Что именно мы собираем и зачем — в{" "}
            <Link href="/legal/privacy" className="text-accent hover:text-accent-deep">
              политике обработки персональных данных
            </Link>
            .
          </p>
        </Card>
      </section>

      <section aria-labelledby="who" className="flex flex-col gap-4">
        <h2 id="who" className="font-display text-2xl font-normal text-ink-1">
          Кто оказывает услугу
        </h2>
        <Card>
          <dl className="flex flex-col gap-3">
            <div>
              <dt className="text-sm text-ink-2">Организация</dt>
              <dd className="mt-0.5 text-base text-ink-1">{COMPANY.name}</dd>
            </div>
            <div>
              <dt className="text-sm text-ink-2">ИНН</dt>
              <dd className="mt-0.5 text-base tabular-nums text-ink-1">{COMPANY.inn}</dd>
            </div>
            <div>
              <dt className="text-sm text-ink-2">ОГРН</dt>
              <dd className="mt-0.5 text-base tabular-nums text-ink-1">{COMPANY.ogrn}</dd>
            </div>
          </dl>
        </Card>
      </section>

      <div className="flex flex-wrap gap-3">
        <ButtonLink href="/services">Посмотреть услуги</ButtonLink>
        <ButtonLink href="/how-it-works" variant="secondary">
          Как это работает
        </ButtonLink>
      </div>
    </>
  );
}
