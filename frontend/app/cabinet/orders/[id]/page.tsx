import Link from "next/link";
import { notFound } from "next/navigation";
import { Card } from "@/components/ui/Card";
import { SectionEyebrow } from "@/components/ui/SectionEyebrow";
import { EstimateCard } from "@/components/cabinet/EstimateCard";
import { OrderTimeline } from "@/components/cabinet/OrderTimeline";
import { OrderActions } from "@/components/cabinet/OrderActions";
import { PhotoGallery } from "@/components/cabinet/PhotoGallery";
import { PaymentPanel } from "@/components/cabinet/PaymentPanel";
import { ReportPanel } from "@/components/cabinet/ReportPanel";
import { ApiError } from "@/lib/api/client";
import { formatRub } from "@/lib/api/catalog";
import { orders } from "@/lib/api/orders";
import { MEDIA_OWNER, listMedia } from "@/lib/api/media";
import { payments } from "@/lib/api/payments";
import { getReport } from "@/lib/api/visits";
import { getSessionCookieHeader } from "@/lib/auth/serverCookie";

export const dynamic = "force-dynamic";

export default async function OrderPage({ params }: { params: { id: string } }) {
  const id = Number(params.id);
  if (!Number.isFinite(id)) notFound();

  let order;
  try {
    order = await orders.get(id, getSessionCookieHeader());
  } catch (err) {
    // 404 covers both "no such order" and "not yours" — telling them apart would confirm the
    // order exists.
    if (err instanceof ApiError && (err.status === 404 || err.isUnauthorized)) notFound();
    throw err;
  }

  const photos = await listMedia(MEDIA_OWNER.order, id, getSessionCookieHeader()).catch(() => []);

  // Photos can be attached while the client still shapes the request. Once it has been priced
  // they become part of what the estimate was based on, so swapping them would quietly change
  // the evidence behind an agreed figure — the server enforces the same rule.
  const photosEditable = ["draft", "submitted", "location_review"].includes(order.status);

  // Both are absent for most of an order's life — a request not yet priced has nothing to pay
  // for, and a report exists only once QA has passed it. Missing is the normal case, not a
  // failure, so neither is allowed to take the page down with it.
  const accepted = order.estimates.find((e) => e.status === "accepted");

  const payment =
    order.status === "awaiting_payment"
      ? await payments.latest(id, getSessionCookieHeader()).catch(() => null)
      : null;

  const report = ["customer_review", "completed", "disputed"].includes(order.status)
    ? await getReport(id, getSessionCookieHeader()).catch(() => null)
    : null;

  // Only one version is ever awaiting a decision; the rest are shown for reference.
  const pending = order.estimates.find((e) => e.status === "published");
  const others = order.estimates.filter((e) => e !== pending);

  return (
    <div className="flex flex-col gap-8">
      <p className="text-sm">
        <Link href="/cabinet/orders" className="inline-flex min-h-hit items-center text-ink-2 hover:text-accent-deep">
          ← Мои заказы
        </Link>
      </p>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <SectionEyebrow>Заказ {order.number}</SectionEyebrow>
          <h1 className="mt-3 font-display text-3xl font-normal text-ink-1">{order.packageTitle}</h1>
          <p className="mt-2 text-base text-ink-2">{order.deceasedFullName}</p>
        </div>
        <span className="rounded-pill border border-accent bg-accent-soft px-4 py-1.5 text-sm font-semibold text-accent-deep">
          {order.statusLabel}
        </span>
      </div>

      {pending ? <EstimateCard orderId={order.id} estimate={pending} decidable /> : null}

      {order.status === "awaiting_payment" && accepted ? (
        <PaymentPanel orderId={order.id} amountRub={accepted.totalRub} initialPayment={payment} />
      ) : null}

      {report ? (
        <ReportPanel
          orderId={order.id}
          report={report}
          decidable={order.status === "customer_review"}
        />
      ) : null}

      <Card className="flex flex-col gap-5">
        <h2 className="font-display text-xl font-normal text-ink-1">О заказе</h2>
        <dl className="grid gap-5 sm:grid-cols-2">
          <div>
            <dt className="text-sm text-ink-2">Пакет</dt>
            <dd className="mt-0.5 text-base text-ink-1">
              {order.packageTitle} · от {formatRub(order.packagePriceFromRub)} ₽
            </dd>
          </div>
          <div>
            <dt className="text-sm text-ink-2">Гарантия</dt>
            <dd className="mt-0.5 text-base text-ink-1">
              {order.warrantyDays} дней после приёмки
            </dd>
          </div>
          {order.comment ? (
            <div className="sm:col-span-2">
              <dt className="text-sm text-ink-2">Ваш комментарий</dt>
              <dd className="mt-0.5 whitespace-pre-line text-base text-ink-1">{order.comment}</dd>
            </div>
          ) : null}
          {order.cancellationReason ? (
            <div className="sm:col-span-2">
              <dt className="text-sm text-ink-2">Причина отмены</dt>
              <dd className="mt-0.5 text-base text-ink-1">{order.cancellationReason}</dd>
            </div>
          ) : null}
        </dl>

        <OrderActions order={order} />
      </Card>

      <PhotoGallery
        siteId={order.id}
        ownerType={MEDIA_OWNER.order}
        initialPhotos={photos}
        canManage={photosEditable}
        title="Фотографии к заявке"
        hint="Снимки помогают точнее оценить работу и найти место. Видны только вам и сотрудникам сервиса."
        emptyHint="Пока фотографий нет. Добавьте снимки — что нужно сделать, в каком состоянии участок сейчас. Чем понятнее, тем точнее смета."
      />

      {others.map((estimate) => (
        <EstimateCard
          key={estimate.version}
          orderId={order.id}
          estimate={estimate}
          decidable={false}
        />
      ))}

      <Card className="flex flex-col gap-4">
        <h2 className="font-display text-xl font-normal text-ink-1">Что происходило</h2>
        <OrderTimeline history={order.history} />
      </Card>
    </div>
  );
}
