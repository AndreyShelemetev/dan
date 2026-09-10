import type { Metadata } from "next";
import Link from "next/link";
import { Card } from "@/components/ui/Card";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { PageHeader } from "@/components/site/PageHeader";
import { COMPANY } from "@/lib/legal/company";

export const metadata: Metadata = {
  title: "Вопросы и ответы · Память рядом",
  description:
    "Что делать, если не знаете номер участка, как устроена оплата и гарантия, кто выезжает и что будет, если работу не сделали.",
};

interface Question {
  q: string;
  a: React.ReactNode;
}

const GROUPS: { title: string; questions: Question[] }[] = [
  {
    title: "Заявка и место",
    questions: [
      {
        q: "Я не знаю номер участка. Что делать?",
        a: (
          <>
            Опишите то, что помните: кладбище, направление от входа, соседние памятники, цвет
            ограды, любые приметы. Этого чаще всего хватает. Если нет — возьмите «Осмотр и
            цифровой паспорт»: исполнитель найдёт место по приметам и опишет его так, что
            следующие заказы уже не потребуют поисков.
          </>
        ),
      },
      {
        q: "А если место так и не найдут?",
        a: (
          <>
            Мы проверяем описание до оплаты. Если по нему участок опознать нельзя, вам скажут об
            этом сразу — заявка не уйдёт исполнителю, и деньги не спишутся. Если поиск не удался
            уже на месте, вы платите только за выезд на осмотр, если он был согласован.
          </>
        ),
      },
      {
        q: "Вы работаете в моём городе?",
        a: (
          <>
            Мы работаем в городах России и расширяем список по мере того, как находим проверенных
            исполнителей. Оставьте заявку: если сейчас в вашем городе никого нет, мы прямо скажем
            об этом, а не будем держать заказ «в обработке».
          </>
        ),
      },
    ],
  },
  {
    title: "Деньги",
    questions: [
      {
        q: "Когда я плачу?",
        a: (
          <>
            После того как посмотрите смету и согласитесь с ней. До этого момента никаких списаний
            не происходит и никто никуда не выезжает.
          </>
        ),
      },
      {
        q: "Может ли сумма вырасти после оплаты?",
        a: (
          <>
            Без вашего согласия — нет. Если на месте выяснится, что нужны дополнительные работы,
            исполнитель остановится, а мы пришлём новую смету. Пока вы её не примете, ничего
            сверх согласованного не делается и не списывается.
          </>
        ),
      },
      {
        q: "А если работу не сделали?",
        a: (
          <>
            Тогда вы за неё не платите. В отчёте видно каждый пункт чек-листа и причину, если
            что-то выполнить не удалось. Деньги за невыполненное возвращаются.
          </>
        ),
      },
    ],
  },
  {
    title: "Исполнитель и результат",
    questions: [
      {
        q: "Кто приедет на кладбище?",
        a: (
          <>
            Исполнитель, которого назначили мы. Вы его не выбираете и не связываетесь с ним
            напрямую: договор у вас с компанией, и за качество отвечаем мы. Это принципиально
            отличается от объявления в интернете, где вы остаётесь с исполнителем один на один.
          </>
        ),
      },
      {
        q: "Как я пойму, что работа сделана?",
        a: (
          <>
            По фотографиям «до» и «после», снятым одним ракурсом, и по чек-листу, где отмечен
            каждый пункт. Отчёт сначала смотрит наш контролёр и возвращает исполнителю, если
            что-то не сделано, — до вас доходит только проверенная работа.
          </>
        ),
      },
      {
        q: "Мне не понравился результат.",
        a: (
          <>
            Нажмите «Есть замечания» в отчёте и опишите, что не так. Мы разберёмся: переделаем
            работу или вернём деньги. Гарантийный срок указан в каждом пакете и начинается с
            момента, когда вы приняли работу.
          </>
        ),
      },
      {
        q: "Можно заказывать регулярно?",
        a: (
          <>
            Да — для этого есть сезонное обслуживание с несколькими визитами. Регулярные подписки
            с автоматическим списанием мы ещё не запустили; пока каждый визит оформляется
            отдельным заказом.
          </>
        ),
      },
    ],
  },
  {
    title: "Данные",
    questions: [
      {
        q: "Кто видит фотографии моего участка?",
        a: (
          <>
            Вы, те, кого вы пригласили в своё место памяти, и сотрудники, которым это нужно для
            работы: диспетчер, контролёр качества, поддержка. Файлы лежат в закрытом хранилище и
            открываются по временным ссылкам — публичного адреса у них нет, и в поиск они не
            попадают.
          </>
        ),
      },
      {
        q: "Как удалить свои данные?",
        a: (
          <>
            Напишите на{" "}
            <a href={`mailto:${COMPANY.privacyEmail}`} className="text-accent hover:text-accent-deep">
              {COMPANY.privacyEmail}
            </a>
            . Подробности — в{" "}
            <Link href="/legal/privacy" className="text-accent hover:text-accent-deep">
              политике обработки персональных данных
            </Link>
            .
          </>
        ),
      },
    ],
  },
];

export default function FaqPage() {
  return (
    <>
      <PageHeader
        eyebrow="Вопросы"
        title="Что спрашивают чаще всего"
        lead={
          <>
            Если ответа здесь нет, напишите нам — мы отвечаем людям, а не шаблонами.
          </>
        }
      />

      {GROUPS.map((group) => (
        <section key={group.title} aria-labelledby={slug(group.title)} className="flex flex-col gap-4">
          <h2 id={slug(group.title)} className="font-display text-2xl font-normal text-ink-1">
            {group.title}
          </h2>
          <div className="flex flex-col gap-3">
            {group.questions.map((item) => (
              <Card key={item.q} as="article" className="flex flex-col gap-2">
                <h3 className="font-display text-lg font-normal text-ink-1">{item.q}</h3>
                <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">{item.a}</p>
              </Card>
            ))}
          </div>
        </section>
      ))}

      <Card className="flex flex-col gap-4">
        <h2 className="font-display text-xl font-normal text-ink-1">Не нашли ответа</h2>
        <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
          Напишите на{" "}
          <a href={`mailto:${COMPANY.email}`} className="text-accent hover:text-accent-deep">
            {COMPANY.email}
          </a>{" "}
          или позвоните по телефону{" "}
          <a href={`tel:${COMPANY.phone.replace(/[^+\d]/g, "")}`} className="text-accent hover:text-accent-deep">
            {COMPANY.phone}
          </a>
          .
        </p>
        <div className="flex flex-wrap gap-3">
          <ButtonLink href="/login">Оставить заявку</ButtonLink>
          <ButtonLink href="/trust" variant="secondary">
            Доверие и гарантии
          </ButtonLink>
        </div>
      </Card>
    </>
  );
}

/** Latin ids from Russian headings — an `aria-labelledby` target has to be a valid id, and a
 *  transliteration nobody reads is safer than trusting the encoder. */
function slug(title: string): string {
  const index = GROUPS.findIndex((g) => g.title === title);
  return `faq-group-${index}`;
}
