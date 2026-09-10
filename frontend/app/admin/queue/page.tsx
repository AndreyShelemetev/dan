import Link from "next/link";
import { Card } from "@/components/ui/Card";
import { adminOrders } from "@/lib/api/adminOrders";
import { type OrderSummary } from "@/lib/api/orders";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

/**
 * The dispatcher's queue.
 *
 * Split three ways, because "everything that isn't finished" is not a work list. Only the first
 * group is work; the other two are here so an order that stops moving gets chased instead of
 * quietly ageing out of sight.
 */
const GROUPS = [
  {
    key: "staff",
    title: "Ждут нас",
    hint: "Следующий шаг за сотрудником.",
  },
  {
    key: "customer",
    title: "Ждут клиента",
    hint: "Ход за клиентом — подтвердить смету, оплатить, принять работу. Смотрим, не застряло ли.",
  },
  {
    key: "in_flight",
    title: "В работе",
    hint: "Назначено исполнителю. Вмешиваемся, только если остановилось.",
  },
] as const;

export default async function AdminQueuePage() {
  const queue = await adminOrders.queue(undefined, getSessionCookieHeader());

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="font-display text-2xl font-normal text-ink-1">Очередь заказов</h1>
        <p className="mt-1 max-w-prose text-sm text-ink-2">
          Все незакрытые заказы. Завершённые, отменённые и возвращённые сюда не попадают.
        </p>
      </div>

      {queue.length === 0 ? (
        <Card>
          <p className="text-sm text-ink-2">Незакрытых заказов нет.</p>
        </Card>
      ) : (
        GROUPS.map((group) => {
          const rows = queue.filter((order) => order.queueGroup === group.key);
          return (
            <section key={group.key} className="flex flex-col gap-3">
              <div>
                <h2 className="font-display text-lg font-normal text-ink-1">
                  {group.title}{" "}
                  <span className="tabular-nums text-ink-2">({rows.length})</span>
                </h2>
                <p className="mt-1 max-w-prose text-sm text-ink-2">{group.hint}</p>
              </div>
              {rows.length === 0 ? (
                <Card>
                  <p className="text-sm text-ink-2">Пусто.</p>
                </Card>
              ) : (
                <Card>
                  <QueueTable rows={rows} caption={group.title} />
                </Card>
              )}
            </section>
          );
        })
      )}
    </div>
  );
}

function QueueTable({ rows, caption }: { rows: OrderSummary[]; caption: string }) {
  return (
    <div className="-mx-2 overflow-x-auto">
      <table className="w-full min-w-[40rem] border-collapse text-sm">
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr className="border-b border-border text-left text-xs uppercase tracking-caps text-ink-2">
            <th scope="col" className="px-2 py-2 font-semibold">Заказ</th>
            <th scope="col" className="px-2 py-2 font-semibold">Статус</th>
            <th scope="col" className="px-2 py-2 font-semibold">Пакет</th>
            <th scope="col" className="px-2 py-2 font-semibold">Место памяти</th>
            <th scope="col" className="px-2 py-2 font-semibold">Создан</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((order) => (
            <tr key={order.id} className="border-b border-border last:border-0">
              <td className="px-2 py-1">
                <Link
                  href={`/admin/queue/${order.id}`}
                  className="inline-flex min-h-hit items-center tabular-nums text-accent no-underline hover:text-accent-deep"
                >
                  {order.number}
                </Link>
              </td>
              <td className="px-2 py-3 text-ink-1">{order.statusLabel}</td>
              <td className="px-2 py-3 text-ink-2">{order.packageTitle}</td>
              <td className="px-2 py-3 text-ink-2">{order.deceasedFullName}</td>
              <td className="px-2 py-3 tabular-nums text-ink-2">
                <time dateTime={order.createdAt}>
                  {new Date(order.createdAt).toLocaleDateString("ru-RU")}
                </time>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
