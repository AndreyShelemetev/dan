import Link from "next/link";
import { notFound } from "next/navigation";
import { Card } from "@/components/ui/Card";
import { EstimateEditor } from "@/components/admin/EstimateEditor";
import { AssignExecutor } from "@/components/admin/AssignExecutor";
import { OrderTimeline } from "@/components/cabinet/OrderTimeline";
import { ApiError } from "@/lib/api/client";
import { formatRub } from "@/lib/api/catalog";
import { adminOrders } from "@/lib/api/adminOrders";
import { qa } from "@/lib/api/visits";
import { MEDIA_OWNER, listMedia } from "@/lib/api/media";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

export default async function AdminOrderPage({ params }: { params: { id: string } }) {
  const id = Number(params.id);
  if (!Number.isFinite(id)) notFound();

  const cookieHeader = getSessionCookieHeader();

  let order;
  try {
    order = await adminOrders.get(id, cookieHeader);
  } catch (err) {
    if (err instanceof ApiError && (err.status === 404 || err.isUnauthorized)) notFound();
    throw err;
  }

  // What the client attached to the request — the reason a dispatcher can price it without a
  // trip of their own.
  const photos = await listMedia(MEDIA_OWNER.order, id, cookieHeader).catch(() => []);
  const published = order.estimates.filter((e) => e.status !== "draft");

  // Dispatch only becomes relevant once the money is in. Before that the card would show a
  // control that refuses every click.
  const dispatchable = ["paid", "assigning", "assigned", "in_progress", "qa_review"].includes(
    order.status,
  );

  const [executors, visits] = dispatchable
    ? await Promise.all([
        qa.executors(cookieHeader).catch(() => []),
        qa.forOrder(id, cookieHeader).catch(() => []),
      ])
    : [[], []];

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm">
        <Link href="/admin/queue" className="inline-flex min-h-hit items-center text-ink-2 hover:text-accent-deep">
          ← Очередь
        </Link>
      </p>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-caps text-ink-2">{order.number}</p>
          <h1 className="mt-2 font-display text-2xl font-normal text-ink-1">{order.packageTitle}</h1>
          <p className="mt-1 text-sm text-ink-2">
            {order.deceasedFullName} · пакет {order.packageCode} v{order.packageVersion} · от{" "}
            {formatRub(order.packagePriceFromRub)} ₽
          </p>
        </div>
        <span className="rounded-pill border border-accent bg-accent-soft px-4 py-1.5 text-sm font-semibold text-accent-deep">
          {order.statusLabel}
        </span>
      </div>

      {order.comment ? (
        <Card>
          <h2 className="font-display text-lg font-normal text-ink-1">Комментарий клиента</h2>
          <p className="mt-2 whitespace-pre-line text-sm text-ink-1">{order.comment}</p>
        </Card>
      ) : null}

      {photos.length > 0 ? (
        <Card as="section" aria-labelledby="client-photos" className="flex flex-col gap-4">
          <h2 id="client-photos" className="font-display text-lg font-normal text-ink-1">
            Фотографии от клиента ({photos.length})
          </h2>
          <ul className="grid list-none grid-cols-2 gap-3 p-0 sm:grid-cols-4">
            {photos.map((photo, index) => (
              <li key={photo.id}>
                <a
                  href={photo.url ?? "#"}
                  target="_blank"
                  rel="noreferrer"
                  className="block overflow-hidden rounded-card bg-surface-raised outline outline-1 -outline-offset-1 outline-black/10"
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={photo.thumbnailUrl ?? photo.url ?? ""}
                    alt={`Фотография от клиента ${index + 1} из ${photos.length}`}
                    loading="lazy"
                    className="aspect-square w-full object-cover"
                  />
                </a>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      {dispatchable ? (
        <AssignExecutor orderId={order.id} executors={executors} visits={visits} />
      ) : (
        <EstimateEditor order={order} />
      )}

      {published.map((estimate) => (
        <Card key={estimate.version} className="flex flex-col gap-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 className="font-display text-lg font-normal text-ink-1">
              Смета v{estimate.version}
            </h2>
            <span className="text-xs text-ink-2">
              {estimate.status === "accepted"
                ? "принята клиентом"
                : estimate.status === "published"
                  ? "ждёт решения клиента"
                  : estimate.status === "rejected"
                    ? "отклонена"
                    : "заменена"}
            </span>
          </div>
          <p className="tabular-nums text-sm text-ink-1">{formatRub(estimate.totalRub)} ₽</p>
        </Card>
      ))}

      <Card className="flex flex-col gap-4">
        <h2 className="font-display text-lg font-normal text-ink-1">История</h2>
        <OrderTimeline history={order.history} />
      </Card>
    </div>
  );
}
