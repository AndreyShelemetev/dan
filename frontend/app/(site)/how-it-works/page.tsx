import type { Metadata } from "next";
import Link from "next/link";
import { Card } from "@/components/ui/Card";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { PageHeader } from "@/components/site/PageHeader";

export const metadata: Metadata = {
  title: "Как это работает · Память рядом",
  description:
    "Шесть шагов от заявки до отчёта: что делаем мы, что нужно от вас и в какой момент вы платите.",
};

const STEPS = [
  {
    title: "Вы оставляете заявку",
    body:
      "Нужны кладбище, участок и любые приметы: соседний памятник, поворот от главной аллеи, " +
      "цвет ограды. Если есть фотография — приложите. Чем точнее описание, тем меньше шансов, " +
      "что исполнитель уедет искать место вместо того, чтобы работать.",
    yours: "5–10 минут",
  },
  {
    title: "Мы проверяем, что место найдётся",
    body:
      "Диспетчер сверяет описание с тем, что известно о кладбище. Если по вашим данным участок " +
      "не опознать, мы скажем об этом сразу и предложим сначала выезд на осмотр — а не отправим " +
      "человека искать наугад за ваши деньги.",
    yours: "ничего",
  },
  {
    title: "Присылаем смету",
    body:
      "Построчно: работы, материалы, доставка. Видно, за что именно вы платите. Пока вы не " +
      "приняли смету, никто никуда не выезжает и ничего не списывается.",
    yours: "решение",
  },
  {
    title: "Вы оплачиваете",
    body:
      "Оплата картой онлайн. Если на месте выяснится, что нужны дополнительные работы, мы " +
      "остановимся и спросим вас — смета пересобирается и требует нового согласия. Доплат без " +
      "вашего «да» не бывает.",
    yours: "2 минуты",
  },
  {
    title: "Исполнитель выезжает",
    body:
      "Работу назначаем мы. Вы не ищете исполнителя, не договариваетесь и не связываетесь с ним " +
      "напрямую — за результат отвечаем мы, а не человек, которого вы нашли в объявлениях. " +
      "На месте он снимает участок до работ и после, тем же ракурсом.",
    yours: "ничего",
  },
  {
    title: "Отчёт и ваше решение",
    body:
      "Сначала отчёт смотрит наш контролёр: сверяет фотографии с чек-листом и, если что-то не " +
      "сделано, возвращает исполнителю. До вас доходит только проверенная работа. Дальше вы " +
      "принимаете её или пишете, что не так.",
    yours: "5 минут",
  },
];

export default function HowItWorksPage() {
  return (
    <>
      <PageHeader
        eyebrow="Как это работает"
        title="Шесть шагов от заявки до отчёта"
        lead={
          <>
            Мы не биржа исполнителей: вы не выбираете человека и не договариваетесь с ним. Вы
            описываете место, мы находим исполнителя, проверяем работу и показываем результат.
            Отвечаем за него тоже мы.
          </>
        }
      />

      <ol className="flex list-none flex-col gap-4 p-0">
        {STEPS.map((step, index) => (
          <li key={step.title}>
            <Card as="article" className="flex flex-col gap-3 sm:flex-row sm:gap-6">
              <span
                aria-hidden="true"
                className="font-display text-2xl tabular-nums text-accent-deep sm:w-10"
              >
                {index + 1}
              </span>
              <div className="flex flex-1 flex-col gap-2">
                <h2 className="font-display text-xl font-normal text-ink-1">
                  {/* Numbered for sighted readers by the digit above; spelled out here so a
                      screen reader announces position rather than a bare heading. */}
                  <span className="sr-only">Шаг {index + 1}. </span>
                  {step.title}
                </h2>
                <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">{step.body}</p>
                <p className="text-sm text-ink-2">
                  От вас: <span className="text-ink-1">{step.yours}</span>
                </p>
              </div>
            </Card>
          </li>
        ))}
      </ol>

      <Card className="flex flex-col gap-4">
        <h2 className="font-display text-xl font-normal text-ink-1">Если что-то пойдёт не так</h2>
        <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
          Исполнитель может не доехать, погода может отменить работы, а участок — оказаться под
          снегом. Это нормальная часть дела, и мы говорим об этом прямо: вы увидите, что именно не
          получилось и почему. Работа, которую не сделали, не оплачивается.
        </p>
        <p className="max-w-[62ch] text-base leading-relaxed text-ink-2">
          Если результат вас не устроил, у вас есть{" "}
          <Link href="/trust" className="text-accent hover:text-accent-deep">
            гарантийный срок
          </Link>{" "}
          — мы переделаем или вернём деньги.
        </p>
      </Card>

      <div className="flex flex-wrap gap-3">
        <ButtonLink href="/services">Посмотреть услуги</ButtonLink>
        <ButtonLink href="/login" variant="secondary">
          Оставить заявку
        </ButtonLink>
      </div>
    </>
  );
}
