import Link from "next/link";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { Card } from "@/components/ui/Card";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";
import { orders, type OrderSummary } from "@/lib/api/orders";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

function OrderRow({ order }: { order: OrderSummary }) {
  return (
    <Card as="li" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-normal text-ink-1">
            <Link
              href={`/cabinet/orders/${order.id}`}
              className="text-ink-1 no-underline hover:text-accent-deep"
            >
              {order.packageTitle}
            </Link>
          </h2>
          <p className="mt-1 text-sm text-ink-2">
            {order.deceasedFullName} · {order.number}
          </p>
        </div>
        <span className="rounded-pill border border-accent bg-accent-soft px-3 py-1 text-xs font-semibold text-accent-deep">
          {order.statusLabel}
        </span>
      </div>

      <div className="flex flex-wrap items-center gap-3 border-t border-border pt-4">
        <ButtonLink href={`/cabinet/orders/${order.id}`} variant="secondary" size="sm">
          Открыть
        </ButtonLink>
        {/* The CTA is what the client is expected to do now; when it is null the ball is on our
            side and saying nothing is more honest than inventing a prompt. */}
        {order.cta ? <span className="text-sm text-accent-deep">{order.cta}</span> : null}
      </div>
    </Card>
  );
}

export default async function OrdersPage() {
  const list = await orders.list(getSessionCookieHeader());

  return (
    <div className="flex flex-col gap-8">
      <div>
        <SectionEyebrow>Ваш кабинет</SectionEyebrow>
        <h1 className="mt-3 font-display text-3xl font-normal text-ink-1">Мои заказы</h1>
      </div>

      {list.length === 0 ? (
        <Card className="text-center">
          <h2 className="mb-3 font-display text-xl font-normal text-ink-1">Заказов пока нет</h2>
          <p className="mx-auto mb-7 max-w-measure text-sm text-ink-2">
            Заказ создаётся из карточки места памяти: так мы точно знаем, о каком месте речь, и
            можем оценить, достаточно ли данных, чтобы направить исполнителя.
          </p>
          <ButtonLink href="/cabinet">Мои места памяти</ButtonLink>
        </Card>
      ) : (
        <ul className="grid list-none grid-cols-1 gap-4 p-0 lg:grid-cols-2">
          {list.map((order) => (
            <OrderRow key={order.id} order={order} />
          ))}
        </ul>
      )}
    </div>
  );
}
